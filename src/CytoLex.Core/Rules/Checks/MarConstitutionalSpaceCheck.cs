using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary><c>MAR.CONSTITUTIONAL_C_SPACE</c> (§8.2 E20): a space separates <c>mar</c> from its constitutional <c>c</c> (<c>+mar c</c>).</summary>
[KbRule("MAR.CONSTITUTIONAL_C_SPACE")]
internal sealed class MarConstitutionalSpaceCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var a in context.Model.DescendantsAndSelf().OfType<Abnormality>())
        {
            if (a.Constitutional is { } mark && !a.Parts.IsEmpty && a.Parts[^1].Head == "mar"
                && mark.Span.Start > 0 && !char.IsWhiteSpace(context.Input[mark.Span.Start - 1]))
            {
                context.Report(Text.TextSpan.FromBounds(a.Parts[^1].Span.Start, mark.Span.End), "A space separates mar from c: mar c.");
            }
        }
    }
}
