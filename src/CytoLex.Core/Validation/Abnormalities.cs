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

    /// <summary>A microarray result (ch. 14, T-072): regions or a chromosome group with a copy number, in <see cref="AbnormalityEntry.Arr"/>.</summary>
    ArrResult,
}

/// <summary>The technique of a group in <see cref="EditionResult.CellLines"/> (T-072).</summary>
public enum SegmentTechnique
{
    /// <summary>A cell line of a karyotype segment.</summary>
    Karyotype,

    /// <summary>A microarray (<c>arr</c>) segment: one group per segment.</summary>
    Arr,
}

/// <summary>A nucleotide position or span of an arr region (T-072).</summary>
/// <param name="Span">The position(s) in the whole input.</param>
/// <param name="Start">The (first) position; <c>null</c> when it does not parse.</param>
/// <param name="End">The last position of a span; <c>null</c> for a single position.</param>
/// <param name="CopyText">The copy number of this item as written in the detailed system (<c>×1</c>); <c>null</c> otherwise.</param>
/// <param name="Alternatives">Other readings joined by <c>or</c>.</param>
public sealed record ArrPositionSummary(
    TextSpan Span,
    long? Start,
    long? End,
    string? CopyText,
    ImmutableArray<ArrPositionSummary> Alternatives);

/// <summary>A region of an arr result: the chromosome, its bands and positions (T-072).</summary>
/// <param name="Span">The region (bands and positions) in the whole input.</param>
/// <param name="Chromosome">The chromosome of the first band.</param>
/// <param name="Bands">The bands as written (<c>4q32.2q35.1</c>).</param>
/// <param name="Breakpoints">The first and last band.</param>
/// <param name="Positions">The nucleotide positions, in input order; empty when none are written.</param>
/// <param name="Alternatives">Other band readings joined by <c>or</c> (<c>Xq28 or Yq12</c>).</param>
public sealed record ArrRegionSummary(
    TextSpan Span,
    string? Chromosome,
    string Bands,
    ImmutableArray<BreakpointSummary> Breakpoints,
    ImmutableArray<ArrPositionSummary> Positions,
    ImmutableArray<ArrRegionSummary> Alternatives);

/// <summary>
/// The microarray details of an <see cref="AbnormalityKind.ArrResult"/> entry (T-072). The copy number is given as
/// written; whether it is a gain or a loss depends on the sex and ploidy and is not interpreted.
/// </summary>
/// <param name="Kind">A copy number, or the word in its place (<c>amp</c>, <c>cx</c>, <c>cth</c>).</param>
/// <param name="CopyMin">The copy number, or the low end of a range.</param>
/// <param name="CopyMax">The high end of a range; equal to <paramref name="CopyMin"/> otherwise.</param>
/// <param name="CopyText">The result as written (<c>×1</c>, <c>×2~4</c>, <c>amp</c>); in the detailed system the copy number of the one span; <c>null</c> when none.</param>
/// <param name="IsWholeChromosomes">A chromosome group (<c>(8)</c>, <c>(1–22,X)</c>) rather than regions.</param>
/// <param name="IsRegionGroup">Regions grouped in parentheses before one result (§14.2.1 E5).</param>
/// <param name="IsDetailed">The detailed system: a copy number per position (§14.1 ¶2).</param>
/// <param name="Regions">The regions; empty for a chromosome group.</param>
/// <param name="Proportion">The proportion of cells as written (<c>0.6</c>).</param>
/// <param name="Zygosity"><c>htz</c> or <c>hmz</c>.</param>
/// <param name="IsMosaic">The qualifier <c>mos</c>.</param>
/// <param name="Build">The genome build of the segment (<c>GRCh38</c>).</param>
public sealed record ArrResultSummary(
    ArrResultKind Kind,
    int? CopyMin,
    int? CopyMax,
    string? CopyText,
    bool IsWholeChromosomes,
    bool IsRegionGroup,
    bool IsDetailed,
    ImmutableArray<ArrRegionSummary> Regions,
    string? Proportion,
    string? Zygosity,
    bool IsMosaic,
    string? Build);

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
/// <param name="BasedOn">The index (in <see cref="EditionResult.CellLines"/>) of the clone this line refers to with <c>idem</c>/<c>sl</c>/<c>sdl</c> (T-032).</param>
/// <param name="Technique">A karyotype cell line, or a whole arr segment (T-072: count, sex and cells are then <c>null</c>).</param>
/// <param name="Build">The genome build of an arr segment (<c>GRCh38</c>).</param>
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
    int AbnormalityCount,
    int? BasedOn = null,
    SegmentTechnique Technique = SegmentTechnique.Karyotype,
    string? Build = null);

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
/// <param name="Detailed">
/// The abnormality in the detailed system (T-066): <c>der(9)(9pter→9q34::22q11.2→22qter)</c>, computed from the short
/// system or as written; <c>null</c> without a band composition (<see cref="Semantics.Abnormality.Composition"/>).
/// </param>
/// <param name="Arr">The microarray details of an <see cref="AbnormalityKind.ArrResult"/> (T-072); <c>null</c> otherwise.</param>
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
    ImmutableArray<KbRef> Kb,
    string? Detailed = null,
    ArrResultSummary? Arr = null);
