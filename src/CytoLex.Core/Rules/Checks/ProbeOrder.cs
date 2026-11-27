using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>One order fault of the loci of an in situ result (T-103), tagged with its rule.</summary>
/// <param name="Rule">The rule ID.</param>
/// <param name="Span">The misplaced locus.</param>
/// <param name="Other">The locus it should come before.</param>
/// <param name="Detail">What was found, naming the probe table source of unreviewed rows.</param>
/// <param name="Unreviewed">The finding relies on a probe table row nobody has reviewed: reported as uncertain (warning).</param>
internal sealed record ProbeOrderFinding(string Rule, TextSpan Span, TextSpan Other, string Detail, bool Unreviewed);

/// <summary>
/// The order of the loci of in situ results, from where each probe lies (T-103):
/// <list type="bullet">
/// <item><c>ISH.LOCUS_ORDER_PTER_QTER</c>: loci on one chromosome from pter to qter (ish, nuc ish, fib ish).</item>
/// <item><c>NUC_ISH.CHROMOSOME_ORDER</c>: nuc ish loci of different chromosomes, sex chromosomes then 1 to 22, within a
/// set of parentheses and between separate hybridizations.</item>
/// </list>
/// A probe lies where its probe table row puts it (T-102), or on the chromosome its name gives (a D-number or a paint,
/// ISH.LOCUS_DESIGNATION). Positions are compared only for loci in different, not nested bands, and not for two ends
/// (<c>5′</c>/<c>3′</c>) of one gene: within a band the order is below banding resolution (the book's
/// <c>nuc ish(KAL1,STS)×2</c>), and the table has no strand. Signal position groups (<c>con</c>/<c>sep</c>) are not
/// lists, and ish abnormalities are read only when the symbol keeps the loci in their native order on one chromosome
/// (<c>del</c>, <c>dup</c>, <c>trp</c>, <c>qdp</c>, <c>r</c>). Decisions: docs/decisions.md (T-103).
/// </summary>
internal static class ProbeOrder
{
    public const string LocusOrder = "ISH.LOCUS_ORDER_PTER_QTER";
    public const string ChromosomeOrder = "NUC_ISH.CHROMOSOME_ORDER";

    private static readonly ImmutableHashSet<string> _nativeOrder = ["del", "dup", "trp", "qdp", "r"];

    public static IEnumerable<ProbeOrderFinding> Findings(FishModel model, ProbeTable probes)
    {
        if (model.Type is not (NotationType.Ish or NotationType.NucIsh or NotationType.FibIsh))
        {
            yield break;
        }

        foreach (var line in model.CellLines)
        {
            Placed? previousLowest = null;
            foreach (var item in line.Items)
            {
                var lists = Lists(item).ToList();
                foreach (var list in lists)
                {
                    foreach (var f in PterToQter(list, probes))
                    {
                        yield return f;
                    }
                }

                if (model.Type != NotationType.NucIsh || item.Region is not { } region)
                {
                    continue;
                }

                var placed = lists.SelectMany(l => l).Select(l => Place(l, probes)).OfType<Placed>().ToList();
                foreach (var f in ChromosomesInGroup(placed))
                {
                    yield return f;
                }

                // Separate hybridizations: the set with the lowest chromosome comes first (§13.3.1 ¶5).
                var lowest = Lowest(region, placed);
                if (lowest is not null && previousLowest is not null && lowest.Rank < previousLowest.Rank)
                {
                    yield return Finding(
                        ChromosomeOrder,
                        lowest,
                        previousLowest,
                        $"the probe set of chromosome {lowest.Chromosome} ({lowest.Name}) comes after that of chromosome {previousLowest.Chromosome} ({previousLowest.Name}); sets are ordered by their lowest chromosome, sex chromosomes first");
                }

                if (lowest is not null && (previousLowest is null || lowest.Rank >= previousLowest.Rank))
                {
                    previousLowest = lowest;
                }
            }
        }
    }

    /// <summary>The locus lists of an item that are read in order: per locus group, split at <c>;</c>; no position groups.</summary>
    private static IEnumerable<List<Locus>> Lists(FishItem item)
    {
        if (item.Abnormality is Abnormality a && (a.Parts.Length != 1 || a.Kind is not { } kind || !_nativeOrder.Contains(kind)))
        {
            yield break;
        }

        if (item.Abnormality is not null and not Abnormality)
        {
            yield break;
        }

        foreach (var group in item.LocusGroups.Where(g => !g.IsPosition))
        {
            var current = new List<Locus>();
            for (var i = 0; i < group.Loci.Length; i++)
            {
                current.Add(group.Loci[i]);
                if (i < group.Separators.Length && group.Separators[i] == LocusSeparator.Semicolon)
                {
                    yield return current;
                    current = [];
                }
            }

            if (current.Count > 0)
            {
                yield return current;
            }
        }
    }

    // ── pter → qter ─────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<ProbeOrderFinding> PterToQter(List<Locus> list, ProbeTable probes)
    {
        var last = new Dictionary<string, Placed>(StringComparer.Ordinal);
        foreach (var locus in list)
        {
            if (Place(locus, probes) is not { Match: { } match } placed)
            {
                continue;
            }

            if (last.TryGetValue(placed.Chromosome, out var before) && Comparable(before, placed) is { } build)
            {
                var a = before.Match!.Row.In(build)!;
                var b = match.Row.In(build)!;
                if (b.Start < a.Start)
                {
                    yield return Finding(
                        LocusOrder,
                        placed,
                        before,
                        $"{placed.Name} ({match.Row.Band}) lies nearer pter than {before.Name} ({before.Match.Row.Band}) but is listed after it");
                    continue;
                }
            }

            last[placed.Chromosome] = placed;
        }
    }

    /// <summary>The build both rows have positions in (GRCh38 first); <c>null</c> when the pair can't be ordered.</summary>
    private static ReferenceBuild? Comparable(Placed a, Placed b)
    {
        var ra = a.Match!.Row;
        var rb = b.Match!.Row;
        if (ra == rb || Nested(ra, rb))
        {
            return null;
        }

        return ra.Grch38 is not null && rb.Grch38 is not null ? ReferenceBuild.Grch38
            : ra.Grch37 is not null && rb.Grch37 is not null ? ReferenceBuild.Grch37
            : null;
    }

    /// <summary>The rows share a band or one's band lies in the other's (<c>Xp22.31</c>/<c>Xp22.31</c>, <c>9q34</c>/<c>9q34.12</c>).</summary>
    private static bool Nested(ProbeRow a, ProbeRow b)
    {
        string[] Bands(ProbeRow r) => r.ToBand is { } to ? [r.FromBand, to] : [r.FromBand];
        return Bands(a).Any(x => Bands(b).Any(y => x.StartsWith(y, StringComparison.Ordinal) || y.StartsWith(x, StringComparison.Ordinal)));
    }

    // ── nuc ish chromosome order ────────────────────────────────────────────────────────────────

    private static IEnumerable<ProbeOrderFinding> ChromosomesInGroup(List<Placed> placed)
    {
        Placed? highest = null;
        foreach (var p in placed)
        {
            if (highest is not null && p.Rank < highest.Rank)
            {
                yield return Finding(
                    ChromosomeOrder,
                    p,
                    highest,
                    $"{p.Name} (chromosome {p.Chromosome}) is listed after {highest.Name} (chromosome {highest.Chromosome}); loci go sex chromosomes first, then 1 to 22");
                continue;
            }

            highest = p;
        }
    }

    /// <summary>The lowest chromosome of a hybridization: the written region's, else its loci's.</summary>
    private static Placed? Lowest(FishRegion region, List<Placed> placed)
    {
        if (region.Chromosome is { } c && Rank(c.Name) is { } rank)
        {
            return new Placed(c.Name, c.Span, c.Name, rank, null);
        }

        return placed.Count == 0 ? null : placed.MinBy(p => p.Rank);
    }

    // ── placing loci ────────────────────────────────────────────────────────────────────────────

    /// <summary>A locus with its chromosome: from the probe table, else from its name (D-number, paint).</summary>
    private sealed record Placed(string Name, TextSpan Span, string Chromosome, int Rank, ProbeMatch? Match);

    private static Placed? Place(Locus locus, ProbeTable probes)
    {
        if (LocusNames.Chromosome(locus.Name) is var (chromosome, _) && Rank(chromosome) is { } r)
        {
            return new Placed(locus.Name, locus.NameSpan, chromosome, r, null);
        }

        return probes.Find(locus.Name) is { } match && Rank(match.Row.Chromosome) is { } rank
            ? new Placed(locus.Name, locus.NameSpan, match.Row.Chromosome, rank, match)
            : null;
    }

    /// <summary>X = 0, Y = 1, autosome n = n + 1 (sex chromosomes first, §13.3.1 ¶1).</summary>
    private static int? Rank(string chromosome) => chromosome switch
    {
        "X" => 0,
        "Y" => 1,
        _ when LocusNames.IsChromosome(chromosome) => int.Parse(chromosome, System.Globalization.CultureInfo.InvariantCulture) + 1,
        _ => null,
    };

    private static ProbeOrderFinding Finding(string rule, Placed misplaced, Placed other, string detail)
    {
        var unreviewed = new[] { misplaced, other }
            .Where(p => p.Match is { Row.Reviewed: false })
            .DistinctBy(p => p.Match!.Row)
            .ToList();
        if (unreviewed.Count > 0)
        {
            var sources = unreviewed
                .GroupBy(p => p.Match!.Row.Source, StringComparer.Ordinal)
                .Select(g => $"{string.Join(", ", g.Select(p => p.Match!.Row.Name))} (source: {g.Key})");
            detail += $"; from probe table rows not yet reviewed: {string.Join("; ", sources)}";
        }

        return new ProbeOrderFinding(rule, misplaced.Span, other.Span, detail, unreviewed.Count > 0);
    }
}
