using System.Collections.Frozen;
using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>
/// Builds <see cref="EditionResult.Loci"/> (T-101) from the abnormality list: karyotype breakpoints and del/dup
/// segments, whole-chromosome gains and losses, arr regions and positions, in situ regions and probes placed by name.
/// Coordinates come from the edition's band data; written arr positions win for their own build.
/// </summary>
internal sealed class LocusList(string input, BandSet? bands, ImmutableArray<CellLineSummary> cellLines)
{
    /// <summary>The rule that allows D-numbers and paints as locus names (§13.2 ¶1): the basis of placing a probe by its name.</summary>
    internal const string LocusDesignation = "ISH.LOCUS_DESIGNATION";

    private const string _probeUnplaced = "probe position unknown (probe table: T-103)";

    private static readonly FrozenSet<string> _losses = FrozenSet.Create(StringComparer.Ordinal, "del");
    private static readonly FrozenSet<string> _gains = FrozenSet.Create(StringComparer.Ordinal, "dup", "trp", "qdp");

    private readonly List<LocusEntry> _loci = [];

    /// <summary>The loci of <paramref name="entries"/>, in input order.</summary>
    public static ImmutableArray<LocusEntry> Build(string input, BandSet? bands, ImmutableArray<CellLineSummary> cellLines, ImmutableArray<AbnormalityEntry> entries)
    {
        var list = new LocusList(input, bands, cellLines);
        foreach (var entry in entries)
        {
            list.Add(entry, entry.Index, isAlternative: false);
        }

        return [.. list._loci];
    }

    private void Add(AbnormalityEntry entry, int index, bool isAlternative)
    {
        var line = entry.CellLine >= 0 && entry.CellLine < cellLines.Length ? cellLines[entry.CellLine] : null;
        var ctx = new Context(entry, index, line, isAlternative);
        switch (entry.Kind)
        {
            case AbnormalityKind.Alternatives:
                foreach (var reading in entry.Readings)
                {
                    Add(reading with { CellLine = entry.CellLine }, index, isAlternative: true);
                }

                return;
            case AbnormalityKind.CloneReference or AbnormalityKind.Unrecognised:
                return;
            case AbnormalityKind.ArrResult when entry.Arr is { } arr:
                AddArr(ctx, arr);
                return;
            case AbnormalityKind.InSituRegion when entry.InSitu is { } inSitu:
                AddRegion(ctx, inSitu);
                return;
            case AbnormalityKind.Gain or AbnormalityKind.Loss:
                AddWholeChromosome(ctx);
                break;
            case AbnormalityKind.Variant:
                AddVariant(ctx);
                break;
            case AbnormalityKind.Structural:
                AddStructural(ctx);
                break;
        }

        if (entry.InSitu is { } ish)
        {
            AddProbes(ctx, ish, placement: null);
        }
    }

    // --- karyotype ---------------------------------------------------------------------------------------------------

    private void AddWholeChromosome(Context ctx)
    {
        var effect = ctx.Entry.Kind == AbnormalityKind.Gain ? LocusEffect.Gain : LocusEffect.Loss;
        var copy = ctx.Entry.Flags.Copies is { } n ? $"×{n}" : null;
        var name = ctx.Entry.Chromosomes.FirstOrDefault();
        if (name is null || ChromosomeArm(name) is not var (chromosome, arm) || !HasChromosome(chromosome))
        {
            AddUnplaced(ctx, ctx.Entry.Span, effect, copy, probe: null, $"chromosome {name ?? "?"} is not known");
            return;
        }

        AddChromosome(ctx, ctx.Entry.Span, chromosome, arm, effect, copy, probe: null, ctx.Entry.Flags.IsUncertain);
    }

    private void AddVariant(Context ctx)
    {
        if (ctx.Entry.Breakpoints.Any(b => b.Name is not null))
        {
            foreach (var b in ctx.Entry.Breakpoints)
            {
                AddPoint(ctx, b, LocusEffect.Variant);
            }

            return;
        }

        if (ctx.Entry.Chromosomes.FirstOrDefault() is { } name && ChromosomeArm(name) is var (chromosome, arm) && HasChromosome(chromosome))
        {
            AddChromosome(ctx, ctx.Entry.Span, chromosome, arm ?? ArmOf(ctx.Entry.Text, chromosome), LocusEffect.Variant, null, null, ctx.Entry.Flags.IsUncertain);
        }
    }

    private void AddStructural(Context ctx)
    {
        if (ctx.Entry.Parts.IsEmpty)
        {
            AddSymbol(ctx, ctx.Entry.Symbol, ctx.Entry.Breakpoints, ctx.Entry.Span);
            return;
        }

        foreach (var part in ctx.Entry.Parts)
        {
            AddSymbol(ctx, part.Symbol, part.Breakpoints, part.Span);
        }
    }

    private void AddSymbol(Context ctx, string? symbol, ImmutableArray<BreakpointSummary> breakpoints, TextSpan span)
    {
        var points = breakpoints.Where(b => b.Landmark is not ("pter" or "qter" or "ter")).ToList();
        if (points.Count == 0)
        {
            return;
        }

        if (symbol is not null && (_losses.Contains(symbol) || _gains.Contains(symbol)))
        {
            AddSegments(ctx, _losses.Contains(symbol) ? LocusEffect.Loss : LocusEffect.Gain, points, span);
            return;
        }

        foreach (var b in points)
        {
            AddPoint(ctx, b, LocusEffect.Break);
        }
    }

    /// <summary>
    /// The segment of a del/dup (and trp/qdp): per chromosome, from the first to the last of its non-terminal
    /// breakpoints, or from a single breakpoint to the end of its arm (a terminal deletion).
    /// </summary>
    private void AddSegments(Context ctx, LocusEffect effect, List<BreakpointSummary> points, TextSpan span)
    {
        foreach (var group in points.GroupBy(b => b.Chromosome))
        {
            var list = group.ToList();
            if (group.Key is not { } chromosome || !HasChromosome(chromosome))
            {
                foreach (var b in list)
                {
                    AddUnplaced(ctx, b.Span, effect, null, null, "chromosome unknown");
                }

                continue;
            }

            var uncertain = ctx.Entry.Flags.IsUncertain || list.Any(IsUncertain);
            if (list.Count == 1)
            {
                var b = list[0];
                var arm = b.Arm;
                var toEnd = arm == "p" ? "pter" : "qter";
                if (arm is null || b.Landmark == "cen")
                {
                    AddPoint(ctx, b, effect);
                    continue;
                }

                // A questioned band (q2?) has no coordinates of its own: the arm stands for it.
                var g37 = Terminal(chromosome, b, arm, ReferenceBuild.Grch37) ?? (uncertain ? bands?.ArmRange(chromosome, arm, ReferenceBuild.Grch37) : null);
                var g38 = Terminal(chromosome, b, arm, ReferenceBuild.Grch38) ?? (uncertain ? bands?.ArmRange(chromosome, arm, ReferenceBuild.Grch38) : null);
                AddLocus(ctx, span, chromosome, LocusExtent.BandRange, BandText(b), chromosome + toEnd, g37, g38, null, effect, null, null, uncertain, NoCoordinates(g37, g38));
                continue;
            }

            var ordered = list.OrderBy(b => Point(chromosome, b, ReferenceBuild.Grch38)?.Start ?? long.MaxValue).ToList();
            var first = ordered[0];
            var last = ordered[^1];
            var r37 = Cover(chromosome, ordered, ReferenceBuild.Grch37);
            var r38 = Cover(chromosome, ordered, ReferenceBuild.Grch38);
            var same = BandText(first) == BandText(last);
            AddLocus(
                ctx,
                span,
                chromosome,
                same ? LocusExtent.Band : LocusExtent.BandRange,
                BandText(first),
                same ? null : BandText(last),
                r37,
                r38,
                null,
                effect,
                null,
                null,
                uncertain,
                NoCoordinates(r37, r38));
        }
    }

    /// <summary>One breakpoint: a band, an interval <c>q21~24</c>, or the arm of a questioned one.</summary>
    private void AddPoint(Context ctx, BreakpointSummary b, LocusEffect effect)
    {
        if (b.Chromosome is not { } chromosome || !HasChromosome(chromosome))
        {
            AddUnplaced(ctx, b.Span, effect, null, null, "chromosome unknown");
            return;
        }

        var uncertain = ctx.Entry.Flags.IsUncertain || IsUncertain(b);
        if (b.Name is null && b.RangeEnd is null && b.Landmark is not ("cen" or "pter" or "qter"))
        {
            // A questioned band (q2?, ?) gives its arm when one is written.
            if (b.Arm is { } arm)
            {
                var a37 = bands?.ArmRange(chromosome, arm, ReferenceBuild.Grch37);
                var a38 = bands?.ArmRange(chromosome, arm, ReferenceBuild.Grch38);
                AddLocus(ctx, b.Span, chromosome, LocusExtent.Arm, arm, null, a37, a38, null, effect, null, null, true, NoCoordinates(a37, a38));
            }
            else
            {
                AddChromosome(ctx, b.Span, chromosome, null, effect, null, null, true);
            }

            return;
        }

        var g37 = Point(chromosome, b, ReferenceBuild.Grch37);
        var g38 = Point(chromosome, b, ReferenceBuild.Grch38);
        if (b.RangeEnd is { } rangeEnd)
        {
            var end = BandName(chromosome, b.Arm, rangeEnd);
            g37 = Union(g37, BandRange(end, ReferenceBuild.Grch37));
            g38 = Union(g38, BandRange(end, ReferenceBuild.Grch38));
            AddLocus(ctx, b.Span, chromosome, LocusExtent.BandRange, BandText(b), end, g37, g38, null, effect, null, null, true, NoCoordinates(g37, g38));
            return;
        }

        AddLocus(ctx, b.Span, chromosome, LocusExtent.Band, BandText(b), null, g37, g38, null, effect, null, null, uncertain, NoCoordinates(g37, g38));
    }

    // --- arr ---------------------------------------------------------------------------------------------------------

    private void AddArr(Context ctx, ArrResultSummary arr)
    {
        var copy = arr.CopyText;
        var uncertain = ctx.Entry.Flags.IsUncertain;
        if (arr.IsWholeChromosomes)
        {
            foreach (var name in ctx.Entry.Chromosomes)
            {
                if (ChromosomeArm(name) is var (chromosome, arm) && HasChromosome(chromosome))
                {
                    AddChromosome(ctx, ctx.Entry.Span, chromosome, arm, LocusEffect.CopyNumber, copy, null, uncertain);
                }
                else
                {
                    AddUnplaced(ctx, ctx.Entry.Span, LocusEffect.CopyNumber, copy, null, $"chromosome {name} is not known");
                }
            }

            return;
        }

        foreach (var region in arr.Regions)
        {
            AddArrRegion(ctx, arr, region, isAlternative: false);
            foreach (var alternative in region.Alternatives)
            {
                AddArrRegion(ctx, arr, alternative, isAlternative: true);
            }
        }
    }

    private void AddArrRegion(Context ctx, ArrResultSummary arr, ArrRegionSummary region, bool isAlternative)
    {
        var c = isAlternative ? ctx with { IsAlternative = true } : ctx;
        var uncertain = ctx.Entry.Flags.IsUncertain || region.Breakpoints.Any(IsUncertain);
        if (region.Chromosome is not { } chromosome || !HasChromosome(chromosome))
        {
            AddUnplaced(c, region.Span, LocusEffect.CopyNumber, arr.CopyText, null, "chromosome unknown");
            return;
        }

        var first = region.Breakpoints.FirstOrDefault();
        var last = region.Breakpoints.Length > 1 ? region.Breakpoints[^1] : null;
        var from = first is null ? null : BandText(first);
        var to = last is null || BandText(last) == from ? null : BandText(last);
        var b37 = Cover(chromosome, region.Breakpoints, ReferenceBuild.Grch37);
        var b38 = Cover(chromosome, region.Breakpoints, ReferenceBuild.Grch38);
        var build = WrittenBuild(arr.Build);
        var extent = to is null ? LocusExtent.Band : LocusExtent.BandRange;

        if (arr.IsDetailed && build is { } detailedBuild && !region.Positions.IsEmpty)
        {
            // The detailed system: a copy number per position item (§14.1 ¶2).
            foreach (var p in region.Positions)
            {
                if (p.Start is not { } start)
                {
                    continue;
                }

                var written = new GenomicRange(start, p.End ?? start);
                var (w37, w38) = detailedBuild == ReferenceBuild.Grch37 ? (written, (GenomicRange?)null) : (null, written);
                AddLocus(c, p.Span, chromosome, LocusExtent.Positions, from, to, w37 ?? b37, w38 ?? b38, arr.Build, LocusEffect.CopyNumber, p.CopyText ?? arr.CopyText, null, uncertain, null);
            }

            return;
        }

        if (build is { } b && region.Positions.FirstOrDefault()?.Start is { } s)
        {
            var lastPosition = region.Positions[^1];
            var written = new GenomicRange(s, lastPosition.End ?? lastPosition.Start ?? s);
            if (written.Start <= written.End)
            {
                var (g37, g38) = b == ReferenceBuild.Grch37 ? (written, b38) : (b37, written);
                AddLocus(c, region.Span, chromosome, LocusExtent.Positions, from, to, g37, g38, arr.Build, LocusEffect.CopyNumber, arr.CopyText, null, uncertain, null);
                return;
            }
        }

        AddLocus(c, region.Span, chromosome, extent, from, to, b37, b38, null, LocusEffect.CopyNumber, arr.CopyText, null, uncertain, NoCoordinates(b37, b38));
    }

    private static ReferenceBuild? WrittenBuild(string? build) => build switch
    {
        "GRCh37" => ReferenceBuild.Grch37,
        "GRCh38" => ReferenceBuild.Grch38,
        _ => null,
    };

    // --- in situ -----------------------------------------------------------------------------------------------------

    /// <summary>A region result: its probes placed on the region; a region without probes is one locus itself.</summary>
    private void AddRegion(Context ctx, InSituResultSummary inSitu)
    {
        var effect = inSitu.IsNormal ? LocusEffect.Normal : LocusEffect.Signal;
        var name = ctx.Entry.Chromosomes.FirstOrDefault();
        Placement? placement = null;
        if (name is not null && ChromosomeArm(name) is var (chromosome, arm) && HasChromosome(chromosome))
        {
            var band = ctx.Entry.Breakpoints.FirstOrDefault(b => b.Chromosome == chromosome);
            placement = band is not null
                ? new Placement(chromosome, band)
                : new Placement(chromosome, null, arm ?? ArmOf(ctx.Entry.ShortForm, chromosome));
        }

        if (inSitu.Groups.SelectMany(g => g.Loci).Any())
        {
            AddProbes(ctx, inSitu, placement);
            return;
        }

        if (placement is null)
        {
            AddUnplaced(ctx, ctx.Entry.Span, effect, null, null, "no chromosome written");
            return;
        }

        Place(ctx, ctx.Entry.Span, placement, effect, null, null, ctx.Entry.Flags.IsUncertain, []);
    }

    /// <summary>Each probe of an in situ result: on <paramref name="placement"/> (the region it is written for), else by its name.</summary>
    private void AddProbes(Context ctx, InSituResultSummary inSitu, Placement? placement)
    {
        var effect = inSitu.IsNormal ? LocusEffect.Normal : LocusEffect.Signal;
        foreach (var group in inSitu.Groups)
        {
            // A number in place of a locus (subtel(41×2): the 41 subtelomeric loci) names no locus.
            foreach (var locus in group.Loci.Where(l => !l.Name.All(char.IsAsciiDigit)))
            {
                var copy = Signal(locus, group);
                if (placement is not null)
                {
                    Place(ctx, locus.Span, placement, effect, copy, locus.Name, ctx.Entry.Flags.IsUncertain, []);
                    continue;
                }

                if (ByName(locus.Name, ctx.Entry) is { } byName)
                {
                    Place(ctx, locus.Span, byName, effect, copy, locus.Name, ctx.Entry.Flags.IsUncertain, [KbRef.Rule(LocusDesignation)]);
                    continue;
                }

                AddUnplaced(ctx, locus.Span, effect, copy, locus.Name, _probeUnplaced);
            }
        }
    }

    /// <summary>
    /// A probe placed by its name: <c>wcp11</c> → chromosome 11, <c>pcp1q</c> → 1q, a D-number <c>D21Z1</c>/<c>D4S96</c>/
    /// <c>D4F26</c> → its chromosome (decided with the user, T-101), a subtelomeric band locus (<c>q34</c>) on the
    /// item's only chromosome; <c>null</c> otherwise.
    /// </summary>
    private Placement? ByName(string name, AbnormalityEntry entry)
    {
        if (LocusNames.Chromosome(name) is var (chromosome, rest) && HasChromosome(chromosome))
        {
            if (rest is ['p' or 'q', ..] && name.StartsWith("pcp", StringComparison.Ordinal))
            {
                var band = bands?.Find(chromosome + rest) is not null ? chromosome + rest : null;
                return band is not null ? new Placement(chromosome, null, null, band) : new Placement(chromosome, null, rest[..1]);
            }

            return new Placement(chromosome, null);
        }

        if (LocusNames.IsBand(name) && entry.Chromosomes.Distinct().ToList() is [var only] && ChromosomeArm(only) is var (c, _)
            && HasChromosome(c) && bands?.Find(c + name) is not null)
        {
            return new Placement(c, null, null, c + name);
        }

        return null;
    }

    private static string? Signal(LocusSummary locus, LocusGroupSummary group)
    {
        var count = locus.CountText ?? group.SharedCount;
        var text = locus.Status + (count is null ? string.Empty : "×" + count);
        return text.Length == 0 ? null : text;
    }

    private void Place(Context ctx, TextSpan span, Placement p, LocusEffect effect, string? copy, string? probe, bool uncertain, ImmutableArray<KbRef> extraKb)
    {
        var c = extraKb.IsEmpty ? ctx : ctx with { ExtraKb = extraKb };
        if (p.Breakpoint is { } b)
        {
            var g37 = Point(p.Chromosome, b, ReferenceBuild.Grch37);
            var g38 = Point(p.Chromosome, b, ReferenceBuild.Grch38);
            AddLocus(c, span, p.Chromosome, LocusExtent.Band, BandText(b), null, g37, g38, null, effect, copy, probe, uncertain || IsUncertain(b), NoCoordinates(g37, g38));
        }
        else if (p.Band is { } band)
        {
            var g37 = BandRange(band, ReferenceBuild.Grch37);
            var g38 = BandRange(band, ReferenceBuild.Grch38);
            AddLocus(c, span, p.Chromosome, LocusExtent.Band, band, null, g37, g38, null, effect, copy, probe, uncertain, NoCoordinates(g37, g38));
        }
        else
        {
            AddChromosome(c, span, p.Chromosome, p.Arm, effect, copy, probe, uncertain);
        }
    }

    // --- loci --------------------------------------------------------------------------------------------------------

    private void AddChromosome(Context ctx, TextSpan span, string chromosome, string? arm, LocusEffect effect, string? copy, string? probe, bool uncertain)
    {
        var g37 = arm is null ? bands?.ChromosomeRange(chromosome, ReferenceBuild.Grch37) : bands?.ArmRange(chromosome, arm, ReferenceBuild.Grch37);
        var g38 = arm is null ? bands?.ChromosomeRange(chromosome, ReferenceBuild.Grch38) : bands?.ArmRange(chromosome, arm, ReferenceBuild.Grch38);
        AddLocus(ctx, span, chromosome, arm is null ? LocusExtent.Chromosome : LocusExtent.Arm, arm, null, g37, g38, null, effect, copy, probe, uncertain, NoCoordinates(g37, g38));
    }

    private void AddUnplaced(Context ctx, TextSpan span, LocusEffect effect, string? copy, string? probe, string reason) =>
        AddLocus(ctx, span, null, LocusExtent.Unplaced, null, null, null, null, null, effect, copy, probe, ctx.Entry.Flags.IsUncertain, reason);

    private void AddLocus(
        Context ctx,
        TextSpan span,
        string? chromosome,
        LocusExtent extent,
        string? from,
        string? to,
        GenomicRange? grch37,
        GenomicRange? grch38,
        string? positionsBuild,
        LocusEffect effect,
        string? copy,
        string? probe,
        bool uncertain,
        string? reason)
    {
        var cells = ctx.Entry.InSitu?.Cells is { } ic
            ? $"[{ic.Text}]"
            : ctx.Line is { Technique: SegmentTechnique.Karyotype, Cells: { } n } line ? (line.IsComposite ? $"[cp{n}]" : $"[{n}]") : null;
        _loci.Add(new LocusEntry(
            _loci.Count,
            ctx.Index,
            ctx.Entry.CellLine,
            ctx.Line?.SegmentIndex ?? -1,
            ctx.Line?.Technique ?? SegmentTechnique.Karyotype,
            span,
            span.Of(input),
            chromosome,
            extent,
            from,
            to,
            grch37,
            grch38,
            positionsBuild,
            effect,
            copy ?? (ctx.Entry.Kind is not (AbnormalityKind.Gain or AbnormalityKind.Loss) && ctx.Entry.Flags.Copies is { } x ? $"×{x}" : null),
            probe,
            cells,
            uncertain,
            ctx.IsAlternative,
            LocusSource.Written,
            reason,
            [.. ctx.Entry.Kb.Concat(ctx.ExtraKb).Distinct()]));
    }

    // --- band data ---------------------------------------------------------------------------------------------------

    private bool HasChromosome(string chromosome) => bands is null ? LocusNames.IsChromosome(chromosome) : bands.Chromosomes.Contains(chromosome);

    /// <summary>The coordinates of a band name, walking up to the nearest enclosing band that has them; a region (<c>1p1</c>) covers its bands.</summary>
    private GenomicRange? BandRange(string name, ReferenceBuild build)
    {
        if (bands is null)
        {
            return null;
        }

        for (var band = bands.Find(name); band is not null; band = band.Parent is { } parent ? bands.Find(parent) : null)
        {
            if (band.In(build) is { } r)
            {
                return r;
            }
        }

        if (bands.ContainsRegion(name) && ChromosomeArm(name) is var (chromosome, _))
        {
            GenomicRange? cover = null;
            foreach (var b in bands.OfChromosome(chromosome).Where(b => b.Name.StartsWith(name, StringComparison.Ordinal)))
            {
                cover = Union(cover, b.In(build));
            }

            return cover;
        }

        return null;
    }

    /// <summary>A breakpoint's coordinates: its band, or a landmark (<c>pter</c>, <c>qter</c>, <c>cen</c>).</summary>
    private GenomicRange? Point(string chromosome, BreakpointSummary b, ReferenceBuild build)
    {
        if (bands is null)
        {
            return null;
        }

        switch (b.Landmark)
        {
            case "pter":
                return bands.ChromosomeRange(chromosome, build) is { } c ? new GenomicRange(c.Start, c.Start) : null;
            case "qter":
                return bands.ChromosomeRange(chromosome, build) is { } q ? new GenomicRange(q.End, q.End) : null;
            case "cen":
                return Union(bands.ArmRange(chromosome, "p", build) is { } p ? new GenomicRange(p.End, p.End) : null, bands.ArmRange(chromosome, "q", build) is { } qa ? new GenomicRange(qa.Start, qa.Start) : null);
        }

        if (b.Band == "10" && b.Arm is { } side)
        {
            // p10/q10: the centromere, at the edge of that arm (TRANSLOC.WHOLE_ARM, §9.2.17.2: arms fused at the centromeres).
            return bands.ArmRange(chromosome, side, build) is { } armRange
                ? side == "p" ? new GenomicRange(armRange.End, armRange.End) : new GenomicRange(armRange.Start, armRange.Start)
                : null;
        }

        return b.Name is { } name ? BandRange(name, build) : b.Arm is { } arm && b.Band is { } digits ? BandRange(chromosome + arm + digits, build) : null;
    }

    private GenomicRange? Cover(string chromosome, IEnumerable<BreakpointSummary> points, ReferenceBuild build)
    {
        GenomicRange? cover = null;
        foreach (var b in points)
        {
            cover = Union(cover, Point(chromosome, b, build));
        }

        return cover;
    }

    /// <summary>From a breakpoint to the end of its arm: <c>q21</c> → q21…qter, <c>p13</c> → pter…p13.</summary>
    private GenomicRange? Terminal(string chromosome, BreakpointSummary b, string arm, ReferenceBuild build)
    {
        if (Point(chromosome, b, build) is not { } point || bands?.ChromosomeRange(chromosome, build) is not { } whole)
        {
            return null;
        }

        return arm == "p" ? new GenomicRange(whole.Start, point.End) : new GenomicRange(point.Start, whole.End);
    }

    private static GenomicRange? Union(GenomicRange? a, GenomicRange? b) => a is null ? b : b is null ? a : a.Union(b);

    private string? NoCoordinates(GenomicRange? a, GenomicRange? b) =>
        a is null && b is null ? (bands is null ? "no band data for the edition" : "no coordinates for the band") : null;

    // --- names -------------------------------------------------------------------------------------------------------

    /// <summary>The band text with its chromosome: <c>9q34</c>, <c>9cen</c>.</summary>
    private static string BandText(BreakpointSummary b) => b.Name ?? b.Text;

    private static string BandName(string chromosome, string? arm, string text)
    {
        if (text.StartsWith(chromosome, StringComparison.Ordinal) && text.Length > chromosome.Length && text[chromosome.Length] is 'p' or 'q')
        {
            return text;
        }

        return text is ['p' or 'q', ..] ? chromosome + text : chromosome + arm + text;
    }

    private static bool IsUncertain(BreakpointSummary b) => b.IsQuestioned || b.Landmark == "?" || b.RangeEnd is not null || !b.Alternatives.IsEmpty;

    /// <summary>A chromosome name as listed (<c>16q</c> → 16 + q, <c>X</c> → X), or <c>null</c> for <c>?</c>, <c>mar</c>, ….</summary>
    private static (string Chromosome, string? Arm)? ChromosomeArm(string name)
    {
        var text = name.TrimStart('?');
        var i = text is ['X' or 'Y', ..] ? 1 : 0;
        while (i < text.Length && text[0] is not ('X' or 'Y') && char.IsAsciiDigit(text[i]))
        {
            i++;
        }

        if (i == 0)
        {
            return null;
        }

        var chromosome = text[..i];
        var arm = i < text.Length && text[i] is 'p' or 'q' ? text[i].ToString() : null;
        return (chromosome, arm);
    }

    /// <summary>The arm written right after the chromosome in <paramref name="text"/> (<c>15q</c> → q), if it is not followed by band digits.</summary>
    private static string? ArmOf(string text, string chromosome)
    {
        var at = text.IndexOf(chromosome, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var i = at + chromosome.Length;
        return i < text.Length && text[i] is 'p' or 'q' && (i + 1 >= text.Length || !char.IsAsciiDigit(text[i + 1])) ? text[i].ToString() : null;
    }

    private sealed record Context(AbnormalityEntry Entry, int Index, CellLineSummary? Line, bool IsAlternative)
    {
        public ImmutableArray<KbRef> ExtraKb { get; init; } = [];
    }

    /// <summary>Where a probe or region goes: a written breakpoint, a band name, an arm, or the whole chromosome.</summary>
    private sealed record Placement(string Chromosome, BreakpointSummary? Breakpoint, string? Arm = null, string? Band = null);
}
