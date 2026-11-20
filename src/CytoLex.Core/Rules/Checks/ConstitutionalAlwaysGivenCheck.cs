namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>KARYO.CONSTITUTIONAL_ALWAYS_GIVEN</c> (§11.3 ¶3, info): in a tumour karyotype a constitutional anomaly is always
/// given, even when no tumour cell still has it. An omitted one can't be seen in the string, so the rule is reported as
/// "can't be verified" once per karyotype, at the first constitutional mark (<c>c</c>).
/// </summary>
[KbRule("KARYO.CONSTITUTIONAL_ALWAYS_GIVEN")]
internal sealed class ConstitutionalAlwaysGivenCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        if (context.Model.DescendantsAndSelf().OfType<Semantics.ConstitutionalMark>().FirstOrDefault() is { } mark)
        {
            context.Report(mark.Span, "Every constitutional anomaly is given, and the acquired changes are stated relative to it; the string can't show whether one was left out.", cannotVerify: true);
        }
    }
}
