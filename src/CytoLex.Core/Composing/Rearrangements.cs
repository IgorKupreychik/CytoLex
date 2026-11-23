using System.Collections.Immutable;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Composing;

/// <summary>A breakpoint group of the short system as points: its chromosome and its bands (or <c>?</c>).</summary>
internal sealed record Group(ChromosomeId Chromosome, ImmutableArray<BandPoint> Points)
{
    /// <summary>An unknown partner: <c>?</c> as the chromosome or as the only point.</summary>
    public bool IsUnknown => Chromosome.IsUnknown || Points is [{ Kind: PointKind.Unknown }];

    public BandPoint First => Points[0];

    public BandPoint Last => Points[^1];
}

/// <summary>
/// The rearrangements of the short system as operations on a <see cref="WorkingChromosome"/> (T-066). Each method names
/// the KB rule whose statement defines it; all return <c>false</c> (leaving no defined composition) when a band can't
/// be placed.
/// </summary>
internal static class Rearrangements
{
    /// <summary><c>DEL.FORMAT</c>: one band → the segment distal to it is lost (<c>pter→q13:</c>); two → the segment between them (<c>pter→q13::q33→qter</c>).</summary>
    public static bool Delete(WorkingChromosome w, ImmutableArray<BandPoint> bands)
    {
        if (bands is [var band])
        {
            if (w.Split(band) is not { } cut)
            {
                return false;
            }

            w.RemoveSide(cut.Boundary, cut.DistalRight);
            if (cut.DistalRight)
            {
                w.Trailing = JoinKind.Break;
            }
            else
            {
                w.Leading = JoinKind.Break;
            }

            return true;
        }

        return bands is [var x, var y] && Between(w, x, y) is { } range && Do(() => w.RemoveBetween(range.From, range.To));
    }

    /// <summary><c>ADD.FORMAT</c>: the segment distal to the band is replaced by material of unknown origin (<c>?::p13.3→qter</c>).</summary>
    public static bool Add(WorkingChromosome w, BandPoint band)
    {
        if (w.Split(band) is not { } cut)
        {
            return false;
        }

        w.RemoveSide(cut.Boundary, cut.DistalRight);
        w.Insert(cut.DistalRight ? w.Count : 0, [WorkingChromosome.Single(PointKind.Unknown)]);
        return true;
    }

    /// <summary><c>INV.FORMAT</c>: the segment between the two bands is reversed.</summary>
    public static bool Invert(WorkingChromosome w, BandPoint x, BandPoint y) =>
        Between(w, x, y) is { } range && Do(() => w.Reverse(range.From, range.To));

    /// <summary>
    /// <c>DUP.FORMAT</c>, <c>TRP.FORMAT</c>, <c>QDP.FORMAT</c>: the segment between the bands, <paramref name="copies"/> times
    /// in tandem. Proximal band first = direct (every copy in the original orientation, <c>pter→q25::q22→qter</c>); distal
    /// first = inverted (the copies alternate, <c>pter→q25::q25→q22::q25→qter</c>).
    /// </summary>
    public static bool Multiply(WorkingChromosome w, BandPoint x, BandPoint y, int copies)
    {
        // dup(17)(p11.2p11.2): both breakpoints in one band leave no segment between them; the composition is not computed
        // (it used to throw, T-033).
        if (Between(w, x, y) is not { } range || range.From == range.To)
        {
            return false;
        }

        var direct = x.Position is { } px && y.Position is { } py && px.CompareDistance(py) is > 0 ? false : true;
        var pieces = w.Slice(range.From, range.To);
        var links = w.SliceLinks(range.From, range.To);
        var reversed = WorkingChromosome.Reversed(pieces, links);
        var at = range.To;
        var lastReversed = false;
        for (var copy = 1; copy < copies; copy++)
        {
            lastReversed = !direct && copy % 2 == 1;
            var (p, l) = lastReversed ? reversed : (pieces, links);
            w.InsertLinked(at, p, l, junctionBefore: true, junctionAfter: true);
            at += p.Count;
        }

        if (!lastReversed && at < w.Count)
        {
            // a copy in the original orientation continues into the rest of the chromosome
            w.SetJunction(at, false);
        }

        return true;
    }

    /// <summary>
    /// <c>INS.ORIENTATION</c>: <paramref name="segment"/> (its first band to be placed proximal) is inserted at
    /// <paramref name="site"/>; with <c>?</c> the inserted material is unknown.
    /// </summary>
    public static bool InsertAt(WorkingChromosome w, BandPoint site, Group segment)
    {
        if (w.Split(site) is not { } cut)
        {
            return false;
        }

        w.Insert(cut.Boundary, [Segment(segment, proximalLeft: cut.DistalRight)]);
        return true;
    }

    /// <summary><c>HSR.FORMAT</c>, <c>HSR.REPLACED_SEGMENT_PROXIMAL</c>: an hsr at the band (at a reunion when the band is one).</summary>
    public static bool InsertHsr(WorkingChromosome w, BandPoint band)
    {
        if (w.Split(band) is not { } cut)
        {
            return false;
        }

        w.Insert(cut.Boundary, [WorkingChromosome.Single(PointKind.Hsr)]);
        return true;
    }

    /// <summary><c>HSR.INTERFACE_TWO_CHROMOSOMES</c>: an hsr at the reunion of the two bands.</summary>
    public static bool InsertHsrAtInterface(WorkingChromosome w, BandPoint a, BandPoint b)
    {
        if (w.JunctionBetween(a, b) is not { } at)
        {
            return false;
        }

        w.Insert(at, [WorkingChromosome.Single(PointKind.Hsr)]);
        return true;
    }

    /// <summary>
    /// <c>TRANSLOC.FORMAT</c>: the segment distal to the receiving band (or between its two bands) is replaced by the
    /// donor's segment distal to its band (or between its bands, first band proximal). A donor that brings its own
    /// centromere (<paramref name="centricDonor"/>, <c>der(a;b)</c>) gives its proximal side instead.
    /// </summary>
    public static bool Receive(WorkingChromosome w, Group receiving, Group donor, bool centricDonor)
    {
        if (receiving.Points is [var band])
        {
            if (w.Split(band) is not { } cut)
            {
                return false;
            }

            w.RemoveSide(cut.Boundary, cut.DistalRight);
            if (DonorEnd(donor, centricDonor) is not { } piece)
            {
                return false;
            }

            // the piece starts at the donor's band: append it as it is, or prepend it reversed
            if (cut.DistalRight)
            {
                w.Insert(w.Count, [piece]);
            }
            else
            {
                w.Insert(0, [piece.Reversed()]);
            }

            return true;
        }

        if (receiving.Points is not [var x, var y] || donor.Points.Length != 2 && !donor.IsUnknown || Between(w, x, y) is not { } range)
        {
            return false;
        }

        var distalRight = w.Split(x)!.Value.DistalRight;
        range = Between(w, x, y)!.Value;
        w.RemoveBetween(range.From, range.To);
        w.Insert(range.From, [Segment(donor, proximalLeft: distalRight)]);
        return true;
    }

    /// <summary>The der(?) receiving from a donor: the donor's segment joined to the unknown chromosome (<c>DER.UNKNOWN_CENTROMERE</c>).</summary>
    public static bool ReceiveUnknown(WorkingChromosome w, Group donor)
    {
        if (DonorEnd(donor, centric: false) is not { } piece)
        {
            return false;
        }

        w.Insert(w.Count, [piece]);
        return true;
    }

    /// <summary>
    /// <c>ISO.FORMAT</c>, <c>DER.ISODERIVATIVE</c>: the side distal to the centromeric band, twice, joined there
    /// (<c>qter→q10::q10→qter</c>).
    /// </summary>
    public static WorkingChromosome? Mirror(WorkingChromosome w, BandPoint band) => Doubled(w, band, distal: true);

    /// <summary><c>DIC.FORMAT</c> (idic): the side with the centromere, twice, joined at the band (<c>pter→q12::q12→pter</c>).</summary>
    public static WorkingChromosome? Isodicentric(WorkingChromosome w, BandPoint band) => Doubled(w, band, distal: false);

    /// <summary>The part of a normal chromosome with its centromere, ending (<paramref name="endsAtBand"/>) or starting at the band.</summary>
    public static ComposedPiece? CentricSide(Group g, bool endsAtBand)
    {
        if (g.IsUnknown)
        {
            return WorkingChromosome.Single(PointKind.Unknown);
        }

        if (g.Points is not [var band, ..] || band.Position is not { } p || p.Arm == 0)
        {
            return null;
        }

        var start = WorkingChromosome.Range(band, BandPoint.ArmEnd(g.Chromosome, p.Arm > 0 ? "p" : "q"));
        return endsAtBand ? start.Reversed() : start;
    }

    /// <summary><c>FIS.FORMAT</c>: the arm distal to the centromeric band, with a break there (<c>pter→p10:</c>, <c>q10→qter</c> after <c>:</c>).</summary>
    public static bool Fission(WorkingChromosome w, BandPoint band)
    {
        if (w.Split(band) is not { } cut)
        {
            return false;
        }

        w.RemoveSide(cut.Boundary, !cut.DistalRight);
        if (cut.DistalRight)
        {
            w.Leading = JoinKind.Break;
        }
        else
        {
            w.Trailing = JoinKind.Break;
        }

        return true;
    }

    /// <summary>An arm of a whole-arm rearrangement (<c>TRANSLOC.WHOLE_ARM</c>): <c>p10</c> → pter→p10, <c>q10</c> → q10→qter, read from the centromere when <paramref name="fromCentromere"/>.</summary>
    public static ComposedPiece WholeArm(ChromosomeId c, BandPoint band, bool fromCentromere)
    {
        var piece = WorkingChromosome.Range(band, BandPoint.ArmEnd(c, band.Arm!));
        return fromCentromere ? piece : piece.Reversed();
    }

    /// <summary>A centromeric band <c>p10</c>/<c>q10</c>.</summary>
    public static bool IsCentromeric(BandPoint b) => b.Kind == PointKind.Band && b.Band == "10";

    /// <summary>The segment between two bands of a group, its first band at the proximal side; <c>?</c> for an unknown group.</summary>
    public static ComposedPiece Segment(Group g, bool proximalLeft)
    {
        if (g.IsUnknown || g.Points.Length != 2)
        {
            return WorkingChromosome.Single(PointKind.Unknown);
        }

        var piece = WorkingChromosome.Range(g.First, g.Last);
        return proximalLeft ? piece : piece.Reversed();
    }

    /// <summary>The donor's piece starting at its band: distal to it (to the arm end), or its centric side when <paramref name="centric"/>.</summary>
    private static ComposedPiece? DonorEnd(Group donor, bool centric)
    {
        if (donor.IsUnknown)
        {
            return WorkingChromosome.Single(PointKind.Unknown);
        }

        if (donor.Points is not [var band] || band.Position is not { } p || p.Arm == 0)
        {
            return null;
        }

        return centric
            ? CentricSide(donor, endsAtBand: false)
            : WorkingChromosome.Range(band, BandPoint.ArmEnd(donor.Chromosome, band.Arm!));
    }

    private static WorkingChromosome? Doubled(WorkingChromosome w, BandPoint band, bool distal)
    {
        if (w.Split(band) is not { } cut)
        {
            return null;
        }

        var keepRight = distal == cut.DistalRight;
        var (from, to) = keepRight ? (cut.Boundary, w.Count) : (0, cut.Boundary);
        var side = w.Slice(from, to);
        var links = w.SliceLinks(from, to);
        var (rev, revLinks) = WorkingChromosome.Reversed(side, links);

        // the band ends up in the middle: keepRight → rev(side) :: side, else side :: rev(side)
        var (first, firstLinks, second, secondLinks) = keepRight ? (rev, revLinks, side, links) : (side, links, rev, revLinks);
        return WorkingChromosome.Linked([.. first, .. second], [.. firstLinks, true, .. secondLinks]);
    }

    /// <summary>Splits at both bands and returns the boundaries around the segment between them.</summary>
    private static (int From, int To)? Between(WorkingChromosome w, BandPoint x, BandPoint y)
    {
        if (w.Split(x) is null || w.Split(y) is not { } b || w.Split(x) is not { } a)
        {
            return null;
        }

        return (Math.Min(a.Boundary, b.Boundary), Math.Max(a.Boundary, b.Boundary));
    }

    private static bool Do(Action action)
    {
        action();
        return true;
    }
}
