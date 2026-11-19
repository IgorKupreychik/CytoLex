using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SYM.NO_SPACE_AT_PARENTHESIS</c> (§3 ¶1): no space between a symbol and a parenthesis directly before or after it
/// (<c>del (5)</c>, <c>der(1) t(1;3)</c>, <c>( p13)</c>). Looks inside abnormalities only: an <c>or</c> between two
/// readings (<c>del(8)(q21.1) or i(8)(p10)</c>) is written with spaces.
/// </summary>
[KbRule("SYM.NO_SPACE_AT_PARENTHESIS")]
internal sealed class NoSpaceAtParenthesisCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var abnormality in context.Model.DescendantsAndSelf().OfType<Abnormality>())
        {
            if (abnormality.Syntax is not { } syntax)
            {
                continue;
            }

            var tokens = syntax.PresentTokens();
            for (var i = 0; i + 1 < tokens.Count; i++)
            {
                var (left, right) = (tokens[i], tokens[i + 1]);
                if (left.HasTrailingTrivia && ((IsSymbol(left) && IsParen(right)) || (IsParen(left) && IsSymbol(right))))
                {
                    var gap = TextSpan.FromBounds(left.Span.End, right.Span.Start);
                    context.Report(gap, $"No space between '{left.Text}' and '{right.Text}'.", [left.Span, right.Span]);
                }
            }
        }
    }

    private static bool IsParen(SyntaxToken t) => t.Kind is TokenKind.OpenParen or TokenKind.CloseParen;

    /// <summary>A listed symbol or abbreviation word (<c>del</c>, <c>t</c>, <c>p</c>) or the multiplication sign.</summary>
    private static bool IsSymbol(SyntaxToken t) =>
        (t.Kind == TokenKind.Word && t.VocabRef is not null) || t.Kind == TokenKind.Multiply;
}
