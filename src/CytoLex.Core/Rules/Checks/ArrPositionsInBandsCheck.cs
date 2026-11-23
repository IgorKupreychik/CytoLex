using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>ARR.POSITIONS_IN_BANDS</c> (§14.2 ¶3/¶5/¶6, decided in T-071, implemented in T-088): the abnormal span of each arr
/// region must lie in the bands of the build written after <c>arr</c> — the first position in the first band, the last
/// position in the last band (one band holds both), no position beyond the chromosome end; <c>pter</c>/<c>qter</c> are
/// the chromosome ends. In the detailed system only the span items count (the flanking normal positions may lie
/// outside); <c>or</c> readings are checked one by one; tolerance 0. Not checked: no build or a build without band
/// coordinates (anything but GRCh37/GRCh38), unknown, questioned or region-only bands, bands without coordinates for the
/// build, whole chromosomes or arms, recovered regions and reversed spans (<c>ARR.PTER_TO_QTER</c>). One finding per arr
/// segment naming every failure; when the positions fit the other build, the detail says so. Level-3 fix (KB hint).
/// </summary>
[KbRule("ARR.POSITIONS_IN_BANDS")]
internal sealed class ArrPositionsInBandsCheck : IRuleCheck
{
    internal const string Grch37 = "GRCh37";
    internal const string Grch38 = "GRCh38";

    public void Check(RuleContext context)
    {
        if (context.Arr is not { Build.Text: Grch37 or Grch38 } model || context.Kb.Bands is not { } bands)
        {
            return;
        }

        var build = model.Build!.Text;
        var other = build == Grch38 ? Grch37 : Grch38;
        var readings = Readings(model).ToList();
        var stated = ArrPositionFit.Evaluate(readings, bands, build);
        if (stated.Failures.IsEmpty)
        {
            return;
        }

        var detail = string.Join("; ", stated.Failures.Select(f => f.Text)) + ".";
        var alternative = ArrPositionFit.Evaluate(readings, bands, other);
        if (alternative.Failures.IsEmpty && alternative.Tested > 0)
        {
            detail += $" The positions fit the {other} bands.";
        }

        var spans = stated.Failures.Select(f => f.Span).Distinct().ToList();
        context.ReportWithFix(spans[0], detail, null, [.. spans.Skip(1)]);
    }

    /// <summary>Every reading of a region with positions: the main one and each <c>or</c> reading paired with its positions.</summary>
    private static IEnumerable<ArrReading> Readings(ArrModel model)
    {
        foreach (var region in model.Aberrations.Where(a => !a.IsWholeChromosomes).SelectMany(a => a.Regions))
        {
            if (region.IsPartial || region.Positions is not { IsPartial: false } positions)
            {
                continue;
            }

            var items = positions.IsDetailed ? positions.Items.Where(i => i.IsSpan).ToList() : [.. positions.Items];
            if (items.Count == 0)
            {
                continue;
            }

            yield return new ArrReading(region, items[0], items[^1]);
            if (items.Count == 1 && region.Alternatives.Length == items[0].Alternatives.Length)
            {
                for (var i = 0; i < region.Alternatives.Length; i++)
                {
                    var item = items[0].Alternatives[i];
                    yield return new ArrReading(region.Alternatives[i], item, item);
                }
            }
        }
    }
}

/// <summary>One reading of an arr region: its bands, and the items holding the first and last position of the abnormal span.</summary>
internal sealed record ArrReading(ArrRegion Region, ArrPositionItem First, ArrPositionItem Last);

/// <summary>A position that does not fit: the span of the number and what is wrong with it.</summary>
internal sealed record ArrPositionFailure(TextSpan Span, string Text);

/// <summary>The outcome for one build: the failures, and how many positions could be tested at all.</summary>
internal sealed record ArrPositionFitResult(ImmutableArray<ArrPositionFailure> Failures, int Tested);

/// <summary>Compares arr positions with the band coordinates of one build (<c>ARR.POSITIONS_IN_BANDS</c>, T-088).</summary>
internal static class ArrPositionFit
{
    private static readonly ConditionalWeakTable<BandSet, ChromosomeEnds> _ends = [];

    public static ArrPositionFitResult Evaluate(IReadOnlyList<ArrReading> readings, BandSet bands, string build)
    {
        var ends = _ends.GetValue(bands, b => new ChromosomeEnds(b));
        var failures = ImmutableArray.CreateBuilder<ArrPositionFailure>();
        var tested = 0;
        foreach (var reading in readings)
        {
            var start = reading.First.Start;
            var end = reading.Last.End ?? reading.Last.Start;
            if (reading.Region.Chromosome is not { IsUnknown: false } chromosome || start is not { } a || end is not { } b || a > b)
            {
                continue;
            }

            var length = ends.Of(chromosome.Name, build);
            var endSpan = reading.Last.EndSpan ?? reading.Last.StartSpan;
            var firstBand = reading.Region.First;
            var lastBand = reading.Region.Last ?? firstBand;
            tested += Test(a, reading.First.StartSpan, firstBand, isStart: true);
            tested += Test(b, endSpan, lastBand, isStart: false);

            int Test(long position, TextSpan span, Breakpoint? written, bool isStart)
            {
                if (length is { } l && position > l)
                {
                    failures.Add(new(span, $"{N(position)} lies beyond the end of chromosome {chromosome.Name} ({N(l)}) in {build}"));
                    return 1;
                }

                if (written is null)
                {
                    return 0;
                }

                if (written.Landmark is "pter" or "qter")
                {
                    // pter/qter = the chromosome ends: only the chromosome length can be tested (done above).
                    var fitsEnd = isStart ? written.Landmark == "pter" : written.Landmark == "qter";
                    return fitsEnd && length is not null ? 1 : 0;
                }

                if (written.IsQuestioned || written.BandNumber is null || written.Name is not { } name
                    || bands.Find(name) is not { } band || Range(band, build) is not { } range)
                {
                    return 0;
                }

                if (position < range.Start || position > range.End)
                {
                    var inside = ContainingBand(bands, band, position, build);
                    var where = inside is null ? "" : $", it lies in {inside.Name}";
                    failures.Add(new(span, $"{N(position)} is not in {name} in {build} ({N(range.Start)}_{N(range.End)}){where}"));
                }

                return 1;
            }
        }

        return new(failures.ToImmutable(), tested);
    }

    private static GenomicRange? Range(Band band, string build) => build == ArrPositionsInBandsCheck.Grch37 ? band.Grch37 : band.Grch38;

    /// <summary>
    /// The band that holds the position at the lowest band level the written band exists at (<c>17p12</c> → level 300 →
    /// <c>17p13</c>); for a written nesting ancestor without levels, a band of the same written form (digits and dots).
    /// </summary>
    private static Band? ContainingBand(BandSet bands, Band written, long position, string build)
    {
        var holding = bands.Bands.Where(b =>
            b.Chromosome == written.Chromosome && Range(b, build) is { } r && position >= r.Start && position <= r.End);
        if (!written.Levels.IsEmpty)
        {
            var level = written.Levels.Min();
            return holding.FirstOrDefault(b => b.Levels.Contains(level));
        }

        var shape = Shape(written);
        return holding.FirstOrDefault(b => Shape(b) == shape);
    }

    private static string Shape(Band band) =>
        new([.. band.Name[(band.Chromosome.Length + 1)..].Select(c => c == '.' ? '.' : '9')]);

    private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The chromosome lengths per build: the largest band end of each chromosome.</summary>
    private sealed class ChromosomeEnds(BandSet bands)
    {
        private readonly Dictionary<string, long> _grch37 = Max(bands, b => b.Grch37);
        private readonly Dictionary<string, long> _grch38 = Max(bands, b => b.Grch38);

        public long? Of(string chromosome, string build) =>
            (build == ArrPositionsInBandsCheck.Grch37 ? _grch37 : _grch38).TryGetValue(chromosome, out var l) ? l : null;

        private static Dictionary<string, long> Max(BandSet bands, Func<Band, GenomicRange?> range) =>
            bands.Bands
                .Where(b => range(b) is not null)
                .GroupBy(b => b.Chromosome, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Max(b => range(b)!.End), StringComparer.Ordinal);
    }
}
