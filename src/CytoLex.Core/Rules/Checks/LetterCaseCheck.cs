using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SYM.LETTER_CASE</c> (§3 ¶1, §4.1 ¶1; T-073 decision): symbols are written in the case of the ch. 3 list and the sex
/// chromosomes as X and Y. Reports the tokens the lexer and parser read in their listed case
/// (<see cref="SyntaxToken.IsCaseFolded"/>: <c>Del</c>, <c>T</c>, <c>xy</c>, <c>45,x</c>, <c>I(17)</c>); adjacent ones
/// form one finding (<c>xy</c> → <c>XY</c>). A form that matches no listed token, or two, stays SYM.LISTED_ONLY.
/// </summary>
[KbRule("SYM.LETTER_CASE")]
internal sealed class LetterCaseCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var tokens = context.Syntax.Root.PresentTokens();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!tokens[i].IsCaseFolded)
            {
                continue;
            }

            var first = i;
            while (i + 1 < tokens.Count && tokens[i + 1].IsCaseFolded && !tokens[i].HasTrailingTrivia && tokens[i].Span.End == tokens[i + 1].Span.Start)
            {
                i++;
            }

            var span = TextSpan.FromBounds(tokens[first].Span.Start, tokens[i].Span.End);
            var listed = string.Concat(tokens.Skip(first).Take(i - first + 1).Select(t => t.Text));
            context.ReportWithFix(span, $"'{span.Of(context.Input)}' is written {listed}.", FixProposal.Replace(span, listed));
        }
    }
}
