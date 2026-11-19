using System.Globalization;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>NUM.PLOIDY_REFERENCE</c> (§8.1 ¶2, §11.2 ¶2): the changes are stated relative to the ploidy level of the count.
/// Fires when the arithmetic fails at the count's level but fits at another one.
/// </summary>
[KbRule("NUM.PLOIDY_REFERENCE")]
internal sealed class PloidyReferenceCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var r in ChromosomeCount.Analyze(context.Model).Where(r => r.Verdict == CountVerdict.OtherPloidyLevel))
        {
            var detail = string.Create(
                CultureInfo.InvariantCulture,
                $"A count of {r.Line.Count!.Span.Of(context.Input)} is {ChromosomeCount.LevelName(r.Level)} (reference {23 * r.Level}), but the changes are stated relative to {23 * r.OtherLevel!.Value}.");
            context.Report(r.CountSpan, detail);
        }
    }
}
