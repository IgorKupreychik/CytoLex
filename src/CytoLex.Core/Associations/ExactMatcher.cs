using CytoLex.Core.Kb;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Associations;

/// <summary>
/// The <c>exact</c> leaf (T-109): an abnormality written as the rule's form, band-level tolerant. Kind, sign, symbol and
/// chromosomes must be the same, with the same number of breakpoints, each the rule's band or a finer band inside it
/// (landmarks such as <c>pter</c> the same). Copies (<c>×2</c>) and the <c>c</c> mark are not compared; a derivative
/// is not the rearrangement it comes from (<c>der(22)t(9;22)</c> is matched by <c>fusion</c>, not by <c>exact t(9;22)</c>).
/// Questioned and ranged breakpoints never match a band.
/// </summary>
internal static class ExactMatcher
{
    public static IEnumerable<AssociationEvidence> Match(ExactLeaf leaf, AbnormalityEntry? pattern, MatchScope scope)
    {
        if (pattern is null)
        {
            yield break;
        }

        foreach (var c in scope.Candidates)
        {
            if (Matches(pattern, c.Entry, scope.Nesting))
            {
                yield return new AssociationEvidence(AssociationLeafKind.Exact, leaf.Text, c.Index, null, c.Entry.Span, c.Entry.Text);
            }
        }
    }

    internal static bool Matches(AbnormalityEntry pattern, AbnormalityEntry entry, BandNesting nesting)
    {
        if (entry.Kind != pattern.Kind
            || entry.Sign != pattern.Sign
            || !string.Equals(entry.Symbol, pattern.Symbol, StringComparison.Ordinal)
            || entry.Parts.Length != pattern.Parts.Length
            || !entry.Chromosomes.SequenceEqual(pattern.Chromosomes, StringComparer.Ordinal)
            || entry.Breakpoints.Length != pattern.Breakpoints.Length)
        {
            return false;
        }

        for (var i = 0; i < pattern.Breakpoints.Length; i++)
        {
            var want = pattern.Breakpoints[i];
            var have = entry.Breakpoints[i];
            if (have.IsQuestioned || have.RangeEnd is not null || !have.Alternatives.IsEmpty)
            {
                return false;
            }

            var ok = want.Name is { } name
                ? nesting.Nests(have.Name, name)
                : want.Landmark is { } landmark && landmark != "?"
                    && string.Equals(have.Landmark, landmark, StringComparison.Ordinal)
                    && string.Equals(have.Chromosome, want.Chromosome, StringComparison.Ordinal);
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
