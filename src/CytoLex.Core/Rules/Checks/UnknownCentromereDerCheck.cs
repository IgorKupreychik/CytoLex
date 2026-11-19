namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ORDER.UNKNOWN_CENTROMERE_DER</c> (§6 ¶3): <c>der(?)</c> after the identified abnormalities, before <c>r</c>, <c>mar</c> and <c>dmin</c>.</summary>
[KbRule("ORDER.UNKNOWN_CENTROMERE_DER")]
internal sealed class UnknownCentromereDerCheck() : OrderCheck(OrderRule.UnknownCentromereDer)
{
    protected override string Detail(string input, OrderViolation v) =>
        $"'{Text(input, v.Item)}' belongs before '{Text(input, v.Before)}' (identified abnormalities, then der(?), then r, mar, dmin).";
}
