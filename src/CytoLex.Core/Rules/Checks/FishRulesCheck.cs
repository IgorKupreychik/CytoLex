namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// The in situ rules (ISCN 2016 ch. 13, T-033) that read one ish, nuc ish, fib ish, rev ish or cgh segment: one shared
/// analysis (<see cref="FishAnalyses"/>), each binding reporting the findings of its own rule. Runs on the segments the
/// rules apply to (<c>ISH.COMBINED_WITH_KARYOTYPE</c> is general: it reads in situ segments only).
/// </summary>
[KbRule(FishAnalyses.CombinedWithKaryotype)]
[KbRule(FishAnalyses.LocusDesignation)]
[KbRule(FishAnalyses.LocusList)]
[KbRule(FishAnalyses.StructuralFormat)]
[KbRule(FishAnalyses.LocusStatus)]
[KbRule(FishAnalyses.AbnormalSignalCount)]
[KbRule(FishAnalyses.NormalChromosomeFormat)]
[KbRule(FishAnalyses.InversionSeparateBreakpoints)]
[KbRule(FishAnalyses.CellLines)]
[KbRule(FishAnalyses.SignalQualifiers)]
[KbRule(FishAnalyses.PartialPaint)]
[KbRule(FishAnalyses.FibFormat)]
[KbRule(FishAnalyses.RevFormat)]
[KbRule(FishAnalyses.CghFormat)]
[KbRule(NucIshAnalyses.SignalCountFormat)]
[KbRule(NucIshAnalyses.DetailedFormat)]
[KbRule(NucIshAnalyses.SharedCount)]
[KbRule(NucIshAnalyses.CellsScored)]
[KbRule(NucIshAnalyses.AbnormalOverTotal)]
[KbRule(NucIshAnalyses.SignalPositionFormat)]
internal sealed class FishRulesCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        if (context.Fish is not { } model)
        {
            return;
        }

        foreach (var f in FishAnalyses.Findings(model, context.Input, context.Context.Effective).Where(f => f.Rule == context.Rule.Id))
        {
            context.ReportWithFix(f.Span, f.Detail, f.Fix);
        }
    }
}
