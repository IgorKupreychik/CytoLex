using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>What the string says about a locus (T-101). Only what the symbol or result itself states; no LGF (T-050).</summary>
public enum LocusEffect
{
    /// <summary>A gained whole chromosome (<c>+8</c>) or a duplicated segment (<c>dup</c>, <c>trp</c>, <c>qdp</c>).</summary>
    Gain,

    /// <summary>A lost whole chromosome (<c>–7</c>) or a deleted segment (<c>del</c>).</summary>
    Loss,

    /// <summary>A breakpoint of any other structural rearrangement (<c>t</c>, <c>inv</c>, <c>der</c>, <c>i</c>, <c>add</c>, …).</summary>
    Break,

    /// <summary>A microarray copy number, as written (<c>×1</c>, <c>×3</c>, <c>amp</c>); not read as a gain or loss (T-072).</summary>
    CopyNumber,

    /// <summary>An in situ result or probe signal, as written (<c>D21Z1×3</c>, <c>HIRA–</c>).</summary>
    Signal,

    /// <summary>An in situ result marked normal (<see cref="InSituResultSummary.IsNormal"/>).</summary>
    Normal,

    /// <summary>A chromosome region with variant features (<c>16qh+</c>).</summary>
    Variant,
}

/// <summary>How precisely a locus is placed.</summary>
public enum LocusExtent
{
    /// <summary>A whole chromosome (<c>+8</c>, <c>wcp11</c>, <c>D21Z1</c>, <c>arr (8)×3</c>).</summary>
    Chromosome,

    /// <summary>A chromosome arm (<c>pcp1q</c>, an uncertain breakpoint <c>q?</c>).</summary>
    Arm,

    /// <summary>One band (<c>9q34</c>).</summary>
    Band,

    /// <summary>From one band to another (<c>5q13→5q33</c>, <c>q21~24</c>, a terminal deletion to <c>qter</c>).</summary>
    BandRange,

    /// <summary>Nucleotide positions written in an arr result (<see cref="LocusEntry.PositionsBuild"/>).</summary>
    Positions,

    /// <summary>No position known: a probe name the string doesn't place and the probe table has no row for.</summary>
    Unplaced,
}

/// <summary>Where the placement of a locus comes from.</summary>
public enum LocusSource
{
    /// <summary>The string itself: bands, positions, chromosome numbers in paints and D-numbers.</summary>
    Written,

    /// <summary>A probe table row (T-103): <see cref="LocusEntry.ProbeRow"/>, which may not be reviewed.</summary>
    ProbeTable,
}

/// <summary>
/// One locus of a string on the chromosomes (T-101): chromosome, band or band range, coordinates per build, effect,
/// cells and span. Built from <see cref="EditionResult.Abnormalities"/> in input order (docs/design/core.md §9).
/// </summary>
/// <param name="Index">Index in <see cref="EditionResult.Loci"/>.</param>
/// <param name="Abnormality">The entry it belongs to (an index into <see cref="EditionResult.Abnormalities"/>; for an <c>or</c> reading, the alternatives entry).</param>
/// <param name="CellLine">An index into <see cref="EditionResult.CellLines"/>.</param>
/// <param name="Segment">The segment (<see cref="Segmentation.Segment.Index"/>).</param>
/// <param name="Technique">Karyotype, arr or in situ.</param>
/// <param name="Span">What names the locus in the whole input: the breakpoint, the region, the probe, or the whole item.</param>
/// <param name="Text">That text as written.</param>
/// <param name="Chromosome">The chromosome; <c>null</c> when unplaced.</param>
/// <param name="Extent">How precisely it is placed.</param>
/// <param name="From">The first band or landmark (<c>5q13</c>, <c>pter</c>), or the arm (<c>q</c>) for <see cref="LocusExtent.Arm"/>.</param>
/// <param name="To">The last band or landmark of a range (<c>5q33</c>, <c>qter</c>); <c>null</c> otherwise.</param>
/// <param name="Grch37">GRCh37 coordinates: the written positions in that build, else from the band data; <c>null</c> when unknown.</param>
/// <param name="Grch38">GRCh38 coordinates, likewise.</param>
/// <param name="PositionsBuild">The build of written arr positions (<c>GRCh38</c>); <c>null</c> when the coordinates come from bands only.</param>
/// <param name="Effect">What the string says about it.</param>
/// <param name="Copy">Copies or signals as written: <c>×2</c> of <c>+8×2</c>, <c>×1</c>/<c>amp</c> of arr, <c>+</c>/<c>–</c>/<c>×3</c> of a probe.</param>
/// <param name="Probe">The probe or locus name of an in situ result (<c>D21Z1</c>); <c>null</c> otherwise.</param>
/// <param name="Cells">The cells as written (<c>[20]</c>, <c>[cp10]</c>, <c>[100/400]</c>); <c>null</c> when none.</param>
/// <param name="IsUncertain">A questioned or ranged breakpoint, or an uncertain item.</param>
/// <param name="IsAlternative">One reading of several joined by <c>or</c>.</param>
/// <param name="Source">Where the placement comes from.</param>
/// <param name="UnplacedReason">Why it is <see cref="LocusExtent.Unplaced"/>, or why it has no coordinates.</param>
/// <param name="Kb">The KB entries of the item, plus <c>ISH.LOCUS_DESIGNATION</c> for a probe placed by its name.</param>
/// <param name="ProbeRow">The probe table row that places the probe (<see cref="LocusSource.ProbeTable"/>); <c>null</c> otherwise.</param>
public sealed record LocusEntry(
    int Index,
    int Abnormality,
    int CellLine,
    int Segment,
    SegmentTechnique Technique,
    TextSpan Span,
    string Text,
    string? Chromosome,
    LocusExtent Extent,
    string? From,
    string? To,
    GenomicRange? Grch37,
    GenomicRange? Grch38,
    string? PositionsBuild,
    LocusEffect Effect,
    string? Copy,
    string? Probe,
    string? Cells,
    bool IsUncertain,
    bool IsAlternative,
    LocusSource Source,
    string? UnplacedReason,
    ImmutableArray<KbRef> Kb,
    ProbeRow? ProbeRow = null)
{
    /// <summary>Whether the placement comes from a probe table row nobody has reviewed yet.</summary>
    public bool IsUnreviewed => ProbeRow is { Reviewed: false };

    /// <summary>Whether the locus has a chromosome.</summary>
    public bool IsPlaced => Extent != LocusExtent.Unplaced;

    /// <summary>The coordinates in <paramref name="build"/>.</summary>
    public GenomicRange? In(ReferenceBuild build) => build == ReferenceBuild.Grch37 ? Grch37 : Grch38;
}
