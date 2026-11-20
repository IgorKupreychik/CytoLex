using System.Collections.Immutable;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// The order of unrelated clones (§11.1.6 ¶1–2) and of jumping translocations (§9.2.17.4), shared by
/// <c>CLONE.ORDER_UNRELATED</c> and <c>TRANSLOC.JUMPING</c>.
/// </summary>
internal static class UnrelatedClones
{
    /// <summary>
    /// Jumping translocations (§9.2.17.4): no related clones, and at least two unrelated clones, each a single
    /// <c>t(a;b)</c>, all sharing one chromosome (the segment that jumps). Their order is then <c>TRANSLOC.JUMPING</c>'s.
    /// </summary>
    public static bool IsJumping(ImmutableArray<CloneLine> related, ImmutableArray<CloneLine> unrelated)
    {
        if (!related.IsEmpty || unrelated.Length < 2)
        {
            return false;
        }

        var sets = new List<HashSet<string>>();
        foreach (var l in unrelated)
        {
            if (l.IsReference || l.HasOmitted || l.Changes is not [Abnormality { Kind: "t", Sign: GainLoss.None, Parts: [{ Chromosomes.Length: 2 } part] }])
            {
                return false;
            }

            sets.Add([.. part.Chromosomes.Select(c => c.Name)]);
        }

        return sets.Skip(1).Aggregate(new HashSet<string>(sets[0]), (acc, s) =>
        {
            acc.IntersectWith(s);
            return acc;
        }).Count > 0;
    }

    /// <summary>The sort key of an unrelated clone: larger first, then sex chromosome abnormalities, then the lowest autosome (§11.1.6 ¶1).</summary>
    public static (int Cells, int Sex, int Autosome) Key(CloneLine l) =>
        (-(l.Cells ?? 0), Clones.HasSexAbnormality(l, l.Changes) ? 0 : 1, Clones.LowestAutosome(l.Changes));

    /// <summary>
    /// The side with its clone positions refilled by <paramref name="clones"/> (normal and constitutional-only lines stay
    /// where they are).
    /// </summary>
    public static List<CloneLine> Refill(ImmutableArray<CloneLine> side, IReadOnlyList<CloneLine> clones)
    {
        var order = side.ToList();
        var k = 0;
        for (var p = 0; p < order.Count; p++)
        {
            if (order[p].Kind == CloneLineKind.Clone)
            {
                order[p] = clones[k++];
            }
        }

        return order;
    }
}
