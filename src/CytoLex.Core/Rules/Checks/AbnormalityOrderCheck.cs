namespace CytoLex.Core.Rules.Checks;

/// <summary><c>KARYO.ABNORMALITY_ORDER</c> (§4.1 ¶2, §6 ¶1): sex chromosome abnormalities first, then autosomes by number. The missing comma is the parser's.</summary>
[KbRule("KARYO.ABNORMALITY_ORDER")]
internal sealed class AbnormalityOrderCheck() : OrderCheck(OrderRule.ChromosomeNumber)
{
    protected override string Detail(string input, OrderViolation v) =>
        $"'{Text(input, v.Item)}' belongs before '{Text(input, v.Before)}' (sex chromosomes first, then autosomes in numerical order).";
}
