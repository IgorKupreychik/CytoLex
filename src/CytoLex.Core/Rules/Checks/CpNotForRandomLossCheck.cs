namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CP.NOT_FOR_RANDOM_LOSS</c> (§11.1.5 ¶1, E7; info): cells with only random loss are left out of a composite
/// karyotype. The single cells are not in the string, so it is reported as "can't be verified" on every <c>[cp…]</c>.
/// </summary>
[KbRule("CP.NOT_FOR_RANDOM_LOSS")]
internal sealed class CpNotForRandomLossCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var cells in context.Model.CellLines.Select(l => l.CellCount).OfType<Semantics.CellCount>().Where(c => c.IsComposite))
        {
            context.Report(cells.Span, "A composite karyotype leaves out cells with only random (nonclonal) losses; the string can't show which cells were included.", cannotVerify: true);
        }
    }
}
