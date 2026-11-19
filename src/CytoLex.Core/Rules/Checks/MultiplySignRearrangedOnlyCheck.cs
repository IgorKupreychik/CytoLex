using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>KARYO.MULTIPLY_SIGN_REARRANGED_ONLY</c> (§4.1 ¶7, §9.3 ¶1): × shows copies of a rearranged chromosome, not of a
/// normal one (<c>+21×2</c> → <c>+21,+21</c>).
/// </summary>
[KbRule("KARYO.MULTIPLY_SIGN_REARRANGED_ONLY")]
internal sealed class MultiplySignRearrangedOnlyCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var w in context.Model.DescendantsAndSelf().OfType<WholeChromosomeChange>().Where(w => w.Copies is not null))
        {
            context.Report(w.Span, $"Chromosome {w.Chromosome.Name} is a normal chromosome; write each extra copy separately with a sign.");
        }
    }
}
