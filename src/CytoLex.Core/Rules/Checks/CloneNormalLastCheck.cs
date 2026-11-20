namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CLONE.NORMAL_LAST</c> (§11.1.6 ¶1, §11.3 ¶1–2): in a tumour karyotype the normal diploid line and lines with only
/// constitutional anomalies come last, whatever their size. Fires on such a line followed by an abnormal clone on the
/// same side of <c>//</c>. Fix: move those lines to the end, in their order. In a constitutional reading the same fault
/// is <c>KARYO.CLONE_ORDER_CONSTITUTIONAL</c>'s (each rule reports in its own context). Not checked in <c>mos</c>/<c>chi</c>
/// karyotypes or on sides with recovered input.
/// </summary>
[KbRule("CLONE.NORMAL_LAST")]
internal sealed class CloneNormalLastCheck : IRuleCheck
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
            var lastClone = side.LastOrDefault(l => l.Kind == CloneLineKind.Clone);
            if (lastClone is null)
            {
                continue;
            }

            var fix = clones.Reorder(side, [.. side.Where(l => l.Kind == CloneLineKind.Clone), .. side.Where(l => l.Kind != CloneLineKind.Clone)]);
            foreach (var l in side.Where(l => l.Kind != CloneLineKind.Clone && l.Index < lastClone.Index))
            {
                var what = l.Kind == CloneLineKind.NormalDiploid ? "the normal diploid line" : "a line with only constitutional anomalies";
                context.ReportWithFix(l.Line.Span, $"'{clones.Text(l)}' is {what}: it is listed after the abnormal clones ('{clones.Text(lastClone)}').", fix, [lastClone.Line.Span]);
            }
        }
    }
}
