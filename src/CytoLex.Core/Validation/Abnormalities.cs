using System.Collections.Immutable;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>What an <see cref="AbnormalityEntry"/> is (docs/design/core.md §9, T-069).</summary>
public enum AbnormalityKind
{
    /// <summary>A gained whole chromosome: <c>+8</c>.</summary>
    Gain,

    /// <summary>A lost whole chromosome: <c>–7</c>.</summary>
    Loss,

    /// <summary>A symbol-led abnormality: <c>del</c>, <c>t</c>, <c>der</c>, <c>mar</c>, <c>dmin</c>, … (its sign is in <see cref="AbnormalityEntry.Sign"/>).</summary>
    Structural,

    /// <summary>A chromosome region with variant features: <c>16qh+</c>, <c>1p31</c>.</summary>
    Variant,

    /// <summary>Readings joined by <c>or</c> (§5.3), in <see cref="AbnormalityEntry.Readings"/>.</summary>
    Alternatives,

    /// <summary>A clone reference <c>idem</c>, <c>sl</c>, <c>sdl</c> (§11.1.4; resolved by T-032).</summary>
    CloneReference,

    /// <summary>Text that could not be given a meaning, or a whole chromosome without a sign.</summary>
    Unrecognised,
}

/// <summary>One cell line of a karyotype segment, for grouping the abnormality list.</summary>
/// <param name="Index">Index in <see cref="EditionResult.CellLines"/>.</param>
/// <param name="SegmentIndex">The segment (<see cref="Segmentation.Segment.Index"/>).</param>
/// <param name="Span">The cell line in the whole input.</param>
/// <param name="Text">The cell line as written.</param>
/// <param name="SeparatorBefore">What separates it from the previous cell line.</param>
/// <param name="Relation">How the cell lines of its karyotype relate (<c>mos</c>, <c>chi</c>, <c>//</c>).</param>
/// <param name="Count">The chromosome count as written (<c>46</c>, <c>45~48</c>); <c>null</c> when absent.</param>
/// <param name="Sex">The sex chromosome field as written (<c>XX</c>, <c>XXY?c</c>); <c>null</c> when absent.</param>
/// <param name="Cells">The cell count <c>[n]</c>.</param>
/// <param name="IsComposite"><c>[cpN]</c>.</param>
/// <param name="IsPartial">Built from recovered input.</param>
/// <param name="AbnormalityCount">The entries of <see cref="EditionResult.Abnormalities"/> in this cell line.</param>
public sealed record CellLineSummary(
    int Index,
    int SegmentIndex,
    TextSpan Span,
    string Text,
    CellLineSeparator SeparatorBefore,
    CellLineRelation Relation,
    string? Count,
    string? Sex,
    int? Cells,
    bool IsComposite,
    bool IsPartial,
    int AbnormalityCount);

/// <summary>A breakpoint of an abnormality.</summary>
/// <param name="Span">The breakpoint in the whole input.</param>
/// <param name="Text">The breakpoint with its chromosome: <c>5q13</c>, <c>9pter</c>, <c>3q2?3</c>, <c>?</c>.</param>
/// <param name="Chromosome">The chromosome (written or from the breakpoint group's position).</param>
/// <param name="Arm"><c>p</c> or <c>q</c>.</param>
/// <param name="Band">Region, band and sub-band digits (<c>13.1</c>).</param>
/// <param name="Landmark"><c>pter</c>, <c>qter</c>, <c>cen</c>, <c>ter</c>, <c>hsr</c>, <c>neo</c> or <c>?</c>.</param>
/// <param name="Name">The full band name when certain (<c>5q13.1</c>).</param>
/// <param name="IsQuestioned">A <c>?</c> in the band.</param>
/// <param name="RangeEnd">The other end of an interval <c>q21~24</c>.</param>
/// <param name="Alternatives">Other readings joined by <c>or</c>.</param>
public sealed record BreakpointSummary(
    TextSpan Span,
    string Text,
    string? Chromosome,
    string? Arm,
    string? Band,
    string? Landmark,
    string? Name,
    bool IsQuestioned,
    string? RangeEnd,
    ImmutableArray<string> Alternatives);

/// <summary>One symbol group of a derivative or recombinant: the <c>der(1)</c> itself, then each rearrangement.</summary>
/// <param name="Span">The part in the whole input.</param>
/// <param name="Text">The part as written.</param>
/// <param name="Symbol">The head symbol (<c>der</c>, <c>t</c>).</param>
/// <param name="Chromosomes">The chromosomes of the part.</param>
/// <param name="Breakpoints">The breakpoints of the part.</param>
/// <param name="ShortForm">E.g. <c>t(1;3)</c>, <c>del(5q)</c>.</param>
/// <param name="IsQuestioned">A <c>?</c> before the symbol.</param>
public sealed record AbnormalityPartSummary(
    TextSpan Span,
    string Text,
    string? Symbol,
    ImmutableArray<string> Chromosomes,
    ImmutableArray<BreakpointSummary> Breakpoints,
    string ShortForm,
    bool IsQuestioned);

/// <summary>The qualifiers of an abnormality.</summary>
/// <param name="IsPartial">Built from recovered or skipped input.</param>
/// <param name="IsQuestioned">A <c>?</c> before the item or one of its symbols.</param>
/// <param name="IsUncertain">A questioned, ranged (<c>~</c>) or alternative (<c>or</c>) breakpoint or chromosome.</param>
/// <param name="Constitutional">The constitutional mark <c>c</c>.</param>
/// <param name="ConstitutionalQuestioned"><c>?c</c> / <c>c?</c>.</param>
/// <param name="Inheritance"><c>mat</c>, <c>pat</c>, <c>inh</c> or <c>dn</c>.</param>
/// <param name="Copies">The <c>n</c> of <c>×n</c>.</param>
/// <param name="LeadingCount">A copy count before the symbol as written (<c>3</c> in <c>+3mar</c>, <c>12~20</c>).</param>
public sealed record AbnormalityFlags(
    bool IsPartial,
    bool IsQuestioned,
    bool IsUncertain,
    bool Constitutional,
    bool ConstitutionalQuestioned,
    string? Inheritance,
    int? Copies,
    string? LeadingCount);

/// <summary>
/// One abnormality of a karyotype segment, flattened from the semantic model (docs/design/core.md §9, T-069). Entries
/// are in input order; for batch export (T-038), the web service (T-044) and the translation (T-058).
/// </summary>
/// <param name="Index">Index in <see cref="EditionResult.Abnormalities"/> (−1 for an <c>or</c> reading).</param>
/// <param name="CellLine">Index in <see cref="EditionResult.CellLines"/>.</param>
/// <param name="Span">The item in the whole input.</param>
/// <param name="Text">The item as written.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Sign">A <c>+</c>/<c>–</c> before it.</param>
/// <param name="Symbol">The head symbol (<c>del</c>, <c>dic</c> for <c>psu dic</c>, <c>der</c>); <c>null</c> without one.</param>
/// <param name="SymbolText">All symbol words of the first part (<c>psu dic</c>).</param>
/// <param name="Chromosomes">The chromosomes, distinct, in written order (<c>2p</c> for <c>dup(2p)</c>).</param>
/// <param name="Breakpoints">All breakpoints, in written order.</param>
/// <param name="ShortForm"><c>t(9;22)</c>, <c>+8</c>, <c>del(5q)</c>, <c>+der(22)t(9;22)</c>.</param>
/// <param name="Parts">The parts of a derivative or recombinant (more than one symbol group); empty otherwise.</param>
/// <param name="Readings">The readings of an <see cref="AbnormalityKind.Alternatives"/> item.</param>
/// <param name="Flags">Qualifiers.</param>
/// <param name="Diagnostics">Indices into <see cref="EditionResult.Diagnostics"/> of the diagnostics inside <see cref="Span"/>.</param>
/// <param name="Kb">The KB entries that define it (symbol vocab items and format rules).</param>
public sealed record AbnormalityEntry(
    int Index,
    int CellLine,
    TextSpan Span,
    string Text,
    AbnormalityKind Kind,
    GainLoss Sign,
    string? Symbol,
    string? SymbolText,
    ImmutableArray<string> Chromosomes,
    ImmutableArray<BreakpointSummary> Breakpoints,
    string ShortForm,
    ImmutableArray<AbnormalityPartSummary> Parts,
    ImmutableArray<AbnormalityEntry> Readings,
    AbnormalityFlags Flags,
    ImmutableArray<int> Diagnostics,
    ImmutableArray<KbRef> Kb);
