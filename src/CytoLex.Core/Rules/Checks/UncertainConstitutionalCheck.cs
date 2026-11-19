using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;

namespace CytoLex.Core.Rules.Checks;

/// <summary><c>KARYO.UNCERTAIN_CONSTITUTIONAL</c> (§8.2 E19): an uncertain constitutional mark is written <c>?c</c>, not <c>c?</c>.</summary>
[KbRule("KARYO.UNCERTAIN_CONSTITUTIONAL")]
internal sealed class UncertainConstitutionalCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var mark in context.Model.DescendantsAndSelf().OfType<ConstitutionalMark>())
        {
            var tokens = mark.Syntax?.PresentTokens() ?? [];
            if (tokens.Count >= 2 && tokens[^1].Kind == TokenKind.Question && tokens[^2] is { Kind: TokenKind.Word, Text: "c" })
            {
                context.Report(mark.Span, "The question mark goes before c: ?c.");
            }
        }
    }
}
