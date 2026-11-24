using System.Collections.Immutable;
using System.Text.Json;
using CytoLex.Core.Kb;
using CytoLex.Core.Translation;
using Microsoft.Data.Sqlite;

namespace CytoLex.Desktop.Rules;

/// <summary>A cited book-vs-NCBI decision on a band group (<c>kb/bands/&lt;version&gt;/exceptions.yaml</c>, T-007).</summary>
/// <param name="Level">The band level compared (400, 550 or 850).</param>
/// <param name="Region">The shortest name of the group.</param>
/// <param name="Resolution"><c>book</c> (the level follows the book) or <c>ncbi</c>.</param>
/// <param name="Book">Names the book shows at this level and NCBI doesn't.</param>
/// <param name="Ncbi">Names NCBI has at this level and the book doesn't.</param>
/// <param name="Keep">NCBI-only names kept valid although no book level shows them.</param>
/// <param name="Note">The decision, in words.</param>
/// <param name="Citation">The rendered book citation (<c>ISCN 2016 §2.5, p.21, Fig. 5</c>).</param>
public sealed record BandDecision(
    int Level,
    string Region,
    string Resolution,
    ImmutableArray<string> Book,
    ImmutableArray<string> Ncbi,
    ImmutableArray<string> Keep,
    string Note,
    string Citation);

/// <summary>Everything the KB database holds about one band in one edition (T-084).</summary>
/// <param name="Edition">The ISCN edition.</param>
/// <param name="Band">The band, as the core reads it from the runtime bundle (name, chromosome, arm, parent, levels, flags, coordinates).</param>
/// <param name="Stain">The Giemsa stain of the finest source level (<c>gneg</c>, <c>gpos50</c>, …), if any.</param>
/// <param name="Children">The sub-bands, in band order.</param>
/// <param name="Exceptions">The band decisions that name the band (as region or in one of their lists).</param>
public sealed record BandDetail(string Edition, Band Band, string? Stain, ImmutableArray<string> Children, ImmutableArray<BandDecision> Exceptions)
{
    /// <summary>The band name.</summary>
    public string Name => Band.Name;

    /// <summary>The band's coordinates in the shape of the translation's band coordinates (same text helpers).</summary>
    public BandCoordinates Coordinates => new(Band.Name, Band.Grch37, Band.Grch38, Band is { Book: true, Grch37: null, Grch38: null });
}

/// <summary>
/// The bands of <c>build/kb.sqlite</c> (tables <c>bands</c>, <c>band_exceptions</c>, written by <c>kb build</c> from
/// <c>kb/bands/&lt;version&gt;/</c>) for the Bands tab (T-084): every band in band order, and one band's detail.
/// </summary>
internal sealed class BandStore(SqliteConnection connection)
{
    private const string _columns = "name, chrom, arm, parent, levels, derived, book, grch37_start, grch37_end, grch38_start, grch38_end, stain";

    /// <summary>Every band of <paramref name="edition"/>, in band order (pter → qter per chromosome).</summary>
    public ImmutableArray<Band> Rows(string edition)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT {_columns} FROM bands WHERE version = $v ORDER BY ord";
        cmd.Parameters.AddWithValue("$v", edition);
        var rows = ImmutableArray.CreateBuilder<Band>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            rows.Add(BandOf(r));
        }

        return rows.ToImmutable();
    }

    /// <summary>The band <paramref name="name"/> of <paramref name="edition"/>, or <c>null</c>.</summary>
    public BandDetail? Detail(string edition, string name)
    {
        Band band;
        string? stain;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT {_columns} FROM bands WHERE version = $v AND name = $n";
            cmd.Parameters.AddWithValue("$v", edition);
            cmd.Parameters.AddWithValue("$n", name);
            using var r = cmd.ExecuteReader();
            if (!r.Read())
            {
                return null;
            }

            band = BandOf(r);
            stain = r.IsDBNull(11) ? null : r.GetString(11);
        }

        return new BandDetail(edition, band, stain, Children(edition, name), Exceptions(edition, name));
    }

    private static Band BandOf(SqliteDataReader r) => new(
        r.GetString(0),
        Chromosome: r.GetString(1),
        Arm: r.GetString(2),
        Parent: r.IsDBNull(3) ? null : r.GetString(3),
        Levels: LevelsOf(r.GetString(4)),
        Derived: r.GetInt32(5) != 0,
        Book: r.GetInt32(6) != 0,
        Grch37: RangeOf(r, 7),
        Grch38: RangeOf(r, 9));

    private static GenomicRange? RangeOf(SqliteDataReader r, int start) =>
        r.IsDBNull(start) || r.IsDBNull(start + 1) ? null : new GenomicRange(r.GetInt64(start), r.GetInt64(start + 1));

    private static ImmutableArray<int> LevelsOf(string json) => [.. JsonSerializer.Deserialize<List<int>>(json) ?? []];

    private static ImmutableArray<string> StringsOf(string json) => [.. JsonSerializer.Deserialize<List<string>>(json) ?? []];

    private ImmutableArray<string> Children(string edition, string name)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM bands WHERE version = $v AND parent = $n ORDER BY ord";
        cmd.Parameters.AddWithValue("$v", edition);
        cmd.Parameters.AddWithValue("$n", name);
        using var r = cmd.ExecuteReader();
        var list = ImmutableArray.CreateBuilder<string>();
        while (r.Read())
        {
            list.Add(r.GetString(0));
        }

        return list.ToImmutable();
    }

    // About 20 rows per edition: read them all and keep the ones that name the band.
    private ImmutableArray<BandDecision> Exceptions(string edition, string name)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT level, region, resolution, book, ncbi, keep, note, cite_rendered FROM band_exceptions WHERE version = $v ORDER BY ord";
        cmd.Parameters.AddWithValue("$v", edition);
        using var r = cmd.ExecuteReader();
        var list = ImmutableArray.CreateBuilder<BandDecision>();
        while (r.Read())
        {
            var x = new BandDecision(
                r.GetInt32(0),
                r.GetString(1),
                r.GetString(2),
                StringsOf(r.GetString(3)),
                StringsOf(r.GetString(4)),
                StringsOf(r.GetString(5)),
                r.GetString(6),
                r.GetString(7));
            if (x.Region == name || x.Book.Contains(name) || x.Ncbi.Contains(name) || x.Keep.Contains(name))
            {
                list.Add(x);
            }
        }

        return list.ToImmutable();
    }
}
