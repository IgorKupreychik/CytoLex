using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SYM.NO_SPACE_AT_PARENTHESIS</c> (§3 ¶1): no space between a symbol and a parenthesis directly before or after it
/// (<c>del (5)</c>, <c>der(1) t(1;3)</c>, <c>( p13)</c>). Looks inside abnormalities only: an <c>or</c> between two
/// readings (<c>del(8)(q21.1) or i(8)(p10)</c>) is written with spaces. In an arr result (T-034, §14.2 ¶1) it covers
/// <c>arr (1–22,X)</c> and the result after a parenthesis (<c>(X,Y) ×1</c>, <c>(1–22) cx</c>); a qualifier after a
/// parenthesis (<c>) dn</c>, <c>) hmz</c>) is <c>ARR.INHERITANCE_SPACING</c>'s or <c>ARR.ZYGOSITY</c>'s.
/// </summary>
[KbRule("SYM.NO_SPACE_AT_PARENTHESIS")]
internal sealed class NoSpaceAtParenthesisCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var (gap, left, right) in Gaps(context))
        {
            context.ReportWithFix(gap, $"No space between '{left.Text}' and '{right.Text}'.", FixProposal.Delete(gap), [left.Span, right.Span]);
        }
    }

    /// <summary>Each space this rule reports, with the tokens around it (also read by <see cref="NoOtherSpaceCheck"/>).</summary>
    internal static IEnumerable<(TextSpan Gap, SyntaxToken Left, SyntaxToken Right)> Gaps(RuleContext context)
    {
        if (context.Arr is { } arr)
        {
            return arr.Syntax is { } root
                ? Gaps(root.PresentTokens(), skip: t => t.Kind == TokenKind.Word && (t.Text == "or" || Parser.ArrQualifierWords.Contains(t.Text)))
                : [];
        }

        return context.SegmentModel.DescendantsAndSelf().OfType<Abnormality>()
            .Where(a => a.Syntax is not null)
            .SelectMany(a => Gaps(a.Syntax!.PresentTokens(), skip: _ => false));
    }

    private static IEnumerable<(TextSpan Gap, SyntaxToken Left, SyntaxToken Right)> Gaps(List<SyntaxToken> tokens, Func<SyntaxToken, bool> skip)
    {
        for (var i = 0; i + 1 < tokens.Count; i++)
        {
            var (left, right) = (tokens[i], tokens[i + 1]);
            if (left.HasTrailingTrivia && !skip(left) && !skip(right)
                && ((IsSymbol(left) && IsParen(right)) || (IsParen(left) && IsSymbol(right))))
            {
                yield return (TextSpan.FromBounds(left.Span.End, right.Span.Start), left, right);
            }
        }
    }

    private static bool IsParen(SyntaxToken t) => t.Kind is TokenKind.OpenParen or TokenKind.CloseParen;

    /// <summary>A listed symbol or abbreviation word (<c>del</c>, <c>t</c>, <c>p</c>) or the multiplication sign.</summary>
    private static bool IsSymbol(SyntaxToken t) =>
        (t.Kind == TokenKind.Word && t.VocabRef is not null) || t.Kind == TokenKind.Multiply;
}
