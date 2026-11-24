using System.Collections.Immutable;
using CytoLex.Core.Composing;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>STRUCT.DETAILED_MATCHES_SYMBOL</c> (§4.3 ¶1, §4.3.2 ¶1, T-086): a band composition written in the detailed system is
/// the one its symbol defines. The breakpoints of the short system are not written in the detailed system, so every
/// reading of the symbol over the bands the composition names is tried (<see cref="Composer.FromGroups"/>); the
/// composition is reported when none gives it, in either orientation (orientation is <c>STRUCT.COMPOSITION_ORDER</c>'s).
/// </summary>
/// <remarks>
/// Checked for one-part abnormalities of the types with a defined composition (del, dup, trp, qdp, inv, add, hsr, i,
/// idic, ins, r, fis, t, rob, dic, trc). Not checked: derivatives and recombinants (the composition is their definition;
/// <c>DER.CENTROMERE_CHROMOSOME</c> checks their centromere), <c>tas</c> chains, compositions without a composition model
/// (uncertain points, <c>neo</c>, <c>ter</c>), unknown chromosomes, or too many readings.
/// </remarks>
internal static class DetailedSymbolMatch
{
    public const string Rule = "STRUCT.DETAILED_MATCHES_SYMBOL";

    private const int _maxReadings = 4096;

    private static readonly int[] _oneOrTwo = [1, 2];

    private static readonly int[] _two = [2];

    private static readonly ImmutableHashSet<string> _checked =
        ["del", "dup", "trp", "qdp", "inv", "add", "hsr", "i", "idic", "ins", "r", "fis", "t", "rob", "dic", "trc"];

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var a in model.DescendantsAndSelf().OfType<Abnormality>())
        {
            if (Expected(a) is { } expected)
            {
                var part = a.Parts[0];
                var span = part.Groups.Length == 1 && part.Groups[0].Structure is { } s ? s.Span : part.Span;
                yield return new StructuralFinding(
                    Rule,
                    span,
                    $"This band composition is not what {part.HeadSpan().Of(input)} defines for the bands it names: with breakpoints {expected.Breakpoints} it is {expected.Composition}.");
            }
        }
    }

    /// <summary>
    /// <c>null</c> when <paramref name="a"/> is not checked, no reading of its symbol has a composition, or one matches;
    /// otherwise what the most likely reading gives.
    /// </summary>
    internal static Reading? Expected(Abnormality a)
    {
        if (a.IsPartial || a.Parts is not [var part] || a.Composition is not { Source: CompositionSource.Written } written
            || part.Head is not { } head || !_checked.Contains(head) || part.Chromosomes.IsEmpty
            || part.Chromosomes.Any(c => c.IsUnknown))
        {
            return null;
        }

        // A short-system breakpoint list written with an arrow (del(5)(q13→q33)) is the symbol's own format fault.
        if (written.Products.All(p => p.Joins.IsEmpty && p.LeadingJoin is null && p.TrailingJoin is null
            && p.Pieces.All(piece => piece.Points.All(pt => pt.Kind == PointKind.Band))))
        {
            return null;
        }

        var chromosomes = part.Chromosomes.Select(ChromosomeId.Of).ToList();
        var points = written.Products.SelectMany(p => p.Pieces).SelectMany(p => p.Points).ToList();
        if (points.Any(p => p.Kind == PointKind.Band && p.Chromosome is null))
        {
            return null;
        }

        // The written bands of each chromosome of the symbol, in order of appearance.
        var bands = chromosomes.Distinct()
            .ToDictionary(c => c, c => points.Where(p => p.Kind == PointKind.Band && p.Chromosome == c).Distinct().ToList());

        // One chromosome written as dic(13)(q14q32) or ins(3)(q27p21p24): one group; else one group per chromosome.
        var sizes = Sizes(head, chromosomes.Count);
        if (sizes is null || sizes.Count != chromosomes.Count || chromosomes.Any(c => bands[c].Count == 0))
        {
            return null;
        }

        var symbols = part.Symbols.Select(s => s.Symbol).ToList();
        var readings = 1L;
        for (var i = 0; i < chromosomes.Count; i++)
        {
            readings *= sizes[i].Sum(n => (long)Math.Pow(bands[chromosomes[i]].Count, n));
        }

        if (readings > _maxReadings)
        {
            return null;
        }

        Reading? best = null;
        var bestScore = -1;
        var defined = false;
        foreach (var groups in Readings(chromosomes, bands, sizes))
        {
            if (Composer.FromGroups(symbols, groups, []) is not { Count: > 0 } products)
            {
                continue;
            }

            defined = true;
            if (Matches(written.Products, products))
            {
                return null;
            }

            // The most likely reading: the one that names the most written bands, with its bands distinct.
            var named = groups.SelectMany(g => g.Points).ToList();
            var score = (named.Distinct().Count() * 2) - (named.Count - named.Distinct().Count());
            if (score > bestScore)
            {
                bestScore = score;
                var withChromosomes = chromosomes.Count > 1;
                best = new Reading(
                    string.Join(" and ", named.Select(p => p.Format(withChromosomes))),
                    string.Join(";", products.Select(p => p.Format(withChromosomes))));
            }
        }

        return defined ? best : null;
    }

    /// <summary>The breakpoints of a reading and the composition it gives.</summary>
    internal sealed record Reading(string Breakpoints, string Composition);

    /// <summary>The number of points each group of the symbol takes (per chromosome); <c>null</c> when not checked.</summary>
    private static List<int[]>? Sizes(string head, int chromosomes) => (head, chromosomes) switch
    {
        ("del", 1) => [[1, 2]],
        ("add" or "hsr" or "i" or "idic" or "fis", 1) => [[1]],
        ("inv" or "dup" or "trp" or "qdp" or "r" or "dic", 1) => [[2]],
        ("ins", 1) => [[3]],
        ("ins", 2) => [[1], [2]],
        ("t", >= 2) => [.. Enumerable.Repeat(_oneOrTwo, chromosomes)],
        ("rob" or "dic", 2) => [[1], [1]],
        ("r", >= 2) => [.. Enumerable.Repeat(_two, chromosomes)],
        ("trc", 3) => [[1], [2], [1]],
        _ => null,
    };

    /// <summary>Every choice of points per group (ordered, repeats allowed: <c>del(5)(q13q13)</c>).</summary>
    private static IEnumerable<List<Group>> Readings(List<ChromosomeId> chromosomes, Dictionary<ChromosomeId, List<BandPoint>> bands, List<int[]> sizes)
    {
        IEnumerable<List<Group>> acc = [[]];
        for (var i = 0; i < chromosomes.Count; i++)
        {
            var c = chromosomes[i];
            var options = sizes[i].SelectMany(n => Tuples(bands[c], n)).Select(t => new Group(c, [.. t])).ToList();
            acc = acc.SelectMany(prefix => options.Select(o => new List<Group>(prefix) { o })).ToList();
        }

        return acc;
    }

    private static IEnumerable<List<BandPoint>> Tuples(List<BandPoint> bands, int n)
    {
        if (n == 0)
        {
            yield return [];
            yield break;
        }

        foreach (var head in bands)
        {
            foreach (var rest in Tuples(bands, n - 1))
            {
                yield return [head, .. rest];
            }
        }
    }

    /// <summary>Every written product is a computed one, read either way, and the counts agree.</summary>
    private static bool Matches(ImmutableArray<ComposedChromosome> written, List<ComposedChromosome> computed)
    {
        if (written.Length != computed.Count)
        {
            return false;
        }

        var left = new List<ComposedChromosome>(computed);
        foreach (var w in written)
        {
            var i = left.FindIndex(c => c.Equals(w) || c.Equals(w.Reversed()));
            if (i < 0)
            {
                return false;
            }

            left.RemoveAt(i);
        }

        return true;
    }
}
