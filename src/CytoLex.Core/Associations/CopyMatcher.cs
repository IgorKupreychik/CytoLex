using System.Globalization;
using CytoLex.Core.Kb;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Associations;

/// <summary>
/// The <c>copy</c> leaf (T-109): a gain or loss locus (T-101) on the rule's chromosome whose GRCh38 coordinates overlap
/// the rule's region; any overlap matches, and the evidence carries the share of the region covered (decided with the
/// user at T-109).
/// <para>
/// Gain or loss comes from the karyotype symbol (<see cref="LocusEffect.Gain"/>/<see cref="LocusEffect.Loss"/>: <c>+</c>,
/// <c>–</c>, <c>del</c>, <c>dup</c>, <c>trp</c>, <c>qdp</c>) as it is. A microarray copy number or an in situ signal is
/// written as a count and not read as gain/loss anywhere else (T-072, T-105); for matching only, it is read against two
/// copies on autosomes (decided with the user at T-109): ×0/×1 = loss, ×3 or more and <c>amp</c> = gain, an in situ
/// status <c>–</c> without a count = loss. X and Y (sex-dependent), <c>cx</c>, <c>cth</c>, ranges that include 2,
/// counts of a <c>con</c>/<c>sep</c> position group (fusion signals) and normal results are not read.
/// </para>
/// </summary>
internal static class CopyMatcher
{
    public static IEnumerable<AssociationEvidence> Match(CopyLeaf leaf, MatchScope scope)
    {
        foreach (var locus in scope.Loci)
        {
            if (!string.Equals(locus.Chromosome, leaf.Chromosome, StringComparison.Ordinal) || locus.Grch38 is not { } range)
            {
                continue;
            }

            if (Read(locus, scope) is not var (effect, reading) || effect != leaf.Effect)
            {
                continue;
            }

            var start = Math.Max(range.Start, leaf.Grch38.Start);
            var end = Math.Min(range.End, leaf.Grch38.End);
            if (start > end)
            {
                continue;
            }

            var overlap = Math.Round((double)(end - start + 1) / (leaf.Grch38.End - leaf.Grch38.Start + 1), 3);
            yield return new AssociationEvidence(AssociationLeafKind.Copy, leaf.Text, locus.Abnormality, locus.Index, locus.Span, locus.Text, overlap, reading);
        }
    }

    /// <summary>Gain or loss of a locus, with how a count was read; <c>null</c> when it says neither.</summary>
    internal static (CopyEffect Effect, string? Reading)? Read(LocusEntry locus, MatchScope scope)
    {
        switch (locus.Effect)
        {
            case LocusEffect.Gain:
                return (CopyEffect.Gain, null);
            case LocusEffect.Loss:
                return (CopyEffect.Loss, null);
            case LocusEffect.CopyNumber or LocusEffect.Signal when locus.Chromosome is not ("X" or "Y") && locus.Copy is { } copy:
                if (locus.Effect == LocusEffect.Signal && InPositionGroup(locus, scope))
                {
                    return null;
                }

                return ReadCount(copy) is { } effect ? (effect, $"{copy} read as {(effect == CopyEffect.Gain ? "gain" : "loss")}") : null;
            default:
                return null;
        }
    }

    /// <summary>A copy number or signal as written (<c>×1</c>, <c>×2~4</c>, <c>amp</c>, <c>–</c>, <c>+×3</c>, <c>×~&gt;50</c>) read against 2.</summary>
    internal static CopyEffect? ReadCount(string copy)
    {
        if (copy == "amp")
        {
            return CopyEffect.Gain;
        }

        var times = copy.IndexOf('×', StringComparison.Ordinal);
        if (times < 0)
        {
            // A status alone: – (absent on one homologue) is a loss; + only says the signal is there.
            return copy.Length > 0 && copy.All(ch => ch is '–' or '-') ? CopyEffect.Loss : null;
        }

        var count = copy[(times + 1)..];
        var openEnded = count.StartsWith("~>", StringComparison.Ordinal);
        var parts = (openEnded ? count[2..] : count).Split('~');
        if (parts.Length is < 1 or > 2 || !parts.All(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return null;
        }

        var low = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var high = openEnded ? int.MaxValue : int.Parse(parts[^1], CultureInfo.InvariantCulture);
        return high < 2 ? CopyEffect.Loss : low > 2 ? CopyEffect.Gain : null;
    }

    private static bool InPositionGroup(LocusEntry locus, MatchScope scope) =>
        scope.Entry(locus.Abnormality)?.InSitu is { } inSitu
        && inSitu.Groups.Any(g => g.IsPosition && g.Loci.Any(l => l.Span == locus.Span));
}
