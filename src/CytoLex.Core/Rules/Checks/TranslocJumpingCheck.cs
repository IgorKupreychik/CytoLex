namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>TRANSLOC.JUMPING</c> (§9.2.17.4): jumping translocations, one clone per translocation, are listed by decreasing
/// frequency. Recognised as unrelated clones that are each a single <c>t(a;b)</c> sharing one chromosome
/// (<see cref="UnrelatedClones.IsJumping"/>); the order is checked when every clone has a cell count. An error, unlike
/// <c>CLONE.ORDER_UNRELATED</c>, which steps back for these strings. Fix: a stable sort by decreasing size.
/// </summary>
[KbRule("TRANSLOC.JUMPING")]
internal sealed class TranslocJumpingCheck : IRuleCheck
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
            if (!UnrelatedClones.IsJumping(related, unrelated) || unrelated.Any(u => u.Cells is null))
            {
                continue;
            }

            var sorted = Clones.StableSort(unrelated, u => -u.Cells!.Value, Comparer<int>.Default);
            var fix = clones.Reorder(side, UnrelatedClones.Refill(side, sorted));
            for (var j = 1; j < unrelated.Length; j++)
            {
                var u = unrelated[j];
                if (unrelated.Take(j).FirstOrDefault(e => e.Cells < u.Cells) is { } before)
                {
                    context.ReportWithFix(u.Line.Span, $"'{clones.Text(u)}' ({u.Cells} cells) belongs before '{clones.Text(before)}' ({before.Cells} cells): jumping translocations are listed by decreasing frequency.", fix, [before.Line.Span]);
                }
            }
        }
    }
}
