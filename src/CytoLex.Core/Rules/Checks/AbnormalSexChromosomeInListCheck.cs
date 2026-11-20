using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST</c> (§9.1 E8, §9.2.17.1 E3): a rearranged X or Y is not written in the sex
/// chromosome field. Fires when the count fits only with the rearranged X/Y also counted in the field
/// (<c>46,XY,t(X;8)</c>).
/// </summary>
[KbRule("SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST")]
internal sealed class AbnormalSexChromosomeInListCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var r in ChromosomeCount.Analyze(context.Model).Where(r => r.Verdict == CountVerdict.RearrangedSexInField))
        {
            var letters = string.Join(", ", r.RearrangedSex.Select(s => s.Letter).Distinct());
            var items = string.Join(", ", r.RearrangedSex.Select(s => s.Item.Span.Of(context.Input)).Distinct());
            context.Report(
                r.Line.Sex!.Span,
                $"The sex chromosome field lists only normal sex chromosomes; the {letters} in {items} is written there too.",
                [.. r.RearrangedSex.Select(s => s.Item.Span).Distinct()],
                fixes: Fix(r) is { } fix ? [fix] : []);
        }
    }

    /// <summary><c>46,XY,t(X;8)</c> → <c>46,Y,t(X;8)</c>: one letter leaves the field per rearranged X or Y.</summary>
    private static FixProposal? Fix(CountResult r)
    {
        var sex = r.Line.Sex!;
        if (!FixHelpers.IsPlainSexField(sex, r.Line))
        {
            return null;
        }

        var letters = sex.Chromosomes.ToList();
        foreach (var s in r.RearrangedSex)
        {
            letters.RemoveAt(letters.LastIndexOf(s.Letter));
        }

        return letters.Count == 0 ? null : FixProposal.Replace(sex.Span, string.Concat(letters));
    }
}
