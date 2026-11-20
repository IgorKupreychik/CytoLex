namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CLONE.ORDER_RELATED</c> (§11.1.2 ¶1, §11.1.4 ¶1): related clones are written in order of increasing complexity, the
/// stemline first. Fires where the relation is visible: a clone written in full that has every change of a later clone
/// written in full, and more. A clone written with <c>idem</c>/<c>sl</c>/<c>sdl</c> follows its stemline by
/// construction (a reference on the first line is <c>CLONE.SUBCLONE_REFERENCE</c>). Fix: move the more basic clone before
/// the other. Not checked in <c>mos</c>/<c>chi</c> karyotypes or on sides with recovered input.
/// </summary>
[KbRule("CLONE.ORDER_RELATED")]
internal sealed class CloneOrderRelatedCheck : IRuleCheck
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
            for (var j = 1; j < side.Length; j++)
            {
                var basic = side[j];
                var i = Enumerable.Range(0, j).FirstOrDefault(k => clones.IsStrictSuperset(side[k], basic), -1);
                if (i < 0)
                {
                    continue;
                }

                var order = side.ToList();
                order.RemoveAt(j);
                order.Insert(i, basic);
                context.ReportWithFix(
                    basic.Line.Span,
                    $"'{clones.Text(side[i])}' has every change of '{clones.Text(basic)}' and more: the more basic clone comes first.",
                    clones.Reorder(side, order),
                    [side[i].Line.Span]);
            }
        }
    }
}
