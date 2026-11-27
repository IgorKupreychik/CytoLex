using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Associations;

/// <summary>
/// The <c>fusion</c> leaf (T-109): two bands joined by a rearrangement, whatever the notation, or an in situ signal
/// position group whose loci are joined by <c>con</c>.
/// <list type="bullet">
/// <item>A structural abnormality (any symbol: <c>t</c>, <c>inv</c>, <c>ins</c>, <c>der</c>, <c>dic</c>, …) with two
/// certain breakpoints, one in each band (the rule's band or a finer one). In a derivative or recombinant each symbol
/// group is looked at alone, so breakpoints of different rearrangements are not paired.</item>
/// <item>Two loci joined by <c>con</c> (<c>ABL1 con BCR</c>), each placed (written band or probe table row) in one of the
/// bands.</item>
/// </list>
/// </summary>
internal static class FusionMatcher
{
    public static IEnumerable<AssociationEvidence> Match(FusionLeaf leaf, MatchScope scope)
    {
        foreach (var c in scope.Candidates)
        {
            var entry = c.Entry;
            if (entry.Kind == AbnormalityKind.Structural && Groups(entry).Any(g => Joins(g, leaf, scope.Nesting)))
            {
                yield return new AssociationEvidence(AssociationLeafKind.Fusion, leaf.Text, c.Index, null, entry.Span, entry.Text);
                continue;
            }

            if (entry.InSitu is not { } inSitu)
            {
                continue;
            }

            foreach (var group in inSitu.Groups.Where(g => g.IsPosition))
            {
                if (ConLocus(group, leaf, scope, c.Index) is { } locus)
                {
                    yield return new AssociationEvidence(AssociationLeafKind.Fusion, leaf.Text, c.Index, locus, group.Span, group.Text);
                }
            }
        }
    }

    /// <summary>The breakpoints of each symbol group: the whole entry, or each part of a derivative or recombinant.</summary>
    private static IEnumerable<ImmutableArray<BreakpointSummary>> Groups(AbnormalityEntry entry) =>
        entry.Parts.IsEmpty ? [entry.Breakpoints] : entry.Parts.Select(p => p.Breakpoints);

    private static bool Joins(ImmutableArray<BreakpointSummary> breakpoints, FusionLeaf leaf, BandNesting nesting)
    {
        var names = breakpoints.Where(b => !b.IsQuestioned && b.RangeEnd is null && b.Alternatives.IsEmpty).Select(b => b.Name).ToList();
        for (var i = 0; i < names.Count; i++)
        {
            for (var j = 0; j < names.Count; j++)
            {
                if (i != j && nesting.Nests(names[i], leaf.BandA) && nesting.Nests(names[j], leaf.BandB))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The locus index of the first of two loci joined by <c>con</c> that lie in the two bands; <c>null</c> when none.</summary>
    private static int? ConLocus(LocusGroupSummary group, FusionLeaf leaf, MatchScope scope, int index)
    {
        var loci = scope.LociOf(index).ToList();
        for (var i = 0; i < group.Separators.Length && i + 1 < group.Loci.Length; i++)
        {
            if (!string.Equals(group.Separators[i], "con", StringComparison.Ordinal))
            {
                continue;
            }

            var a = loci.FirstOrDefault(l => l.Span == group.Loci[i].Span);
            var b = loci.FirstOrDefault(l => l.Span == group.Loci[i + 1].Span);
            if (a is null || b is null)
            {
                continue;
            }

            if ((In(a, leaf.BandA, scope.Nesting) && In(b, leaf.BandB, scope.Nesting))
                || (In(a, leaf.BandB, scope.Nesting) && In(b, leaf.BandA, scope.Nesting)))
            {
                return a.Index;
            }
        }

        return null;
    }

    /// <summary>A locus lies in <paramref name="band"/>: its band (or both ends of its band range) nested in it.</summary>
    private static bool In(LocusEntry locus, string band, BandNesting nesting) =>
        locus.Extent is LocusExtent.Band or LocusExtent.BandRange or LocusExtent.Positions
        && nesting.Nests(Full(locus, locus.From), band)
        && (locus.To is null || nesting.Nests(Full(locus, locus.To), band));

    /// <summary>A band of a locus with its chromosome (<c>From</c>/<c>To</c> may be written without it).</summary>
    private static string? Full(LocusEntry locus, string? band) =>
        band is null || locus.Chromosome is null || band.StartsWith(locus.Chromosome, StringComparison.Ordinal) && band.Length > locus.Chromosome.Length && band[locus.Chromosome.Length] is 'p' or 'q'
            ? band
            : locus.Chromosome + band;
}
