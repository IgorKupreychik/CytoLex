using CytoLex.Core.Syntax;
using CytoLex.Core.Text;

namespace CytoLex.Core.Tests.Parsing;

public class LexerTests
{
    private static readonly Lexer _lexer = new(ParseHelper.Parser2016.Vocabulary);

    private static List<SyntaxToken> Lex(string text) => [.. _lexer.Lex(text, new TextSpan(0, text.Length))];

    private static string Kinds(string text) =>
        string.Join(" ", Lex(text).Where(t => t.Kind != TokenKind.EndOfSegment).Select(t => $"{t.Kind}:{t.Text}"));

    [Fact]
    public void SimpleKaryotype()
    {
        Assert.Equal(
            "Number:46 Comma:, ChromosomeLetter:X ChromosomeLetter:Y Comma:, Word:del OpenParen:( Number:5 CloseParen:) OpenParen:( Word:q Number:13 Word:q Number:33 CloseParen:)",
            Kinds("46,XY,del(5)(q13q33)"));
    }

    [Theory]
    [InlineData("psudic", "Word:psu Word:dic")]
    [InlineData("mardn", "Word:mar Word:dn")]
    [InlineData("marc", "Word:mar Word:c")]
    [InlineData("XXYc", "ChromosomeLetter:X ChromosomeLetter:X ChromosomeLetter:Y Word:c")]
    [InlineData("pstkstk", "Word:p Word:stk Word:stk")]
    [InlineData("cenh", "Word:cen Word:h")]
    [InlineData("pss", "Word:ps Word:s")]
    [InlineData("Xqter", "ChromosomeLetter:X Word:qter")]
    [InlineData("idic", "Word:idic")]
    [InlineData("dicr", "Word:dic Word:r")]
    [InlineData("orq", "Word:or Word:q")]
    [InlineData("GTG", "Word:GTG")]
    public void LetterRuns_SplitIntoListedSymbols(string text, string expected) => Assert.Equal(expected, Kinds(text));

    [Fact]
    public void SplitPieces_AreAdjacent_WithoutTrivia()
    {
        var tokens = Lex("psudic");
        Assert.Equal(new TextSpan(0, 3), tokens[0].Span);
        Assert.Equal(new TextSpan(3, 3), tokens[1].Span);
        Assert.False(tokens[0].HasTrailingTrivia);
    }

    [Theory]
    [InlineData("dl")]
    [InlineData("iso")]
    [InlineData("abr")]
    [InlineData("P")]
    [InlineData("Z")]
    public void UnlistedLetterRuns_AreUnknownWords(string text)
    {
        var t = Assert.Single(Lex(text), t => t.Kind != TokenKind.EndOfSegment);
        Assert.Equal(TokenKind.UnknownWord, t.Kind);
        Assert.Null(t.VocabRef);
    }

    [Theory]
    [InlineData("–", TokenKind.Minus, false)]
    [InlineData("-", TokenKind.Minus, true)]
    [InlineData("−", TokenKind.Minus, true)]
    [InlineData("×", TokenKind.Multiply, false)]
    [InlineData("x", TokenKind.Multiply, true)]
    [InlineData("→", TokenKind.Arrow, false)]
    [InlineData("->", TokenKind.Arrow, true)]
    public void TypedForms_GetThePrintedKind(string text, TokenKind kind, bool typed)
    {
        var t = Lex(text)[0];
        Assert.Equal(kind, t.Kind);
        Assert.Equal(typed, t.IsTypedForm);
        Assert.Equal(text, t.Text);
        Assert.NotNull(t.VocabRef);
    }

    [Fact]
    public void Symbols_CarryTheirVocabRef()
    {
        var tokens = Lex("del(5)");
        Assert.Equal("vocab:symbols/del", tokens[0].VocabRef);
        Assert.Equal("vocab:symbols/parentheses ( )", tokens[1].VocabRef);
        Assert.Null(Lex("9q34.1").Single(t => t.Kind == TokenKind.Dot).VocabRef); // shared token: decimal point or period
    }

    [Fact]
    public void Underlining_StaysInTheToken()
    {
        var t = Lex("fra(1̲0̲)")[2];
        Assert.Equal(TokenKind.Number, t.Kind);
        Assert.True(t.IsUnderlined);
        Assert.Equal("10", t.ValueText);
        Assert.Equal(new TextSpan(4, 4), t.Span);

        var letters = Lex("X̲p");
        Assert.Equal(TokenKind.ChromosomeLetter, letters[0].Kind);
        Assert.Equal("X̲", letters[0].Text);
    }

    [Theory]
    [InlineData("Del", "Word:del")]
    [InlineData("DEL", "Word:del")]
    [InlineData("T", "Word:t")]
    [InlineData("INV", "Word:inv")]
    [InlineData("y", "ChromosomeLetter:Y")]
    [InlineData("xy", "ChromosomeLetter:X ChromosomeLetter:Y")]
    [InlineData("xx", "ChromosomeLetter:X ChromosomeLetter:X")]
    [InlineData("Xy", "ChromosomeLetter:X ChromosomeLetter:Y")]
    public void WrongCase_IsReadAsTheListedForm(string text, string expected)
    {
        Assert.Equal(expected, Kinds(text));
        var tokens = Lex(text);
        Assert.Contains(tokens, t => t.IsCaseFolded);
        Assert.Equal(text, string.Concat(tokens.Select(t => t.ToFullString())));
        Assert.Equal(text, string.Concat(tokens.Select(t => t.WrittenText)));
    }

    [Theory]
    [InlineData("dl", "UnknownWord:dl")] // no listed token in any case
    [InlineData("P", "UnknownWord:P")] // arm letter: BAND.ARM_SYMBOL
    [InlineData("x", "Multiply:x")] // decided by position (the parser)
    [InlineData("I", "Word:I")] // meiosis numeral; i before "(" is decided by the parser
    [InlineData("R", "Word:R")] // listed banding code
    [InlineData("del", "Word:del")]
    public void ListedOrUnfoldable_IsNotFolded(string text, string expected)
    {
        Assert.Equal(expected, Kinds(text));
        Assert.DoesNotContain(Lex(text), t => t.IsCaseFolded);
    }

    [Fact]
    public void Whitespace_IsTrivia_AndLossless()
    {
        const string text = "  mos 45,X/46,XX\t ";
        var tokens = Lex(text);
        Assert.Equal("  ", Assert.Single(tokens[0].Leading).Text);
        Assert.Equal(" ", Assert.Single(tokens[0].Trailing).Text);
        Assert.Equal(text, string.Concat(tokens.Select(t => t.ToFullString())));
        Assert.Equal(TokenKind.EndOfSegment, tokens[^1].Kind);
    }

    [Fact]
    public void Spans_AreOffsetsInTheWholeInput()
    {
        const string text = "46,XX.ish del(5)";
        var tokens = _lexer.Lex(text, TextSpan.FromBounds(10, text.Length));
        Assert.Equal(new TextSpan(10, 3), tokens[0].Span);
        Assert.Equal(new TextSpan(text.Length, 0), tokens[^1].Span);
    }
}
