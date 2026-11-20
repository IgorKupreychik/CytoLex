namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CLONE.SINGLE_CELL_REPORTING</c> (§11.1.1 ¶3–5, info): a clone seen in one cell is reported only when an earlier
/// study or another method confirms it, which the string can't show. Reported as "can't be verified" on an abnormal clone
/// with <c>[1]</c> (not a composite karyotype). Not checked in <c>mos</c>/<c>chi</c> karyotypes.
/// </summary>
[KbRule("CLONE.SINGLE_CELL_REPORTING")]
internal sealed class SingleCellReportingCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        if (Clones.IsConstitutionalMosaicOrChimera(context.Model))
        {
            return;
        }

        foreach (var l in Clones.Of(context.Input, context.Model).Lines.Where(l => l.Kind == CloneLineKind.Clone && l.Cells == 1 && l.Line.CellCount is { IsComposite: false }))
        {
            context.Report(
                l.Line.CellCount!.Span,
                $"'{l.Line.Span.Of(context.Input)}' is seen in one cell: report it only if an earlier or follow-up study, or another method such as FISH, shows it to be clonal.",
                cannotVerify: true);
        }
    }
}
