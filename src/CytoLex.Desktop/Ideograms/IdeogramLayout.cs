using System.Collections.Immutable;
using CytoLex.Core.Kb;

namespace CytoLex.Desktop.Ideograms;

/// <summary>One band drawn on an ideogram: its place along the chromosome (0 = pter, 1 = qter) and its stain.</summary>
/// <param name="Name">The band name (<c>1p36.33</c>).</param>
/// <param name="From">The start as a fraction of the chromosome length.</param>
/// <param name="To">The end as a fraction of the chromosome length.</param>
/// <param name="Stain">The Giemsa stain (<c>gneg</c>, <c>gpos50</c>, <c>acen</c>, <c>gvar</c>, <c>stalk</c>).</param>
/// <param name="Arm"><c>p</c> or <c>q</c>.</param>
public sealed record IdeogramBand(string Name, double From, double To, string? Stain, string Arm);

/// <summary>A marked interval on an ideogram: fractions of the chromosome length and the lane beside the chromosome.</summary>
/// <param name="From">The start fraction.</param>
/// <param name="To">The end fraction (equal to <paramref name="From"/> for a point).</param>
/// <param name="Lane">The lane (0 = next to the chromosome); overlapping intervals get different lanes.</param>
public sealed record IdeogramSpan(double From, double To, int Lane);

/// <summary>
/// The geometry of the Map view (T-101), without drawing: 850-level bands from the edition's band data placed by their
/// coordinates in one build, chromosome lengths relative to the longest, and marker lanes for overlapping loci.
/// </summary>
public static class IdeogramLayout
{
    /// <summary>The band level drawn.</summary>
    public const int Level = 850;

    /// <summary>The smallest drawn length of a marker, as a fraction: a break or a short region stays visible.</summary>
    public const double MinMarker = 0.006;

    /// <summary>The 850-level bands of <paramref name="chromosome"/> with coordinates in <paramref name="build"/>, pter → qter.</summary>
    public static ImmutableArray<IdeogramBand> Bands(BandSet bands, string chromosome, ReferenceBuild build)
    {
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.ChromosomeRange(chromosome, build) is not { } whole)
        {
            return [];
        }

        // The finest bands at the level: where the book keeps a name NCBI splits (6p24 → 6p24.1–3, exceptions.yaml),
        // both are at 850 and the sub-bands are drawn.
        var atLevel = bands.OfChromosome(chromosome).Where(b => b.Levels.Contains(Level) && b.In(build) is not null).ToList();
        var parents = atLevel.Select(b => b.Parent).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return
        [
            .. atLevel
                .Where(b => !parents.Contains(b.Name))
                .OrderBy(b => b.In(build)!.Start)
                .Select(b => new IdeogramBand(b.Name, Fraction(b.In(build)!.Start - 1, whole), Fraction(b.In(build)!.End, whole), b.Stain, b.Arm)),
        ];
    }

    /// <summary>The length of <paramref name="chromosome"/> relative to the longest chromosome of the data (chromosome 1 = 1).</summary>
    public static double RelativeLength(BandSet bands, string chromosome, ReferenceBuild build)
    {
        ArgumentNullException.ThrowIfNull(bands);
        var longest = bands.Chromosomes.Select(c => bands.ChromosomeRange(c, build)?.End ?? 0).DefaultIfEmpty(0).Max();
        return longest == 0 || bands.ChromosomeRange(chromosome, build) is not { } r ? 0 : (double)r.End / longest;
    }

    /// <summary>Where the centromere is: the boundary of the p and q arms, as a fraction; <c>null</c> without bands.</summary>
    public static double? Centromere(ImmutableArray<IdeogramBand> bands)
    {
        var p = bands.Where(b => b.Arm == "p").Select(b => b.To).DefaultIfEmpty(-1).Max();
        return p < 0 ? null : p;
    }

    /// <summary>
    /// <paramref name="range"/> on a chromosome of extent <paramref name="whole"/> as fractions, at least
    /// <see cref="MinMarker"/> long; <c>null</c> without a range.
    /// </summary>
    public static (double From, double To)? Place(GenomicRange? range, GenomicRange whole)
    {
        ArgumentNullException.ThrowIfNull(whole);
        if (range is null)
        {
            return null;
        }

        var from = Math.Clamp(Fraction(range.Start - 1, whole), 0, 1);
        var to = Math.Clamp(Fraction(range.End, whole), 0, 1);
        if (to - from < MinMarker)
        {
            var mid = (from + to) / 2;
            from = Math.Max(0, mid - (MinMarker / 2));
            to = Math.Min(1, from + MinMarker);
        }

        return (from, to);
    }

    /// <summary>Lanes for intervals in input order: each gets the lowest lane where it overlaps no interval placed before it.</summary>
    public static ImmutableArray<int> Lanes(IReadOnlyList<(double From, double To)> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        var placed = new List<(double From, double To, int Lane)>();
        var lanes = ImmutableArray.CreateBuilder<int>(intervals.Count);
        foreach (var (from, to) in intervals)
        {
            var lane = 0;
            while (placed.Any(p => p.Lane == lane && p.From < to && from < p.To))
            {
                lane++;
            }

            placed.Add((from, to, lane));
            lanes.Add(lane);
        }

        return lanes.MoveToImmutable();
    }

    /// <summary>The index of the interval at <paramref name="fraction"/> (within <paramref name="tolerance"/>) in <paramref name="lane"/>, the shortest first; −1 when none.</summary>
    public static int HitTest(IReadOnlyList<IdeogramSpan> spans, double fraction, int lane, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(spans);
        var best = -1;
        for (var i = 0; i < spans.Count; i++)
        {
            var s = spans[i];
            if (s.Lane == lane && fraction >= s.From - tolerance && fraction <= s.To + tolerance
                && (best < 0 || s.To - s.From < spans[best].To - spans[best].From))
            {
                best = i;
            }
        }

        return best;
    }

    private static double Fraction(long position, GenomicRange whole) => (double)(position - (whole.Start - 1)) / (whole.End - (whole.Start - 1));
}
