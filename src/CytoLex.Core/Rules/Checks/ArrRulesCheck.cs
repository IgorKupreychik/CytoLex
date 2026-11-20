namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// The arr rules (ISCN 2016 ch. 14, T-034) that read one arr result: one shared analysis (<see cref="ArrAnalyses"/>),
/// each binding reporting the findings of its own rule. Runs on arr segments only (the rules' <c>applies_to</c>).
/// </summary>
[KbRule(ArrAnalyses.GenomeBuild)]
[KbRule(ArrAnalyses.NucleotideFormat)]
[KbRule(ArrAnalyses.NormalResult)]
[KbRule(ArrAnalyses.AberrationsOnly)]
[KbRule(ArrAnalyses.AberrationOrder)]
[KbRule(ArrAnalyses.RegionFormat)]
[KbRule(ArrAnalyses.PterToQter)]
[KbRule(ArrAnalyses.CopyNumber)]
[KbRule(ArrAnalyses.MosaicProportion)]
[KbRule(ArrAnalyses.InheritanceSpacing)]
[KbRule(ArrAnalyses.Zygosity)]
[KbRule(ArrAnalyses.ComplexCx)]
[KbRule(ArrAnalyses.Chromothripsis)]
internal sealed class ArrRulesCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        if (context.Arr is not { } model)
        {
            return;
        }

        foreach (var f in ArrAnalyses.Findings(model, context.Input).Where(f => f.Rule == context.Rule.Id))
        {
            context.ReportWithFix(f.Span, f.Detail, f.Fix, f.SecondarySpans);
        }
    }
}
