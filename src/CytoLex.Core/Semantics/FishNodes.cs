using System.Collections.Immutable;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Text;

namespace CytoLex.Core.Semantics;

/// <summary>
/// The meaning of one in situ segment (ISCN 2016 ch. 13, T-033): <c>ish</c>, <c>nuc ish</c>, <c>fib ish</c>, <c>rev ish</c>
/// or <c>ish cgh</c>/<c>cgh</c> (<see cref="Type"/>), with its cell lines in input order.
/// </summary>
public sealed record FishModel : SegmentModel
{
    /// <summary>Which in situ notation the segment is.</summary>
    public required NotationType Type { get; init; }

    /// <summary>The keyword words (<c>ish</c>, <c>nuc ish</c>, …).</summary>
    public required TextSpan Keyword { get; init; }

    /// <summary>Whitespace follows the keyword (before the first result).</summary>
    public bool KeywordFollowedBySpace { get; init; }

    /// <summary>The segment starts with <c>//</c> (donor cells only after a transplant, §13.3.1 E19).</summary>
    public bool StartsWithDoubleSlash { get; init; }

    /// <summary><c>mos</c>/<c>chi</c> after the keyword.</summary>
    public ImmutableArray<Prefix> Prefixes { get; init; } = [];

    /// <summary>The cell lines, in input order.</summary>
    public ImmutableArray<FishCellLine> CellLines { get; init; } = [];

    /// <summary>Text the parser skipped.</summary>
    public ImmutableArray<UnboundItem> Unbound { get; init; } = [];

    /// <summary>How the cell lines relate: <c>//</c> → transplant, else the <c>mos</c>/<c>chi</c> prefix.</summary>
    public CellLineRelation Relation { get; init; }

    /// <summary>Every locus group of the segment, in input order.</summary>
    public IEnumerable<LocusGroup> LocusGroups => CellLines.SelectMany(l => l.Items).SelectMany(i => i.LocusGroups);

    public override IEnumerable<MeaningNode> Children() => Join(Prefixes, CellLines, Unbound);
}

/// <summary>One cell line of an in situ result: its comma-separated results and, at its end, the cells.</summary>
public sealed record FishCellLine : MeaningNode
{
    /// <summary>What separates the line from the one before it.</summary>
    public CellLineSeparator Separator { get; init; }

    /// <summary>The comma-separated results.</summary>
    public ImmutableArray<FishItem> Items { get; init; } = [];

    /// <summary>The cells after the line's last result (<c>[20]</c>), if any.</summary>
    public FishCells? Cells => Items.IsEmpty ? null : Items[^1].Cells;

    public override IEnumerable<MeaningNode> Children() => Items;
}

/// <summary>Which kind of parenthesis group follows an ish abnormality's symbols.</summary>
public enum FishGroupKind
{
    /// <summary>The chromosomes (<c>(22)</c>, <c>(14;21)</c>).</summary>
    Chromosomes,

    /// <summary>The breakpoints (<c>(q11.2q11.2)</c>).</summary>
    Breakpoints,

    /// <summary>The loci (<c>(D22S75–)</c>).</summary>
    Loci,
}

/// <summary>One parenthesis group of an ish abnormality, in input order: which part it belongs to and what it holds.</summary>
/// <param name="Part">The index of the abnormality part (<c>der</c>, <c>t</c>, …) it follows; parts are counted from 0.</param>
/// <param name="Kind">What it holds.</param>
/// <param name="Span">Its span, parentheses included.</param>
/// <param name="Loci">The locus group, for <see cref="FishGroupKind.Loci"/>.</param>
public sealed record FishGroup(int Part, FishGroupKind Kind, TextSpan Span, LocusGroup? Loci);

/// <summary>
/// One comma-separated in situ result: an abnormality with its locus groups (ish, rev ish, cgh) or a <see cref="FishRegion"/>
/// (a chromosome or band with loci; a nuc ish hybridization), and its cells.
/// </summary>
public sealed record FishItem : MeaningNode
{
    /// <summary>The abnormality (bound like a karyotype item; its locus groups are in <see cref="Groups"/>).</summary>
    public KaryotypeItem? Abnormality { get; init; }

    /// <summary>The region result, when the item is not an abnormality.</summary>
    public FishRegion? Region { get; init; }

    /// <summary>The abnormality's parenthesis groups in input order (empty for a region).</summary>
    public ImmutableArray<FishGroup> Groups { get; init; } = [];

    /// <summary>A status sign written after the locus group's closing parenthesis (<c>(D22S75)–</c>).</summary>
    public TextSpan? StraySign { get; init; }

    /// <summary>The cells (<c>[20]</c>, <c>[100/400]</c>).</summary>
    public FishCells? Cells { get; init; }

    /// <summary>The item's locus groups, in input order.</summary>
    public IEnumerable<LocusGroup> LocusGroups =>
        Region is not null ? Region.Groups : Groups.Select(g => g.Loci).OfType<LocusGroup>();

    public override IEnumerable<MeaningNode> Children() => Join([Abnormality, Region], Region is null ? LocusGroups : [], [Cells]);
}

/// <summary>
/// Locus groups led by a chromosome or band, or by nothing: <c>22q11.2(D22S75×2)</c>, <c>X(DXZ1×2)</c>,
/// <c>4p16.3(D4F26,D4S96)×2</c>, <c>15q11.2(D15S11+,…)</c>, <c>(ABL1,BCR)×2(ABL1 con BCR×1)</c>, <c>15q</c>.
/// </summary>
public sealed record FishRegion : MeaningNode
{
    /// <summary>The chromosome written before the loci (<c>X</c>, or the band's chromosome).</summary>
    public ChromosomeRef? Chromosome { get; init; }

    /// <summary>The band (<c>22q11.2</c>), if written.</summary>
    public Breakpoint? Band { get; init; }

    /// <summary>The band is in parentheses (<c>(22q11.2)(D22S75×2)</c>; ISH.NORMAL_CHROMOSOME_FORMAT).</summary>
    public bool BandInParentheses { get; init; }

    /// <summary>The locus groups, each with its shared count and qualifiers.</summary>
    public ImmutableArray<LocusGroup> Groups { get; init; } = [];

    public override IEnumerable<MeaningNode> Children() => Join([Chromosome, Band], Groups);
}

/// <summary>What separates two loci of a group.</summary>
public enum LocusSeparator
{
    /// <summary>Nothing (two loci written one after the other).</summary>
    None,

    /// <summary><c>,</c>: loci on one chromosome.</summary>
    Comma,

    /// <summary><c>;</c>: loci on different chromosomes.</summary>
    Semicolon,

    /// <summary><c>/</c>: loci of one contig.</summary>
    Slash,

    /// <summary><c>con</c>: connected (juxtaposed) signals.</summary>
    Con,

    /// <summary><c>sep</c>: separated signals.</summary>
    Sep,
}

/// <summary>Loci in parentheses (<c>(D22S75–)</c>, <c>(ABL1,BCR)</c>, <c>(ABL1 con BCR×1)</c>) and what follows the group.</summary>
public sealed record LocusGroup : MeaningNode
{
    /// <summary>The loci, in input order.</summary>
    public ImmutableArray<Locus> Loci { get; init; } = [];

    /// <summary>The separator after each locus but the last (<see cref="LocusSeparator.None"/> when none is written).</summary>
    public ImmutableArray<LocusSeparator> Separators { get; init; } = [];

    /// <summary>The spans of the separators (zero-width at the gap when none is written).</summary>
    public ImmutableArray<TextSpan> SeparatorSpans { get; init; } = [];

    /// <summary>A signal position group: its loci are joined by <c>con</c>/<c>sep</c> (§13.3.2).</summary>
    public bool IsPosition => Separators.Any(s => s is LocusSeparator.Con or LocusSeparator.Sep);

    /// <summary>The count written after the closing parenthesis, shared by the loci (<c>(A,B)×3</c>).</summary>
    public LocusCount? SharedCount { get; init; }

    /// <summary>Whitespace before the shared count (<c>(A,B) ×3</c>).</summary>
    public bool SpaceBeforeSharedCount { get; init; }

    /// <summary>Qualifiers written after the closing parenthesis (<c>(PAX7 con FOXO1)amp</c>).</summary>
    public ImmutableArray<LocusQualifier> Qualifiers { get; init; } = [];

    /// <summary>Breakpoints written inside the parentheses before the loci (<c>(q11.2q11.2D22S75–)</c>).</summary>
    public ImmutableArray<Breakpoint> Breakpoints { get; init; } = [];

    /// <summary>Whitespace before the opening parenthesis.</summary>
    public bool SpaceBefore { get; init; }

    /// <summary>The span of the opening parenthesis.</summary>
    public TextSpan Open { get; init; }

    /// <summary>The span of the closing parenthesis (zero-width when missing).</summary>
    public TextSpan Close { get; init; }

    public override IEnumerable<MeaningNode> Children() => Join(Breakpoints, Loci, [SharedCount]);
}

/// <summary><c>amp</c>, <c>dim</c> or <c>enh</c> after a locus or a group.</summary>
/// <param name="Word">The word.</param>
/// <param name="Span">Its span.</param>
/// <param name="SpaceBefore">Whitespace separates it from what precedes it.</param>
public sealed record LocusQualifier(string Word, TextSpan Span, bool SpaceBefore);

/// <summary>One locus: its name, status (<c>+</c>, <c>–</c>, <c>++</c>), signal count and qualifiers.</summary>
public sealed record Locus : MeaningNode
{
    /// <summary>The name as written (<c>D22S75</c>, <c>5′CBFB</c>, <c>wcp11</c>, <c>q34</c> for a subtelomeric band).</summary>
    public required string Name { get; init; }

    /// <summary>The span of the name.</summary>
    public required TextSpan NameSpan { get; init; }

    /// <summary>Signs written before the name (<c>–D22S75</c>; ISH.LOCUS_STATUS).</summary>
    public string LeadingSigns { get; init; } = "";

    /// <summary>The span of the leading signs.</summary>
    public TextSpan? LeadingSignsSpan { get; init; }

    /// <summary>The status signs after the name (<c>+</c>, <c>–</c>, <c>++</c>), empty when none.</summary>
    public string Status { get; init; } = "";

    /// <summary>The span of the status signs.</summary>
    public TextSpan? StatusSpan { get; init; }

    /// <summary>The signal count (<c>×2</c>).</summary>
    public LocusCount? Count { get; init; }

    /// <summary>The qualifiers (<c>amp</c>, <c>dim</c>, <c>enh</c>).</summary>
    public ImmutableArray<LocusQualifier> Qualifiers { get; init; } = [];

    /// <summary>The name is a band (<c>q34</c>, <c>p13</c>: subtelomeric results, §13.2.2).</summary>
    public bool IsBand => Name.Length > 1 && Name[0] is 'p' or 'q' && Name.Skip(1).All(c => char.IsAsciiDigit(c) || c == '.');

    /// <summary>The chromosome a paint names (<c>wcp11</c> → 11, <c>pcp18p11.2</c> → 18), or null.</summary>
    public string? PaintedChromosome
    {
        get
        {
            if (!Name.StartsWith("wcp", StringComparison.Ordinal) && !Name.StartsWith("pcp", StringComparison.Ordinal))
            {
                return null;
            }

            var rest = Name[3..].TrimStart();
            var n = rest.TakeWhile(c => char.IsAsciiDigit(c) || c is 'X' or 'Y').ToArray();
            return n.Length == 0 ? null : new string(n);
        }
    }

    public override IEnumerable<MeaningNode> Children() => Join([Count]);
}

/// <summary>A signal count: <c>×2</c>, <c>×0</c>, <c>×4~5</c>, <c>×12~&gt;50</c>.</summary>
public sealed record LocusCount : MeaningNode
{
    /// <summary>The count, or the low end of a range.</summary>
    public int? Low { get; init; }

    /// <summary>The high end of a range.</summary>
    public int? High { get; init; }

    /// <summary>The range is open-ended (<c>~&gt;50</c>).</summary>
    public bool IsOpenEnded { get; init; }

    /// <summary>The count as written, without the multiplication sign.</summary>
    public required string Text { get; init; }
}

/// <summary>The cells of an in situ result in square brackets: <c>[20]</c>, <c>[100/400]</c>, <c>[60%]</c>.</summary>
public sealed record FishCells : MeaningNode
{
    /// <summary>The number of cells (the abnormal ones in <c>[100/400]</c>).</summary>
    public int? Count { get; init; }

    /// <summary>The number of cells scored (<c>400</c> in <c>[100/400]</c>).</summary>
    public int? Total { get; init; }

    /// <summary>Written as a percentage (<c>[60%]</c>; ISH.CELL_LINES).</summary>
    public bool IsPercentage { get; init; }

    /// <summary>The text inside the brackets.</summary>
    public required string Text { get; init; }
}
