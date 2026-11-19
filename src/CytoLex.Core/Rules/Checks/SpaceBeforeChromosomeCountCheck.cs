using CytoLex.Core.Text;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SYM.SPACE_BEFORE_CHROMOSOME_COUNT</c> (§3 ¶1, §4.1 ¶9): a symbol directly before the chromosome count is separated
/// from it by a space (<c>mos 47,XXX</c>, not <c>mos47,XXX</c>).
/// </summary>
[KbRule("SYM.SPACE_BEFORE_CHROMOSOME_COUNT")]
internal sealed class SpaceBeforeChromosomeCountCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var model = context.Model;
        foreach (var count in model.CellLines.Select(l => l.Count).Where(c => c is { IsMissing: false }))
        {
            foreach (var prefix in model.Prefixes.Where(p => p.Span.End == count!.Span.Start))
            {
                context.Report(
                    TextSpan.FromBounds(prefix.Span.Start, count!.Span.End),
                    $"A space separates '{prefix.Symbol}' from the chromosome count.");
            }
        }
    }
}
