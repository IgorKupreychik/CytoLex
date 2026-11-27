using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;

namespace CytoLex.Core.Kb;

/// <summary>
/// One row of the probe table (<c>curated/probes/probes.csv</c>, T-102): where a FISH probe lies, where that comes
/// from and whether a person has checked it.
/// </summary>
/// <param name="Name">The probe name as written in ISCN strings (<c>ABL1</c>).</param>
/// <param name="Aliases">Other names of the same probe (<c>KAL1</c> for <c>ANOS1</c>).</param>
/// <param name="Kind"><c>gene</c>, <c>centromere</c>, <c>clone</c>, <c>paint</c>, <c>commercial</c> or <c>other</c>.</param>
/// <param name="Chromosome">1–22, X or Y.</param>
/// <param name="Band">The band or band range (<c>9q34.12</c>, <c>Xp21.2-p21.1</c>).</param>
/// <param name="Grch37">GRCh37 positions; <c>null</c> when unknown.</param>
/// <param name="Grch38">GRCh38 positions; <c>null</c> when unknown.</param>
/// <param name="Source">Where the row's data come from; ends with <c>(auto)</c> for rows filled in by <c>kb probes</c>.</param>
/// <param name="Reviewed">Whether a person has checked the row against its source.</param>
public sealed record ProbeRow(
    string Name,
    ImmutableArray<string> Aliases,
    string Kind,
    string Chromosome,
    string Band,
    GenomicRange? Grch37,
    GenomicRange? Grch38,
    string Source,
    bool Reviewed)
{
    /// <summary>The first band of <see cref="Band"/> (<c>Xp21.2</c> of <c>Xp21.2-p21.1</c>).</summary>
    public string FromBand => Band.Split('-')[0];

    /// <summary>The last band of a range with its chromosome (<c>Xp21.1</c>); <c>null</c> for one band.</summary>
    public string? ToBand => Band.Split('-') is [_, var to] ? (to.StartsWith(Chromosome, StringComparison.Ordinal) ? to : Chromosome + to) : null;

    /// <summary>The positions in <paramref name="build"/>.</summary>
    public GenomicRange? In(ReferenceBuild build) => build == ReferenceBuild.Grch37 ? Grch37 : Grch38;
}

/// <summary>A probe table row found for a locus name, and whether the name named one end (<c>5′</c>/<c>3′</c>) of it.</summary>
/// <param name="Row">The row.</param>
/// <param name="End">The written end prefix (<c>5′</c>, <c>3′</c>); <c>null</c> for the whole probe.</param>
public sealed record ProbeMatch(ProbeRow Row, string? End);

/// <summary>The probe table of the runtime bundle (top-level <c>probes</c>, T-102); edition-independent.</summary>
public sealed class ProbeTable
{
    private readonly FrozenDictionary<string, ProbeRow> _byName;

    private ProbeTable(ImmutableArray<ProbeRow> rows)
    {
        Rows = rows;
        var map = new Dictionary<string, ProbeRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            map.TryAdd(row.Name, row);
            foreach (var alias in row.Aliases)
            {
                map.TryAdd(alias, row);
            }
        }

        _byName = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>An empty table.</summary>
    public static ProbeTable Empty { get; } = new([]);

    /// <summary>Every row, in file order.</summary>
    public ImmutableArray<ProbeRow> Rows { get; }

    /// <summary>
    /// The row of a locus name, by name or alias, ignoring case; a leading <c>5′</c>/<c>3′</c> (one end of a gene,
    /// §13.3.1 ¶1) is set aside. <c>null</c> when the table has no row for it.
    /// </summary>
    public ProbeMatch? Find(string locusName)
    {
        ArgumentNullException.ThrowIfNull(locusName);
        string? end = null;
        var name = locusName;
        if (name.Length > 2 && name[0] is '5' or '3' && name[1] is '′' or '\'')
        {
            end = name[..2];
            name = name[2..];
        }

        return _byName.TryGetValue(name, out var row) ? new ProbeMatch(row, end) : null;
    }

    /// <summary>Reads the top-level <c>probes</c> array of the runtime bundle.</summary>
    internal static ProbeTable FromBundle(JsonElement probes)
    {
        if (probes.ValueKind != JsonValueKind.Array)
        {
            return Empty;
        }

        return new ProbeTable([.. probes.EnumerateArray().Select(p => new ProbeRow(
            p.GetProperty("name").GetString()!,
            p.TryGetProperty("aliases", out var a) && a.ValueKind == JsonValueKind.Array ? [.. a.EnumerateArray().Select(x => x.GetString()!)] : [],
            p.GetProperty("kind").GetString()!,
            p.GetProperty("chrom").GetString()!,
            p.GetProperty("band").GetString()!,
            Range(p, "grch37"),
            Range(p, "grch38"),
            p.GetProperty("source").GetString()!,
            p.GetProperty("reviewed").GetBoolean()))]);
    }

    private static GenomicRange? Range(JsonElement row, string name) =>
        row.TryGetProperty(name, out var r) && r.ValueKind == JsonValueKind.Array
            ? new GenomicRange(r[0].GetInt64(), r[1].GetInt64())
            : null;
}
