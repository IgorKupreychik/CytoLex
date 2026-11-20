using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// The order rules share one analysis (<see cref="ItemOrder"/>); each check reports the violations of its own rule, so
/// an out-of-order item gets exactly one finding.
/// </summary>
internal abstract class OrderCheck(OrderRule rule) : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var v in ItemOrder.Violations(context.Model, context.Input).Where(v => v.Rule == rule))
        {
            context.ReportWithFix(v.Item.Span, Detail(context.Input, v), v.Fix, [v.Before.Span]);
        }
    }

    protected abstract string Detail(string input, OrderViolation violation);

    protected static string Text(string input, MeaningNode node) => node.Span.Of(input);
}
