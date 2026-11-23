namespace CytoLex.Core.Syntax;

/// <summary>Lexical token classes of a notation segment (docs/design/core.md §4).</summary>
public enum TokenKind
{
    /// <summary>A run of digits (chromosome number, count, region/band digits). May carry underlining (U+0332).</summary>
    Number,

    /// <summary>A listed symbol or banding code (<c>del</c>, <c>pter</c>, <c>p</c>, <c>GTG</c>); <see cref="SyntaxToken.VocabRef"/> names it.</summary>
    Word,

    /// <summary>A letter run that is not made of listed symbols (<c>dl</c>, <c>iso</c>, <c>P</c>).</summary>
    UnknownWord,

    /// <summary>
    /// A FISH probe or locus name read as one token by the FISH grammars (<c>D22S75</c>, <c>RP11-1143I12</c>, <c>5′CBFB</c>,
    /// <c>wcp11</c>; T-033). The lexer never produces it: the grammar joins the adjacent pieces the lexer split the name into.
    /// </summary>
    Probe,

    /// <summary>A sex chromosome letter <c>X</c> or <c>Y</c>.</summary>
    ChromosomeLetter,

    Comma,
    Semicolon,
    Colon,
    DoubleColon,
    OpenParen,
    CloseParen,
    OpenBracket,
    CloseBracket,
    OpenAngle,
    CloseAngle,
    OpenBrace,
    CloseBrace,
    Slash,
    DoubleSlash,

    /// <summary><c>.</c>: a decimal point inside a band (separator periods never reach the parser; §3.1).</summary>
    Dot,

    Tilde,
    Question,
    Plus,

    /// <summary><c>–</c>, or its typed forms <c>-</c> and <c>−</c> (<see cref="SyntaxToken.IsTypedForm"/>).</summary>
    Minus,

    /// <summary><c>±</c> (ploidy levels such as <c>&lt;4n±&gt;</c>).</summary>
    PlusMinus,

    /// <summary><c>×</c>, or its typed form <c>x</c>.</summary>
    Multiply,

    /// <summary><c>→</c>, or its typed form <c>-&gt;</c>.</summary>
    Arrow,

    /// <summary><c>…</c>: abnormalities left out (book fragments, T-010).</summary>
    Ellipsis,

    Equals,
    Underscore,

    /// <summary>Any other character.</summary>
    BadCharacter,

    /// <summary>Zero-width token at the end of the segment; holds the trailing whitespace of an otherwise empty segment.</summary>
    EndOfSegment,
}
