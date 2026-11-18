using System.Collections.Immutable;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using BandInfo = CytoLex.Core.Kb.Band;

namespace CytoLex.Core.Semantics;

/// <summary>
/// A node of the semantic model (docs/design/core.md §5): what a piece of notation means, where it is, and which KB
/// entries define it. Built by the <see cref="Binder"/> from the syntax tree; immutable.
/// </summary>
public abstract record MeaningNode
{
    /// <summary>The text the node was built from, in the whole input (without surrounding whitespace).</summary>
    public required TextSpan Span { get; init; }

    /// <summary>The KB entries that define the node: rule IDs, vocab items, bands.</summary>
    public ImmutableArray<KbRef> Kb { get; init; } = [];

    /// <summary>The syntax errors inside the node: recovered or skipped input (§5 "partial nodes").</summary>
    public ImmutableArray<SyntaxDiagnostic> Issues { get; init; } = [];

    /// <summary>The syntax node or token the meaning was read from (for spacing and system-specific rules).</summary>
    public SyntaxElement? Syntax { get; init; }

    /// <summary>Built from recovered or skipped input: rules that need complete data skip it.</summary>
    public bool IsPartial => !Issues.IsEmpty;

    /// <summary>The direct child meaning nodes, in input order.</summary>
    public virtual IEnumerable<MeaningNode> Children() => [];

    /// <summary>This node and every node below it, depth first.</summary>
    public IEnumerable<MeaningNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children())
        {
            foreach (var n in child.DescendantsAndSelf())
            {
                yield return n;
            }
        }
    }

    private protected static IEnumerable<MeaningNode> Join(params IEnumerable<MeaningNode?>[] parts) =>
        parts.SelectMany(p => p).OfType<MeaningNode>();
}

/// <summary>How the cell lines of a karyotype relate (§4.1).</summary>
public enum CellLineRelation
{
    /// <summary>One cell line, or several without <c>mos</c>/<c>chi</c>/<c>//</c>.</summary>
    None,

    /// <summary><c>mos</c>: a mosaic.</summary>
    Mosaic,

    /// <summary><c>chi</c>: a chimera.</summary>
    Chimera,

    /// <summary><c>//</c>: a chimera after transplantation (recipient // donor).</summary>
    Transplant,
}

/// <summary>What separates a cell line from the one before it.</summary>
public enum CellLineSeparator
{
    /// <summary>The first cell line.</summary>
    None,

    /// <summary><c>/</c>.</summary>
    Slash,

    /// <summary><c>//</c> (transplant chimera).</summary>
    DoubleSlash,

    /// <summary>A comma, recovered as a separator (always partial).</summary>
    Comma,
}

/// <summary>A plus or minus sign before an item.</summary>
public enum GainLoss
{
    /// <summary>No sign.</summary>
    None,

    /// <summary><c>+</c>.</summary>
    Gain,

    /// <summary><c>–</c> (or a typed form).</summary>
    Loss,
}

/// <summary>A join in a band composition (detailed system, §4.3.2).</summary>
public enum JoinKind
{
    /// <summary><c>:</c> — a break.</summary>
    Break,

    /// <summary><c>::</c> — a break and reunion.</summary>
    Junction,
}

/// <summary>The meaning of one karyotype segment (or book fragment).</summary>
public sealed record KaryotypeModel : MeaningNode
{
    /// <summary>The syntax tree the model was bound from.</summary>
    public required SyntaxTree Tree { get; init; }

    /// <summary>Parsed with the fragment entry (count and sex chromosomes optional).</summary>
    public bool IsFragment { get; init; }

    /// <summary><c>mos</c>, <c>chi</c>, <c>end</c> before the cell lines, and misplaced ones after them.</summary>
    public ImmutableArray<Prefix> Prefixes { get; init; } = [];

    /// <summary>The cell lines, in input order.</summary>
    public ImmutableArray<CellLine> CellLines { get; init; } = [];

    /// <summary>Text between cell lines the parser skipped.</summary>
    public ImmutableArray<UnboundItem> Unbound { get; init; } = [];

    /// <summary>How the cell lines relate: <c>//</c> → transplant, else the <c>mos</c>/<c>chi</c> prefix.</summary>
    public CellLineRelation Relation { get; init; }

    /// <summary><c>end</c>: endoreduplication.</summary>
    public bool IsEndoreduplication { get; init; }

    public override IEnumerable<MeaningNode> Children() => Join(Prefixes, CellLines, Unbound);
}

/// <summary>A <c>mos</c>, <c>chi</c> or <c>end</c> prefix.</summary>
public sealed record Prefix : MeaningNode
{
    public required string Symbol { get; init; }

    /// <summary>Written after the karyotype instead of before it (recovered).</summary>
    public bool IsMisplaced { get; init; }
}

/// <summary>One cell line: count, ploidy, sex chromosomes, items and cell count.</summary>
public sealed record CellLine : MeaningNode
{
    public CellLineSeparator SeparatorBefore { get; init; }

    /// <summary>The chromosome count; <c>null</c> in a fragment without one.</summary>
    public NumberRange? Count { get; init; }

    public Ploidy? Ploidy { get; init; }

    /// <summary>The sex chromosome field; <c>null</c> when absent (e.g. <c>46,t(X;Y)(…)</c> or a fragment).</summary>
    public SexComplement? Sex { get; init; }

    /// <summary>The comma-separated items after the sex chromosomes.</summary>
    public ImmutableArray<KaryotypeItem> Items { get; init; } = [];

    public CellCount? CellCount { get; init; }

    public override IEnumerable<MeaningNode> Children() => Join([Count, Ploidy, Sex], Items, [CellCount]);
}

/// <summary>A number or range: chromosome count <c>46</c>, <c>45~48</c>, leading count <c>3</c>, <c>12~20</c>, <c>~14</c>.</summary>
public sealed record NumberRange : MeaningNode
{
    /// <summary>The (first) number; <c>null</c> for <c>~14</c> or a missing count.</summary>
    public int? From { get; init; }

    /// <summary>The number after <c>~</c>; <c>null</c> for a single number.</summary>
    public int? To { get; init; }

    /// <summary>Written with <c>~</c>.</summary>
    public bool IsRange { get; init; }

    /// <summary>Inserted by recovery (no count written).</summary>
    public bool IsMissing { get; init; }
}

/// <summary>A ploidy level <c>&lt;2n&gt;</c>, <c>&lt;4n±&gt;</c>.</summary>
public sealed record Ploidy : MeaningNode
{
    /// <summary>The text between the brackets, e.g. <c>2n</c>.</summary>
    public required string Text { get; init; }

    /// <summary>The number before <c>n</c>.</summary>
    public int? Level { get; init; }

    /// <summary>Written in angle brackets (false: recovered from parentheses).</summary>
    public bool InAngleBrackets { get; init; }
}

/// <summary>The sex chromosome field (<c>XXY</c>, <c>Xc</c>, <c>XXX?c</c>).</summary>
public sealed record SexComplement : MeaningNode
{
    /// <summary>One entry per letter, e.g. <c>X</c>, <c>X</c>, <c>Y</c>.</summary>
    public ImmutableArray<string> Chromosomes { get; init; } = [];

    public ConstitutionalMark? Constitutional { get; init; }

    public override IEnumerable<MeaningNode> Children() => Join([Constitutional]);
}

/// <summary>The constitutional mark <c>c</c> (<c>?c</c> or <c>c?</c> when uncertain).</summary>
public sealed record ConstitutionalMark : MeaningNode
{
    public bool IsQuestioned { get; init; }
}

/// <summary>A cell count <c>[20]</c> or <c>[cp10]</c>.</summary>
public sealed record CellCount : MeaningNode
{
    public int? Cells { get; init; }

    /// <summary><c>cp</c>: composite karyotype (§11.1.5; not implemented in the pilot).</summary>
    public bool IsComposite { get; init; }
}

/// <summary>One comma-separated item of a cell line.</summary>
public abstract record KaryotypeItem : MeaningNode;

/// <summary>The suffixes an item can carry: <c>×n</c>, <c>c</c>, <c>mat</c>/<c>pat</c>/<c>inh</c>/<c>dn</c>.</summary>
public abstract record SuffixedItem : KaryotypeItem
{
    public GainLoss Sign { get; init; }

    /// <summary>A <c>?</c> before the item (questionable identification, §5.1).</summary>
    public bool IsQuestioned { get; init; }

    /// <summary><c>×n</c>.</summary>
    public Copies? Copies { get; init; }

    public ConstitutionalMark? Constitutional { get; init; }

    /// <summary><c>mat</c>, <c>pat</c>, <c>inh</c> or <c>dn</c>.</summary>
    public Inheritance? Inheritance { get; init; }

    private protected IEnumerable<MeaningNode> SuffixNodes => Join([Copies, Constitutional, Inheritance]);
}

/// <summary>Copies of an abnormality: <c>×2</c>.</summary>
public sealed record Copies : MeaningNode
{
    public int? Count { get; init; }
}

/// <summary>Parental origin or de novo: <c>mat</c>, <c>pat</c>, <c>inh</c>, <c>dn</c>.</summary>
public sealed record Inheritance : MeaningNode
{
    public required string Symbol { get; init; }
}

/// <summary>A gained, lost or questioned whole chromosome: <c>+21</c>, <c>–X</c>, <c>–?21</c>, <c>+21c</c>.</summary>
public sealed record WholeChromosomeChange : SuffixedItem
{
    public required ChromosomeRef Chromosome { get; init; }

    public override IEnumerable<MeaningNode> Children() => Join([Chromosome], SuffixNodes);
}

/// <summary>
/// A symbol-led abnormality: <c>del(5)(q13q33)</c>, <c>der(1)t(1;3)(p32;q21)t(1;11)(q25;q13)</c>, <c>+3mar</c>,
/// <c>12~20dmin</c>, <c>psu dic(15;13)(q12;q12)</c>. A derivative (<c>der</c>, <c>rec</c>, <c>ider</c>) is one part
/// for the derivative itself followed by one part per rearrangement it results from.
/// </summary>
public sealed record Abnormality : SuffixedItem
{
    /// <summary>A copy count before the symbol: <c>3mar</c>, <c>12~20dmin</c>.</summary>
    public NumberRange? LeadingCount { get; init; }

    /// <summary>The symbol groups, in input order.</summary>
    public ImmutableArray<AbnormalityPart> Parts { get; init; } = [];

    /// <summary>The head symbol of the first part (<c>del</c>, <c>der</c>, <c>dic</c> for <c>psu dic</c>, <c>r</c> for <c>trc r</c>).</summary>
    public string? Kind => Parts.IsEmpty ? null : Parts[0].Head;

    /// <summary>All breakpoints of all parts, in input order.</summary>
    public IEnumerable<Breakpoint> Breakpoints => Parts.SelectMany(p => p.Breakpoints);

    public override IEnumerable<MeaningNode> Children() => Join([LeadingCount], Parts, SuffixNodes);
}

/// <summary>One symbol group of an abnormality: <c>[?]symbols[number](chromosomes)(breakpoints)</c>.</summary>
public sealed record AbnormalityPart : MeaningNode
{
    /// <summary>A <c>?</c> before the symbol (<c>der(1)?t(1;3)(…)</c>).</summary>
    public bool IsQuestioned { get; init; }

    /// <summary>The symbol words in written order (<c>psu</c>, <c>dic</c>).</summary>
    public ImmutableArray<SymbolRef> Symbols { get; init; } = [];

    /// <summary>The last symbol word: the kind of the part.</summary>
    public string? Head => Symbols.IsEmpty ? null : Symbols[^1].Symbol;

    /// <summary>A number after the symbol: <c>mar1</c>, <c>r2</c>.</summary>
    public int? SymbolNumber { get; init; }

    /// <summary>The chromosomes (in parentheses, or a bare one recovered from <c>del4(p14)</c>).</summary>
    public ImmutableArray<ChromosomeRef> Chromosomes { get; init; } = [];

    /// <summary>The chromosomes were written in parentheses.</summary>
    public bool HasChromosomeList { get; init; }

    /// <summary>The breakpoint groups (one per chromosome, separated by <c>;</c>); empty without a breakpoint list.</summary>
    public ImmutableArray<BreakpointGroup> Groups { get; init; } = [];

    /// <summary>The breakpoints were written in the detailed system (band composition with <c>→</c>, <c>:</c>, <c>::</c>).</summary>
    public bool IsDetailed => Groups.Any(g => g.Structure is not null);

    /// <summary>The breakpoints of every group, in input order. In the detailed system: the band ends at the joins, distinct.</summary>
    public IEnumerable<Breakpoint> Breakpoints => Groups.SelectMany(g => g.Breakpoints);

    public override IEnumerable<MeaningNode> Children() => Join(Symbols, Chromosomes, Groups);
}

/// <summary>A symbol word of an abnormality (with its vocab item and format rule as <see cref="MeaningNode.Kb"/>).</summary>
public sealed record SymbolRef : MeaningNode
{
    public required string Symbol { get; init; }

    /// <summary>The word is not a listed symbol (recovered; partial).</summary>
    public bool IsListed { get; init; }
}

/// <summary>A chromosome: <c>1</c>–<c>22</c>, <c>X</c>, <c>Y</c>, <c>?</c>; maybe with an arm (<c>dup(2p)</c>) or underlined.</summary>
public sealed record ChromosomeRef : MeaningNode
{
    /// <summary>The identifier without underline marks (<c>1</c> for <c>1̲</c>), or <c>?</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The autosome number, if numeric (not range-checked: that is <c>CHROM.IDENTIFIERS</c>'s job).</summary>
    public int? Number { get; init; }

    public bool IsSexChromosome => Name is "X" or "Y";

    /// <summary><c>?</c>: unknown chromosome.</summary>
    public bool IsUnknown => Name == "?";

    /// <summary>Written <c>?21</c> in a chromosome list.</summary>
    public bool IsQuestioned { get; init; }

    /// <summary>Written underlined (homologue distinction, T-042).</summary>
    public bool IsUnderlined { get; init; }

    /// <summary>An arm written with it (<c>2p</c> in <c>dup(2p)</c>).</summary>
    public string? Arm { get; init; }
}

/// <summary>The breakpoints of one chromosome (between semicolons), in the short or the detailed system.</summary>
public sealed record BreakpointGroup : MeaningNode
{
    /// <summary>The chromosome the group belongs to by position in the chromosome list; <c>null</c> when there is none.</summary>
    public ChromosomeRef? Chromosome { get; init; }

    /// <summary>The breakpoints (short system: as written; detailed system: band ends at the joins, distinct).</summary>
    public ImmutableArray<Breakpoint> Breakpoints { get; init; } = [];

    /// <summary>The band composition as written (detailed system); <c>null</c> for the short system.</summary>
    public DerivativeStructure? Structure { get; init; }

    public override IEnumerable<MeaningNode> Children() => Structure is null ? Breakpoints : [Structure];
}

/// <summary>
/// A breakpoint or a point of a band composition: a band (<c>q13</c>, <c>3q13.1</c>, <c>q2?3</c>, <c>q21~24</c>) or a
/// landmark (<c>pter</c>, <c>qter</c>, <c>cen</c>, <c>ter</c>, <c>hsr</c>, <c>neo</c>, <c>?</c>).
/// </summary>
public sealed record Breakpoint : MeaningNode
{
    /// <summary>The chromosome: written (<c>3q13</c>) or from the breakpoint group's position.</summary>
    public ChromosomeRef? Chromosome { get; init; }

    /// <summary>The chromosome was written with the band (<c>3q13</c>, <c>9pter</c>).</summary>
    public bool ChromosomeWritten { get; init; }

    /// <summary>The text as written, without the chromosome: <c>q13.1</c>, <c>q2?3</c>, <c>pter</c>, <c>?</c>.</summary>
    public required string Text { get; init; }

    /// <summary><c>p</c> or <c>q</c> (as written; <c>P</c>/<c>Q</c> are recovered).</summary>
    public string? Arm { get; init; }

    /// <summary>Region, band and sub-band digits (<c>13.1</c>); <c>null</c> without digits or when questioned.</summary>
    public string? BandNumber { get; init; }

    /// <summary><c>pter</c>, <c>qter</c>, <c>cen</c>, <c>ter</c>, <c>hsr</c>, <c>neo</c> or <c>?</c> (unknown).</summary>
    public string? Landmark { get; init; }

    /// <summary>A <c>?</c> in the band (<c>q2?3</c>, <c>q?</c>, <c>q?23</c>, §5.2).</summary>
    public bool IsQuestioned { get; init; }

    /// <summary>The other end of an interval <c>q21~24</c> (§5.2).</summary>
    public Breakpoint? RangeEnd { get; init; }

    /// <summary>Other readings joined by <c>or</c> (<c>p13 or q13</c>, §5.3).</summary>
    public ImmutableArray<Breakpoint> Alternatives { get; init; } = [];

    /// <summary>The band in the edition's band set (<c>band:</c> ref); <c>null</c> when unknown, a region only, p10/q10 or uncertain.</summary>
    public BandInfo? KbBand { get; init; }

    /// <summary>The full band name (<c>5q13.1</c>) when chromosome, arm and digits are known and certain.</summary>
    public string? Name => Chromosome is { IsUnknown: false } c && Arm is not null && BandNumber is not null ? c.Name + Arm + BandNumber : null;

    /// <summary>A terminal landmark (<c>pter</c>, <c>qter</c>, <c>ter</c>): the end of an arm, not a break.</summary>
    public bool IsTerminal => Landmark is "pter" or "qter" or "ter";

    public override IEnumerable<MeaningNode> Children() => Join([RangeEnd], Alternatives);
}

/// <summary>
/// A band composition as written in the detailed system (§4.3.2): pieces joined by <c>::</c> (break and reunion) or
/// ended by <c>:</c> (break); a ring starts and ends with <c>::</c>.
/// </summary>
public sealed record DerivativeStructure : MeaningNode
{
    /// <summary>A join before the first piece (<c>:p12→q31:</c>, rings <c>::p22→q36::</c>).</summary>
    public JoinKind? LeadingJoin { get; init; }

    public ImmutableArray<SegmentPiece> Pieces { get; init; } = [];

    /// <summary>The joins between consecutive pieces (one fewer than the pieces).</summary>
    public ImmutableArray<JoinKind> Joins { get; init; } = [];

    public JoinKind? TrailingJoin { get; init; }

    public override IEnumerable<MeaningNode> Children() => Pieces;
}

/// <summary>
/// One piece of a band composition: points joined by <c>→</c>, in the written orientation
/// (<c>3qter→3q21</c>, <c>q28→neo→q28</c>), or a single point (<c>hsr</c>, <c>?</c>).
/// </summary>
public sealed record SegmentPiece : MeaningNode
{
    public ImmutableArray<Breakpoint> Points { get; init; } = [];

    public Breakpoint From => Points[0];

    public Breakpoint To => Points[^1];

    public override IEnumerable<MeaningNode> Children() => Points;
}

/// <summary>A chromosome region with variant features or a band: <c>16qh+</c>, <c>14cenh+pstk+ps+</c>, <c>1p31</c>.</summary>
public sealed record ChromosomeRegion : SuffixedItem
{
    /// <summary>The chromosome; <c>null</c> for a band without one (<c>q24</c> in <c>1q23 or q24</c>).</summary>
    public ChromosomeRef? Chromosome { get; init; }

    /// <summary>Arms/bands, features and length signs, in written order.</summary>
    public ImmutableArray<RegionElement> Elements { get; init; } = [];

    public override IEnumerable<MeaningNode> Children() => Join([Chromosome], Elements, SuffixNodes);
}

/// <summary>What a <see cref="RegionElement"/> is.</summary>
public enum RegionElementKind
{
    /// <summary>An arm or band (<c>q</c>, <c>p31</c>).</summary>
    Band,

    /// <summary>A variant feature (<c>h</c>, <c>s</c>, <c>stk</c>, <c>var</c>, <c>ps</c>, <c>qs</c>) or <c>cen</c>.</summary>
    Feature,

    /// <summary><c>+</c>: increase in length.</summary>
    Increase,

    /// <summary><c>–</c>: decrease in length.</summary>
    Decrease,
}

/// <summary>One element of a <see cref="ChromosomeRegion"/>.</summary>
public sealed record RegionElement : MeaningNode
{
    public RegionElementKind Kind { get; init; }

    public required string Text { get; init; }

    /// <summary>The band, for <see cref="RegionElementKind.Band"/>.</summary>
    public Breakpoint? Band { get; init; }

    public override IEnumerable<MeaningNode> Children() => Join([Band]);
}

/// <summary>Two or more readings joined by <c>or</c> (§5.3).</summary>
public sealed record AlternativeItem : KaryotypeItem
{
    public ImmutableArray<KaryotypeItem> Readings { get; init; } = [];

    public override IEnumerable<MeaningNode> Children() => Readings;
}

/// <summary><c>…</c>: abnormalities left out (book fragments).</summary>
public sealed record OmittedItems : KaryotypeItem;

/// <summary>A band composition on its own, e.g. the book fragment <c>(pter→q22::q25→q22::q22→qter)</c>.</summary>
public sealed record BandComposition : KaryotypeItem
{
    public ImmutableArray<BreakpointGroup> Groups { get; init; } = [];

    public override IEnumerable<MeaningNode> Children() => Groups;
}

/// <summary>Text that could not be given a meaning (skipped or misplaced input); always partial.</summary>
public sealed record UnboundItem : KaryotypeItem
{
    public required string Text { get; init; }
}
