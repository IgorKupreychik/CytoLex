using System.Collections.Immutable;
using CytoLex.Core.Text;

namespace CytoLex.Core.Semantics;

/// <summary>The meaning of one arr segment (ISCN 2016 ch. 14, T-034): the genome build and the aberrations, in input order.</summary>
public sealed record ArrModel : SegmentModel
{
    /// <summary>The <c>arr</c> keyword.</summary>
    public required TextSpan Keyword { get; init; }

    /// <summary>The genome build (<c>[GRCh38]</c>); <c>null</c> when none is written.</summary>
    public GenomeBuild? Build { get; init; }

    /// <summary>The comma-separated results, in input order.</summary>
    public ImmutableArray<ArrAberration> Aberrations { get; init; } = [];

    /// <summary>Text between results the parser skipped.</summary>
    public ImmutableArray<UnboundItem> Unbound { get; init; } = [];

    /// <summary>Some result gives nucleotide positions (they need the genome build, GEN.GENOME_BUILD_REQUIRED).</summary>
    public bool HasPositions => Aberrations.Any(a => a.HasPositions);

    public override IEnumerable<MeaningNode> Children() => Join([Build], Aberrations, Unbound);
}

/// <summary>The genome build in square brackets after <c>arr</c>.</summary>
public sealed record GenomeBuild : MeaningNode
{
    /// <summary>The text inside the brackets, e.g. <c>GRCh38</c>.</summary>
    public required string Text { get; init; }

    /// <summary>The closing bracket is followed by whitespace.</summary>
    public bool FollowedBySpace { get; init; }

    /// <summary>The span of the closing bracket (zero-width when it is missing).</summary>
    public TextSpan CloseBracket { get; init; }
}

/// <summary>What stands in the place of the copy number.</summary>
public enum ArrResultKind
{
    /// <summary><c>×n</c> or <c>×n~m</c>.</summary>
    CopyNumber,

    /// <summary><c>amp</c>: an amplification too high to count (§14.2 E47).</summary>
    Amplification,

    /// <summary><c>cx</c>: multiple complex rearrangements (§14.2.2 ¶1).</summary>
    Complex,

    /// <summary><c>cth</c>: chromothripsis (§14.2.2 ¶2).</summary>
    Chromothripsis,
}

/// <summary>
/// One comma-separated array result: whole chromosomes or arms (<c>(8)</c>, <c>(1–22,X)</c>), or one region, or
/// regions grouped in parentheses; then the copy number, the proportion of cells and the qualifiers.
/// </summary>
public sealed record ArrAberration : MeaningNode
{
    /// <summary>Whole chromosomes or arms in parentheses; empty for a region result.</summary>
    public ImmutableArray<ArrChromosomeEntry> Chromosomes { get; init; } = [];

    /// <summary>The region(s) with their positions; several only in a grouped long form (§14.2.1 E5).</summary>
    public ImmutableArray<ArrRegion> Regions { get; init; } = [];

    /// <summary>The regions are grouped in parentheses before a shared result.</summary>
    public bool IsRegionGroup { get; init; }

    /// <summary>The first of <see cref="Results"/>; <c>null</c> when none is written (the detailed system carries one per position).</summary>
    public ArrCopyNumber? Copy { get; init; }

    /// <summary>Every copy number or word in its place written after the chromosomes or regions (one in a well-formed result).</summary>
    public ImmutableArray<ArrCopyNumber> Results { get; init; } = [];

    /// <summary>The proportion of cells (<c>[0.6]</c>).</summary>
    public ArrProportion? Proportion { get; init; }

    /// <summary>The words after the result, in input order.</summary>
    public ImmutableArray<ArrQualifier> Qualifiers { get; init; } = [];

    /// <summary>The span of the chromosomes or regions (before the result).</summary>
    public TextSpan RegionSpan { get; init; }

    /// <summary>A whole-chromosome or whole-arm result.</summary>
    public bool IsWholeChromosomes => !Chromosomes.IsEmpty;

    /// <summary>Some region gives nucleotide positions.</summary>
    public bool HasPositions => Regions.Any(r => r.Positions is not null);

    /// <summary>The detailed system: positions with a copy number each (§14.1 ¶2).</summary>
    public bool IsDetailed => Regions.Any(r => r.Positions is { IsDetailed: true });

    /// <summary>The chromosome names the result concerns, in written order (ranges expanded; <c>X</c>/<c>Y</c> as letters).</summary>
    public IEnumerable<string> ChromosomeNames =>
        IsWholeChromosomes ? Chromosomes.SelectMany(c => c.Names) : Regions.Select(r => r.Chromosome?.Name).OfType<string>();

    public override IEnumerable<MeaningNode> Children() => Join(Chromosomes, Regions, Results, [Proportion], Qualifiers);
}

/// <summary>One entry of a chromosome group: a chromosome or arm (<c>8</c>, <c>16q</c>) or a range (<c>1–22</c>).</summary>
public sealed record ArrChromosomeEntry : MeaningNode
{
    /// <summary>The chromosome (with its arm, if written).</summary>
    public required ChromosomeRef First { get; init; }

    /// <summary>The end of a range (<c>22</c> in <c>1–22</c>).</summary>
    public ChromosomeRef? Last { get; init; }

    /// <summary>The span of the range sign.</summary>
    public TextSpan? RangeSign { get; init; }

    /// <summary>The chromosome names covered: one, or every number of a numeric range.</summary>
    public ImmutableArray<string> Names { get; init; } = [];

    public override IEnumerable<MeaningNode> Children() => Join([First, Last]);
}

/// <summary>A region: the chromosome with the first and last band of the segment (or one band), and the positions.</summary>
public sealed record ArrRegion : MeaningNode
{
    /// <summary>The chromosome (written with the first band).</summary>
    public ChromosomeRef? Chromosome { get; init; }

    /// <summary>The bands and landmarks as written (two at most in a well-formed region).</summary>
    public ImmutableArray<Breakpoint> Bands { get; init; } = [];

    /// <summary>Hyphens written between bands (<c>6q21-q25.1</c>).</summary>
    public ImmutableArray<TextSpan> BandHyphens { get; init; } = [];

    /// <summary>Other readings joined by <c>or</c> (<c>Xq28 or Yq12</c>, §14.2 E46): bands only.</summary>
    public ImmutableArray<ArrRegion> Alternatives { get; init; } = [];

    /// <summary>The nucleotide positions; <c>null</c> when none are written.</summary>
    public ArrPositions? Positions { get; init; }

    /// <summary>The span of the bands of this reading (without the positions).</summary>
    public TextSpan BandSpan { get; init; }

    /// <summary>The first band.</summary>
    public Breakpoint? First => Bands.IsEmpty ? null : Bands[0];

    /// <summary>The last band when two or more are written.</summary>
    public Breakpoint? Last => Bands.Length > 1 ? Bands[^1] : null;

    public override IEnumerable<MeaningNode> Children() => Join(Bands, Alternatives, [Positions]);
}

/// <summary>Nucleotide positions in parentheses.</summary>
public sealed record ArrPositions : MeaningNode
{
    /// <summary>The positions or spans, in input order (<c>or</c> readings are on the item).</summary>
    public ImmutableArray<ArrPositionItem> Items { get; init; } = [];

    /// <summary>The detailed system: a copy number per item.</summary>
    public bool IsDetailed => Items.Any(i => i.Copy is not null);

    /// <summary>The span of the closing parenthesis (zero-width when missing).</summary>
    public TextSpan CloseParen { get; init; }

    public override IEnumerable<MeaningNode> Children() => Items;
}

/// <summary>A position or a span of positions (<c>163146681_183022312</c>), with its copy number in the detailed system.</summary>
public sealed record ArrPositionItem : MeaningNode
{
    /// <summary>The (first) position; <c>null</c> when it does not parse.</summary>
    public long? Start { get; init; }

    /// <summary>The last position of a span.</summary>
    public long? End { get; init; }

    /// <summary>The span of the start number as written (thousands separators included).</summary>
    public TextSpan StartSpan { get; init; }

    /// <summary>The span of the end number as written.</summary>
    public TextSpan? EndSpan { get; init; }

    /// <summary>The sign joining a span, as written: <c>_</c>, or a hyphen/en dash (ARR.NUCLEOTIDE_FORMAT).</summary>
    public string? Joiner { get; init; }

    /// <summary>The span of the joining sign.</summary>
    public TextSpan? JoinerSpan { get; init; }

    /// <summary>Commas written inside the numbers as thousands separators.</summary>
    public ImmutableArray<TextSpan> GroupSeparators { get; init; } = [];

    /// <summary>The copy number of this item (detailed system).</summary>
    public ArrCopyNumber? Copy { get; init; }

    /// <summary>Other readings joined by <c>or</c> (§14.2 E46).</summary>
    public ImmutableArray<ArrPositionItem> Alternatives { get; init; } = [];

    public bool IsSpan => Joiner is not null;

    public override IEnumerable<MeaningNode> Children() => Join([Copy], Alternatives);
}

/// <summary>The copy number (<c>×1</c>, <c>×2~4</c>) or the word in its place (<c>amp</c>, <c>cx</c>, <c>cth</c>).</summary>
public sealed record ArrCopyNumber : MeaningNode
{
    public ArrResultKind Kind { get; init; }

    /// <summary>The copy number, or the low end of a range.</summary>
    public int? Min { get; init; }

    /// <summary>The high end of a range (<c>4</c> in <c>×2~4</c>); equal to <see cref="Min"/> otherwise.</summary>
    public int? Max { get; init; }

    /// <summary>The range sign as written (<c>~</c>, or a hyphen for ARR.COPY_NUMBER); <c>null</c> for a single number.</summary>
    public string? RangeSign { get; init; }

    /// <summary>The span of the range sign.</summary>
    public TextSpan? RangeSignSpan { get; init; }

    public bool IsRange => RangeSign is not null;
}

/// <summary>The proportion of cells in square brackets (<c>[0.6]</c>, §14.2 E2).</summary>
public sealed record ArrProportion : MeaningNode
{
    /// <summary>The text inside the brackets.</summary>
    public required string Text { get; init; }

    /// <summary>A decimal fraction between 0 and 1 (<c>0.6</c>, <c>.15</c>).</summary>
    public bool IsFraction { get; init; }

    /// <summary>Written before the copy number (<c>(X)[0.6]×1</c>).</summary>
    public bool BeforeCopyNumber { get; init; }
}

/// <summary>A word after the result: <c>mos</c>, <c>htz</c>/<c>hmz</c>, <c>mat</c>/<c>pat</c>/<c>inh</c>/<c>dn</c>, <c>c</c>.</summary>
public sealed record ArrQualifier : MeaningNode
{
    public required string Word { get; init; }

    /// <summary>Whitespace separates it from the text before it.</summary>
    public bool SpaceBefore { get; init; }

    /// <summary>The text before it is a closing parenthesis (a detailed description, or a group with <c>cx</c>…).</summary>
    public bool AfterParenthesis { get; init; }

    /// <summary>The whitespace between it and the text before it (empty span when none).</summary>
    public TextSpan Gap { get; init; }

    /// <summary>The parental origin: <c>mat</c>, <c>pat</c>, <c>inh</c>, <c>dn</c>.</summary>
    public bool IsInheritance => Word is "mat" or "pat" or "inh" or "dn";

    /// <summary>The zygosity: <c>htz</c>, <c>hmz</c>.</summary>
    public bool IsZygosity => Word is "htz" or "hmz";
}
