namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ORDER.HOMOLOGUES_ALPHABETICAL</c> (§6 ¶1): structural changes of homologues in alphabetical order of their abbreviations.</summary>
[KbRule("ORDER.HOMOLOGUES_ALPHABETICAL")]
internal sealed class HomologuesAlphabeticalCheck() : OrderCheck(OrderRule.HomologuesAlphabetical)
{
    protected override string Detail(string input, OrderViolation v) =>
        $"'{Text(input, v.Item)}' belongs before '{Text(input, v.Before)}' (alphabetical order of the abbreviations).";
}
