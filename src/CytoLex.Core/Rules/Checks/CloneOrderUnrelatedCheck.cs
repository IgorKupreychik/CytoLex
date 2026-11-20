namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CLONE.ORDER_UNRELATED</c> (§11.1.6 ¶1–3, a warning: ¶3 lets an abnormality identified earlier come first, which a
/// string can't show): related clones come first; unrelated clones follow by decreasing size, ties with sex chromosome
/// abnormalities first, then by the lowest autosome. Size order is checked only when every unrelated clone has a cell
/// count. Jumping translocations are <c>TRANSLOC.JUMPING</c>'s (one fault, one finding); normal and constitutional-only
/// lines are <c>CLONE.NORMAL_LAST</c>'s. Fix: one reorder of the clone positions. Not checked in <c>mos</c>/<c>chi</c>
/// karyotypes or on sides with recovered input.
/// </summary>
[KbRule("CLONE.ORDER_UNRELATED")]
internal sealed class CloneOrderUnrelatedCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        if (Clones.IsConstitutionalMosaicOrChimera(context.Model))
        {
            return;
        }

        var clones = Clones.Of(context.Input, context.Model);
        foreach (var side in clones.Sides.Where(s => !Clones.IsPartial(s)))
        {
            var (related, unrelated) = clones.Groups(side);
            if (unrelated.IsEmpty || UnrelatedClones.IsJumping(related, unrelated))
            {
                continue;
            }

            var sized = unrelated.All(u => u.Cells is not null);
            var sorted = sized ? Clones.StableSort(unrelated, UnrelatedClones.Key, Comparer<(int, int, int)>.Default) : [.. unrelated];
            var fix = clones.Reorder(side, UnrelatedClones.Refill(side, [.. related, .. sorted]));
            for (var j = 0; j < unrelated.Length; j++)
            {
                var u = unrelated[j];
                if (related.FirstOrDefault(r => r.Index > u.Index) is { } later)
                {
                    context.ReportWithFix(u.Line.Span, $"'{clones.Text(u)}' is unrelated to the other clones: the related clones ('{clones.Text(later)}') come first.", fix, [later.Line.Span]);
                    continue;
                }

                if (!sized)
                {
                    continue;
                }

                var key = UnrelatedClones.Key(u);
                var before = unrelated.Take(j).FirstOrDefault(e => key.CompareTo(UnrelatedClones.Key(e)) < 0);
                if (before is not null)
                {
                    var why = u.Cells == before.Cells
                        ? "with equal sizes, sex chromosome abnormalities come first, then the lower autosome"
                        : $"the larger clone ({u.Cells} cells) comes before the smaller one ({before.Cells} cells), unless the latter was identified earlier";
                    context.ReportWithFix(u.Line.Span, $"'{clones.Text(u)}' belongs before '{clones.Text(before)}': {why}.", fix, [before.Line.Span]);
                }
            }
        }
    }
}
