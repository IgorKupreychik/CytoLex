namespace CytoLex.Core.Semantics;

/// <summary>
/// The normal-result test of nuc ish locus groups: every locus shows two signals, with no status, position or qualifier
/// (shared by NUC_ISH.ABNORMAL_OVER_TOTAL and the abnormality list, T-092).
/// </summary>
internal static class FishNormal
{
    /// <summary>At least one group, and every locus of every group shows two signals (own or shared count).</summary>
    public static bool AllTwoSignals(IEnumerable<LocusGroup> groups)
    {
        var list = groups.ToList();
        return list.Count > 0 && list.All(g => !g.IsPosition && g.Qualifiers.IsEmpty && g.Loci.All(l =>
            l.Qualifiers.IsEmpty && l.Status.Length == 0 && (l.Count ?? g.SharedCount) is { Low: 2, High: null }));
    }
}
