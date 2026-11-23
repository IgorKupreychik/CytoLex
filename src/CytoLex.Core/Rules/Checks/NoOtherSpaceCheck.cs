using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SYM.NO_OTHER_SPACE</c> (§3 ¶1, §4.1 ¶1–2; T-073 decision): a space stands only where a rule prescribes one. Reports
/// each whitespace run between two tokens of the segment (<c>47,XY, +21</c>, <c>46,X Y</c>, <c>(q13 q33)</c>) except:
/// <list type="bullet">
/// <item>around <c>or</c> between alternative readings (§4.2, §5.3);</item>
/// <item>between two listed symbols (<c>psu dic</c>, <c>+mar c</c>, <c>+mar dn</c>: SYM.SPACE_BETWEEN_SYMBOLS,
/// MAR.CONSTITUTIONAL_C_SPACE, KARYO.DE_NOVO_SPACE);</item>
/// <item>between a symbol and the chromosome count (<c>mos 47</c>: SYM.SPACE_BEFORE_CHROMOSOME_COUNT);</item>
/// <item>arr: after the genome build, or after <c>arr</c> where the build is missing (GEN.GENOME_BUILD_REQUIRED), and
/// before an inheritance or zygosity symbol (ARR.INHERITANCE_SPACING, ARR.ZYGOSITY, which also judge a space there);</item>
/// <item>in situ (T-033): between and after the technique words (<c>nuc ish 21q22(…)</c>; a space before the parenthesis of
/// the short form is NUC_ISH.SIGNAL_COUNT_FORMAT's or ISH.NORMAL_CHROMOSOME_FORMAT's), around <c>con</c>/<c>sep</c> and
/// before <c>amp</c>/<c>dim</c>/<c>enh</c>.</item>
/// </list>
/// A space another rule reports (SYM.NO_SPACE_AT_PARENTHESIS, BAND.DESIGNATION_FORMAT) is left to it. Whitespace at
/// the edges of a segment is not reported.
/// </summary>
[KbRule("SYM.NO_OTHER_SPACE")]
internal sealed class NoOtherSpaceCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var root = context.Syntax.Root;
        var claimed = NoSpaceAtParenthesisCheck.Gaps(context).Select(g => g.Gap)
            .Concat(BandDesignationFormatCheck.Designations(root).SelectMany(d => d.Gaps).Select(e => e.Span))
            .ToHashSet();
        var countStarts = root.DescendantNodesAndSelf()
            .Where(n => n.Kind == SyntaxKind.ChromosomeCount)
            .Select(n => n.Span.Start)
            .ToHashSet();
        // arr: the space after the genome build is prescribed; without a build, the space after "arr" is where the build
        // goes (GEN.GENOME_BUILD_REQUIRED inserts it), not a stray one.
        var afterBuild = context.Arr switch
        {
            { Build: { IsPartial: false } build } => build.CloseBracket.End,
            { Build: null } arr => arr.Keyword.End,
            _ => -1,
        };

        var inSitu = root.MisplacedInSituStart() ?? int.MaxValue;
        var tokens = root.PresentTokens();
        for (var i = 0; i + 1 < tokens.Count && tokens[i + 1].Span.Start < inSitu; i++)
        {
            var (left, right) = (tokens[i], tokens[i + 1]);
            if (!left.HasTrailingTrivia || right.Kind == TokenKind.EndOfSegment)
            {
                continue;
            }

            var gap = TextSpan.FromBounds(left.Span.End, right.Span.Start);
            if (claimed.Contains(gap) || IsPrescribed(context, left, right, countStarts, afterBuild))
            {
                continue;
            }

            context.ReportWithFix(gap, $"No space between '{left.WrittenText}' and '{right.WrittenText}'.", FixProposal.Delete(gap), [left.Span, right.Span]);
        }
    }

    private static bool IsPrescribed(RuleContext context, SyntaxToken left, SyntaxToken right, HashSet<int> countStarts, int afterBuild) =>
        IsWord(left, "or") || IsWord(right, "or")
        || (IsSymbol(left) && (IsSymbol(right) || countStarts.Contains(right.Span.Start)))
        || (context.Arr is not null && (left.Span.End == afterBuild || (right.Kind == TokenKind.Word && Parser.ArrQualifierWords.Contains(right.Text))))
        || (context.Fish is { } fish && (left.Span.End <= fish.Keyword.End
            || IsWordIn(left, LocusGrammar.PositionWords) || IsWordIn(right, LocusGrammar.PositionWords) || IsWordIn(right, LocusGrammar.QualifierWords)));

    private static bool IsWordIn(SyntaxToken t, System.Collections.Frozen.FrozenSet<string> words) => t.Kind == TokenKind.Word && words.Contains(t.Text);

    private static bool IsWord(SyntaxToken t, string word) => t.Kind == TokenKind.Word && t.Text == word;

    /// <summary>A listed symbol or abbreviation (<c>psu</c>, <c>mar</c>, <c>c</c>, <c>mos</c>).</summary>
    private static bool IsSymbol(SyntaxToken t) => t.Kind == TokenKind.Word && t.VocabRef is not null;
}
