namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ORDER.X_BEFORE_Y</c> (§6 ¶1): X chromosome abnormalities before Y abnormalities.</summary>
[KbRule("ORDER.X_BEFORE_Y")]
internal sealed class XBeforeYCheck() : OrderCheck(OrderRule.XBeforeY)
{
    protected override string Detail(string input, OrderViolation v) =>
        $"'{Text(input, v.Item)}' (X) belongs before '{Text(input, v.Before)}' (Y).";
}
