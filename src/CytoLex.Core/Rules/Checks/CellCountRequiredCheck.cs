namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CLONE.CELL_COUNT_REQUIRED</c> (§11.1.2 ¶1): the number of cells of a clone follows it in square brackets. Fires on a
/// line without <c>[n]</c> when another line of the karyotype has one, or when the line is written with
/// <c>idem</c>/<c>sl</c>/<c>sdl</c> (decided in T-032: a single karyotype without counts is the usual constitutional
/// form, so it is never flagged). Not checked in <c>mos</c>/<c>chi</c> karyotypes. Level-3 fix (the number is unknown).
/// </summary>
[KbRule("CLONE.CELL_COUNT_REQUIRED")]
internal sealed class CellCountRequiredCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var lines = context.Model.CellLines;
        if (Clones.IsConstitutionalMosaicOrChimera(context.Model))
        {
            return;
        }

        var anyCounted = lines.Any(l => l.CellCount is not null);
        foreach (var line in lines.Where(l => l.CellCount is null && !l.IsPartial && (anyCounted || l.BasedOn is not null)))
        {
            context.Report(line.Span, $"'{line.Span.Of(context.Input)}' has no cell count: the number of cells follows each clone in square brackets, e.g. [7].");
        }
    }
}
