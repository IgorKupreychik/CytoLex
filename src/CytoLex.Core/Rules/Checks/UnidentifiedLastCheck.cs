namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ORDER.UNIDENTIFIED_LAST</c> (§6 ¶2): unidentified <c>r</c>, then <c>mar</c>, then <c>dmin</c>, after all other abnormalities.</summary>
[KbRule("ORDER.UNIDENTIFIED_LAST")]
internal sealed class UnidentifiedLastCheck() : OrderCheck(OrderRule.UnidentifiedLast)
{
    protected override string Detail(string input, OrderViolation v) =>
        $"'{Text(input, v.Item)}' belongs before '{Text(input, v.Before)}' (unidentified r, mar and dmin come last, in that order).";
}
