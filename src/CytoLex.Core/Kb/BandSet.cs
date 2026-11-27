using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;

namespace CytoLex.Core.Kb;

/// <summary>A genomic range on a chromosome (1-based, inclusive), as in the runtime bundle.</summary>
public sealed record GenomicRange(long Start, long End)
{
    /// <summary>The smallest range holding both.</summary>
    public GenomicRange Union(GenomicRange other) => new(Math.Min(Start, other.Start), Math.Max(End, other.End));

    /// <summary>Whether <paramref name="other"/> lies inside this range.</summary>
    public bool Contains(GenomicRange other) => other.Start >= Start && other.End <= End;
}

/// <summary>The genome assemblies the band data has coordinates for.</summary>
public enum ReferenceBuild
{
    /// <summary>GRCh37 (hg19).</summary>
    Grch37,

    /// <summary>GRCh38 (hg38).</summary>
    Grch38,
}

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
/// <param name="Stain">
/// The Giemsa stain of the finest NCBI level with the name (<c>gneg</c>, <c>gpos25</c>…<c>gpos100</c>, <c>acen</c>,
/// <c>gvar</c>, <c>stalk</c>); <c>null</c> for nesting ancestors, book-only names and bundles without stain (T-101).
/// </param>
public sealed record Band(
    string Name,
    string Chromosome,
    string Arm,
    string? Parent,
    ImmutableArray<int> Levels,
    bool Derived,
    bool Book,
    GenomicRange? Grch37,
    GenomicRange? Grch38,
    string? Stain = null)
{
    /// <summary>The coordinates in <paramref name="build"/>, if known.</summary>
    public GenomicRange? In(ReferenceBuild build) => build == ReferenceBuild.Grch37 ? Grch37 : Grch38;
}

/// <summary>The band names of one edition (docs/design/core.md §6 <c>IBandSet</c>), looked up by exact name.</summary>
public sealed class BandSet
{
    private readonly FrozenDictionary<string, Band> _byName;
    private readonly FrozenSet<string> _regions;
    private readonly FrozenDictionary<string, ImmutableArray<Band>> _byChromosome;

    private BandSet(ImmutableArray<int> levelsAvailable, ImmutableArray<Band> bands)
    {
        LevelsAvailable = levelsAvailable;
        Bands = bands;
        _byName = bands.ToFrozenDictionary(b => b.Name, StringComparer.Ordinal);
        _regions = bands
            .Select(b => b.Name.Length > b.Chromosome.Length + 1 ? b.Name[..(b.Chromosome.Length + 2)] : null)
            .OfType<string>()
            .ToFrozenSet(StringComparer.Ordinal);
        Chromosomes = [.. bands.Select(b => b.Chromosome).Distinct(StringComparer.Ordinal)];
        _byChromosome = bands
            .GroupBy(b => b.Chromosome, StringComparer.Ordinal)
            .ToFrozenDictionary(g => g.Key, g => g.ToImmutableArray(), StringComparer.Ordinal);
    }

    /// <summary>The chromosomes of the data, in bundle order (1–22, X, Y).</summary>
    public ImmutableArray<string> Chromosomes { get; }

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

    /// <summary>The bands of <paramref name="chromosome"/> in bundle order (pter → qter, parents before children); empty if none.</summary>
    public ImmutableArray<Band> OfChromosome(string chromosome) => _byChromosome.GetValueOrDefault(chromosome, []);

    /// <summary>The whole chromosome in <paramref name="build"/>: the smallest start to the largest end of its bands.</summary>
    public GenomicRange? ChromosomeRange(string chromosome, ReferenceBuild build) => Cover(OfChromosome(chromosome), build);

    /// <summary>One arm (<c>p</c> or <c>q</c>) of <paramref name="chromosome"/> in <paramref name="build"/>.</summary>
    public GenomicRange? ArmRange(string chromosome, string arm, ReferenceBuild build) =>
        Cover(OfChromosome(chromosome).Where(b => b.Arm == arm), build);

    private static GenomicRange? Cover(IEnumerable<Band> bands, ReferenceBuild build)
    {
        GenomicRange? cover = null;
        foreach (var band in bands)
        {
            if (band.In(build) is { } r)
            {
                cover = cover is null ? r : cover.Union(r);
            }
        }

        return cover;
    }

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
            Range(b, "grch38"),
            b.TryGetProperty("stain", out var stain) && stain.ValueKind == JsonValueKind.String ? stain.GetString() : null)).ToImmutableArray();
        return new BandSet(levels, list);
    }

    private static GenomicRange? Range(JsonElement band, string name) =>
        band.TryGetProperty(name, out var r) && r.ValueKind == JsonValueKind.Array
            ? new GenomicRange(r[0].GetInt64(), r[1].GetInt64())
            : null;
}
