namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ORDER.NUMERICAL_BEFORE_STRUCTURAL</c> (§6 ¶1): per chromosome, the numerical change before the structural ones.</summary>
[KbRule("ORDER.NUMERICAL_BEFORE_STRUCTURAL")]
internal sealed class NumericalBeforeStructuralCheck() : OrderCheck(OrderRule.NumericalBeforeStructural)
{
    protected override string Detail(string input, OrderViolation v) =>
        $"The numerical change '{Text(input, v.Item)}' belongs before the structural change '{Text(input, v.Before)}'.";
}
