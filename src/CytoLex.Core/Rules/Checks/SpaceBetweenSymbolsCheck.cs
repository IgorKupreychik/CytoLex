using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SYM.SPACE_BETWEEN_SYMBOLS</c> (§3 ¶1): two symbols used together are separated by a space (<c>psu dic</c>, not
/// <c>psudic</c>). Covers the symbol words of an abnormality; suffixes (<c>mardn</c>, <c>marc</c>) have rules of their
/// own, and variant features are written together by design (<c>13cenh+</c>). <c>dicr</c>/<c>trcr</c> are
/// <c>RING.DIC_TRC_ORDER</c>'s (<see cref="ChromosomeOrder"/>).
/// </summary>
[KbRule("SYM.SPACE_BETWEEN_SYMBOLS")]
internal sealed class SpaceBetweenSymbolsCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var part in context.SegmentModel.DescendantsAndSelf().OfType<AbnormalityPart>())
        {
            for (var i = 0; i + 1 < part.Symbols.Length; i++)
            {
                var (first, second) = (part.Symbols[i], part.Symbols[i + 1]);
                if (first.Span.End == second.Span.Start && !(first.Symbol is "dic" or "trc" && second.Symbol == "r"))
                {
                    context.ReportWithFix(
                        TextSpan.FromBounds(first.Span.Start, second.Span.End),
                        $"'{first.Symbol}' and '{second.Symbol}' are separated by a space.",
                        FixProposal.Insert(first.Span.End, " "));
                }
            }
        }
    }
}
