using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// The order of in situ loci from where the probes lie (T-103, <see cref="ProbeOrder"/>): a finding that relies on a
/// probe table row nobody has reviewed is reported as uncertain (at most a warning) and names the row's source.
/// </summary>
[KbRule(ProbeOrder.LocusOrder)]
[KbRule(ProbeOrder.ChromosomeOrder)]
internal sealed class ProbeOrderCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        if (context.Fish is not { } model)
        {
            return;
        }

        foreach (var f in ProbeOrder.Findings(model, context.Kb.Probes).Where(f => f.Rule == context.Rule.Id))
        {
            context.Report(f.Span, f.Detail, [f.Other], f.Unreviewed ? Certainty.Uncertain : Certainty.Certain);
        }
    }
}
