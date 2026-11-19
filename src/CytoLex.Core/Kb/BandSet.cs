using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;

namespace CytoLex.Core.Kb;

/// <summary>A genomic range on a chromosome (1-based, inclusive), as in the runtime bundle.</summary>
public sealed record GenomicRange(long Start, long End);

/// <summary>One band name of an edition (kb/bands/&lt;version&gt;/bands.yaml via the runtime bundle).</summary>
/// <param name="Name">The band name, e.g. <c>5q13</c>, <c>1p36.3</c>.</param>
/// <param name="Chromosome">The chromosome: <c>1</c>–<c>22</c>, <c>X</c>, <c>Y</c>.</param>
/// <param name="Arm"><c>p</c> or <c>q</c>.</param>
/// <param name="Parent">The enclosing band, if any.</param>
/// <param name="Levels">The band levels (300/400/550/700/850) the name exists at; empty for a nesting ancestor only.</param>
/// <param name="Derived">A nesting ancestor derived from its sub-bands rather than printed at a level.</param>
/// <param name="Book">The name appears in the book's idiograms; <c>false</c> = from NCBI only.</param>
/// <param name="Grch37">GRCh37 coordinates, if known.</param>
/// <param name="Grch38">GRCh38 coordinates, if known.</param>
public sealed record Band(
    string Name,
    string Chromosome,
    string Arm,
    string? Parent,
    ImmutableArray<int> Levels,
    bool Derived,
    bool Book,
    GenomicRange? Grch37,
    GenomicRange? Grch38);

/// <summary>The band names of one edition (docs/design/core.md §6 <c>IBandSet</c>), looked up by exact name.</summary>
public sealed class BandSet
{
    private readonly FrozenDictionary<string, Band> _byName;
    private readonly FrozenSet<string> _regions;

    private BandSet(ImmutableArray<int> levelsAvailable, ImmutableArray<Band> bands)
    {
        LevelsAvailable = levelsAvailable;
        Bands = bands;
        _byName = bands.ToFrozenDictionary(b => b.Name, StringComparer.Ordinal);
        _regions = bands
            .Select(b => b.Name.Length > b.Chromosome.Length + 1 ? b.Name[..(b.Chromosome.Length + 2)] : null)
            .OfType<string>()
            .ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>The band levels the data covers, e.g. 300, 400, 550, 700, 850.</summary>
    public ImmutableArray<int> LevelsAvailable { get; }

    /// <summary>Every band, in bundle order.</summary>
    public ImmutableArray<Band> Bands { get; }

    /// <summary>Whether <paramref name="name"/> is a band name of the edition (exact, case-sensitive).</summary>
    public bool Contains(string name) => _byName.ContainsKey(name);

    /// <summary>
    /// Whether <paramref name="name"/> is a region of the edition (chromosome, arm and one region digit, e.g. <c>1p1</c>):
    /// some band of that chromosome arm lies in it (§4.2, region-only breakpoints).
    /// </summary>
    public bool ContainsRegion(string name) => _regions.Contains(name);

    /// <summary>The band called <paramref name="name"/>; <c>null</c> if none.</summary>
    public Band? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>Reads the <c>bands</c> object of an edition in the runtime bundle; <c>null</c> when it is JSON <c>null</c>.</summary>
    internal static BandSet? FromBundle(JsonElement bands)
    {
        if (bands.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var levels = bands.GetProperty("levels_available").EnumerateArray().Select(l => l.GetInt32()).ToImmutableArray();
        var list = bands.GetProperty("bands").EnumerateArray().Select(b => new Band(
            b.GetProperty("name").GetString()!,
            b.GetProperty("chrom").GetString()!,
            b.GetProperty("arm").GetString()!,
            b.GetProperty("parent").ValueKind == JsonValueKind.String ? b.GetProperty("parent").GetString() : null,
            [.. b.GetProperty("levels").EnumerateArray().Select(l => l.GetInt32())],
            b.GetProperty("derived").GetBoolean(),
            b.GetProperty("book").GetBoolean(),
            Range(b, "grch37"),
            Range(b, "grch38"))).ToImmutableArray();
        return new BandSet(levels, list);
    }

    private static GenomicRange? Range(JsonElement band, string name) =>
        band.TryGetProperty(name, out var r) && r.ValueKind == JsonValueKind.Array
            ? new GenomicRange(r[0].GetInt64(), r[1].GetInt64())
            : null;
}
