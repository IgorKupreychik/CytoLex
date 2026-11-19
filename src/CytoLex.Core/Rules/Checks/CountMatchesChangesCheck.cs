using System.Globalization;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>NUM.COUNT_MATCHES_CHANGES</c> (§8.1 ¶2–3): the count equals the reference level plus gains minus losses
/// (<see cref="ChromosomeCount"/>). Error when the arithmetic is certain, else uncertain (warning). A mismatch that a more
/// precise rule explains is left to that rule.
/// </summary>
[KbRule("NUM.COUNT_MATCHES_CHANGES")]
internal sealed class CountMatchesChangesCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var r in ChromosomeCount.Analyze(context.Model).Where(r => r.Verdict == CountVerdict.Mismatch))
        {
            var detail = string.Create(
                CultureInfo.InvariantCulture,
                $"The listed changes give {ChromosomeCount.Describe(r.Expected)} chromosomes at the {ChromosomeCount.LevelName(r.Level)} level, but the count is {r.Line.Count!.Span.Of(context.Input)}.");
            context.Report(r.CountSpan, detail, certainty: r.Certain ? Certainty.Certain : Certainty.Uncertain);
        }
    }
}
