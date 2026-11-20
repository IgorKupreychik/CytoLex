using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>KARYO.CLONE_ORDER_CONSTITUTIONAL</c> (§4.1 ¶9): a normal diploid line comes last; abnormal lines by decreasing size,
/// ties with a sex chromosome abnormality first, then numerical before structural, then the lower autosome; in chimeras
/// too the largest line comes first (two normal lines: <c>chi 46,XX[25]/46,XY[10]</c>). Size order is checked only where
/// every line of the group has a cell count. Each side of <c>//</c> is ordered separately. Not checked when a line uses
/// <c>idem</c>/<c>sl</c>/<c>sdl</c> (a tumour) or on sides with recovered input. Fix: one reorder.
/// </summary>
[KbRule("KARYO.CLONE_ORDER_CONSTITUTIONAL")]
internal sealed class CloneOrderConstitutionalCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var clones = Clones.Of(context.Input, context.Model);
        if (clones.Lines.Any(l => l.IsReference))
        {
            return;
        }

        foreach (var side in clones.Sides.Where(s => !Clones.IsPartial(s)))
        {
            var abnormal = side.Where(l => l.Kind != CloneLineKind.NormalDiploid).ToList();
            var normal = side.Where(l => l.Kind == CloneLineKind.NormalDiploid).ToList();
            var abnormalSized = abnormal.All(l => l.Cells is not null);
            var normalSized = normal.All(l => l.Cells is not null);
            var fix = clones.Reorder(side, [
                .. abnormalSized ? Clones.StableSort(abnormal, Key, Comparer<(int, int, int, int)>.Default) : abnormal,
                .. normalSized ? Clones.StableSort(normal, l => -l.Cells!.Value, Comparer<int>.Default) : normal,
            ]);

            var lastAbnormal = abnormal.LastOrDefault();
            foreach (var l in side)
            {
                if (l.Kind == CloneLineKind.NormalDiploid && lastAbnormal is not null && l.Index < lastAbnormal.Index)
                {
                    context.ReportWithFix(l.Line.Span, $"'{clones.Text(l)}' is the normal diploid line: it is listed last.", fix, [lastAbnormal.Line.Span]);
                    continue;
                }

                var group = l.Kind == CloneLineKind.NormalDiploid ? normal : abnormal;
                if (!(l.Kind == CloneLineKind.NormalDiploid ? normalSized : abnormalSized))
                {
                    continue;
                }

                var before = group.TakeWhile(e => !ReferenceEquals(e, l)).FirstOrDefault(e => Compare(l, e) < 0);
                if (before is not null)
                {
                    var why = l.Cells == before.Cells
                        ? "with equal sizes, a sex chromosome abnormality comes first, then a numerical before a structural one, then the lower autosome"
                        : $"the larger line ({l.Cells} cells) comes before the smaller one ({before.Cells} cells)";
                    context.ReportWithFix(l.Line.Span, $"'{clones.Text(l)}' belongs before '{clones.Text(before)}': {why}.", fix, [before.Line.Span]);
                }
            }
        }
    }

    private static int Compare(CloneLine a, CloneLine b) =>
        a.Kind == CloneLineKind.NormalDiploid ? (b.Cells ?? 0).CompareTo(a.Cells ?? 0) : Key(a).CompareTo(Key(b));

    /// <summary>Larger first; ties: sex chromosome abnormality, numerical before structural, lower autosome (§4.1 ¶9).</summary>
    private static (int Cells, int Sex, int Numerical, int Autosome) Key(CloneLine l)
    {
        var changes = l.Line.Items.Where(i => i is not OmittedItems).ToList();
        return (-(l.Cells ?? 0), Clones.HasSexAbnormality(l, changes) ? 0 : 1, Clones.IsNumerical(changes) ? 0 : 1, Clones.LowestAutosome(changes));
    }
}
