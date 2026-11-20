using System.Collections.Frozen;
using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Text;

namespace CytoLex.Core.Syntax;

/// <summary>
/// Turns one segment of the input into tokens (docs/design/core.md §4). Spans are offsets in the whole input.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Whitespace becomes trivia: trailing trivia of the token before it, or leading trivia of the first token.</item>
/// <item>A letter run is split into listed symbols (ch. 3 vocab, Table 2 codes) and the sex chromosome letters
/// <c>X</c>/<c>Y</c>, with the fewest pieces (ties: longest first piece): <c>psudic</c> → <c>psu</c> <c>dic</c>,
/// <c>mardn</c> → <c>mar</c> <c>dn</c>, <c>XXYc</c> → <c>X X Y c</c>, <c>14pstkstk</c> → … <c>p stk stk</c>. Pieces are
/// adjacent tokens without trivia, so spacing rules can still see that a space is missing. A run that cannot be split
/// is one <see cref="TokenKind.UnknownWord"/>.</item>
/// <item>Typed forms (<c>-</c>, <c>−</c>, <c>x</c>, <c>-&gt;</c>) get the printed symbol's kind with
/// <see cref="SyntaxToken.IsTypedForm"/> (T-053, T-063), and only where the vocab lists them as equivalent forms.</item>
/// <item>The underline mark U+0332 stays inside the token of the character it follows (T-042).</item>
/// <item>A run that is not listed but folds to exactly one listed token ignoring case (<c>Del</c>, <c>T</c>, <c>y</c>), or a
/// run of sex chromosome letters with a lower-case one (<c>xy</c>), is read as the listed form
/// (<see cref="SyntaxToken.IsCaseFolded"/>; SYM.LETTER_CASE, T-074).</item>
/// </list>
/// </remarks>
public sealed class Lexer
{
    private static readonly FrozenSet<string> _chromosomeLetters = FrozenSet.ToFrozenSet(["X", "Y"], StringComparer.Ordinal);

    private readonly Vocabulary _vocab;
    private readonly FrozenSet<string> _letterPieces;
    private readonly int _longestPiece;
    private readonly FrozenDictionary<string, string> _caseFoldedByUpper;

    public Lexer(Vocabulary vocab)
    {
        ArgumentNullException.ThrowIfNull(vocab);
        _vocab = vocab;
        _letterPieces = vocab.Tokens
            .Where(t => t.Length > 0 && t.All(char.IsAsciiLetter))
            .Concat(_chromosomeLetters)
            .ToFrozenSet(StringComparer.Ordinal);
        _longestPiece = _letterPieces.Max(p => p.Length);
        _caseFoldedByUpper = CaseFoldedPieces(_letterPieces);
    }

    /// <summary>Tokens of <paramref name="span"/> in <paramref name="input"/>, ending with a zero-width <see cref="TokenKind.EndOfSegment"/>.</summary>
    public ImmutableArray<SyntaxToken> Lex(string input, TextSpan span)
    {
        ArgumentNullException.ThrowIfNull(input);
        // Whitespace runs are recorded as EndOfSegment placeholders and turned into trivia by Assemble.
        var raw = new List<RawToken>();

        var i = span.Start;
        while (i < span.End)
        {
            var c = input[i];
            if (char.IsWhiteSpace(c))
            {
                var start = i;
                while (i < span.End && char.IsWhiteSpace(input[i]))
                {
                    i++;
                }

                raw.Add(new(TokenKind.EndOfSegment, TextSpan.FromBounds(start, i), null, false));
                continue;
            }

            if (char.IsAsciiDigit(c))
            {
                var start = i;
                while (i < span.End && (char.IsAsciiDigit(input[i]) || input[i] == SyntaxToken.Underline))
                {
                    i++;
                }

                raw.Add(new(TokenKind.Number, TextSpan.FromBounds(start, i), null, false));
                continue;
            }

            if (char.IsLetter(c))
            {
                var start = i;
                while (i < span.End && (char.IsLetter(input[i]) || input[i] == SyntaxToken.Underline))
                {
                    i++;
                }

                raw.AddRange(SplitLetters(input, TextSpan.FromBounds(start, i)));
                continue;
            }

            var (kind, length, text) = Punctuation(input, i, span.End);
            var entry = _vocab.Find(text);
            var typed = entry is not null && entry.IsTypedForm(text);
            if (kind == TokenKind.Minus && text == "-" && entry is null)
            {
                typed = true;
            }

            raw.Add(new(kind, new TextSpan(i, length), kind == TokenKind.Dot ? null : entry?.KbRef, typed));
            i += length;
        }

        return Assemble(input, span, raw);
    }

    /// <summary>Attaches whitespace (the placeholder entries) as trivia and builds the tokens.</summary>
    private static ImmutableArray<SyntaxToken> Assemble(
        string input,
        TextSpan span,
        List<RawToken> raw)
    {
        var tokens = ImmutableArray.CreateBuilder<SyntaxToken>();
        var leading = ImmutableArray<Trivia>.Empty;
        var pending = (Raw: default(RawToken), Leading: ImmutableArray<Trivia>.Empty);
        var hasPending = false;
        var trailing = new List<Trivia>();

        void Flush()
        {
            if (hasPending)
            {
                var written = pending.Raw.Span.Of(input);
                tokens.Add(new SyntaxToken(
                    pending.Raw.Kind,
                    pending.Raw.Span,
                    pending.Raw.Listed ?? written,
                    pending.Leading,
                    [.. trailing],
                    vocabRef: pending.Raw.VocabRef,
                    isTypedForm: pending.Raw.Typed,
                    writtenText: written));
                trailing.Clear();
                hasPending = false;
            }
        }

        foreach (var r in raw)
        {
            if (r.Kind == TokenKind.EndOfSegment)
            {
                var trivia = new Trivia(r.Span, r.Span.Of(input));
                if (hasPending)
                {
                    trailing.Add(trivia);
                }
                else
                {
                    leading = leading.Add(trivia);
                }

                continue;
            }

            Flush();
            pending = (r, tokens.Count == 0 ? leading : []);
            hasPending = true;
        }

        Flush();
        tokens.Add(new SyntaxToken(TokenKind.EndOfSegment, new TextSpan(span.End, 0), "", tokens.Count == 0 ? leading : [], []));
        return tokens.ToImmutable();
    }

    private static (TokenKind Kind, int Length, string Text) Punctuation(string input, int i, int end)
    {
        var c = input[i];
        var next = i + 1 < end ? input[i + 1] : '\0';
        return c switch
        {
            ':' when next == ':' => (TokenKind.DoubleColon, 2, "::"),
            ':' => (TokenKind.Colon, 1, ":"),
            '/' when next == '/' => (TokenKind.DoubleSlash, 2, "//"),
            '/' => (TokenKind.Slash, 1, "/"),
            '-' when next == '>' => (TokenKind.Arrow, 2, "->"),
            '-' or '–' or '−' => (TokenKind.Minus, 1, c.ToString()),
            '→' => (TokenKind.Arrow, 1, "→"),
            ',' => (TokenKind.Comma, 1, ","),
            ';' => (TokenKind.Semicolon, 1, ";"),
            '(' => (TokenKind.OpenParen, 1, "("),
            ')' => (TokenKind.CloseParen, 1, ")"),
            '[' => (TokenKind.OpenBracket, 1, "["),
            ']' => (TokenKind.CloseBracket, 1, "]"),
            '<' => (TokenKind.OpenAngle, 1, "<"),
            '>' => (TokenKind.CloseAngle, 1, ">"),
            '{' => (TokenKind.OpenBrace, 1, "{"),
            '}' => (TokenKind.CloseBrace, 1, "}"),
            '.' => (TokenKind.Dot, 1, "."),
            '~' => (TokenKind.Tilde, 1, "~"),
            '?' => (TokenKind.Question, 1, "?"),
            '+' => (TokenKind.Plus, 1, "+"),
            '±' => (TokenKind.PlusMinus, 1, "±"),
            '×' => (TokenKind.Multiply, 1, "×"),
            '…' => (TokenKind.Ellipsis, 1, "…"),
            '=' => (TokenKind.Equals, 1, "="),
            '_' => (TokenKind.Underscore, 1, "_"),
            _ => (TokenKind.BadCharacter, char.IsSurrogatePair(input, i) ? 2 : 1, char.IsSurrogatePair(input, i) ? input.Substring(i, 2) : c.ToString()),
        };
    }

    /// <summary>Splits a letter run into listed pieces (fewest pieces, then longest first piece); underline marks stay with their letter.</summary>
    private IEnumerable<RawToken> SplitLetters(string input, TextSpan run)
    {
        // Letters without underline marks, with their positions in the input.
        var letters = new List<int>();
        for (var i = run.Start; i < run.End; i++)
        {
            if (input[i] != SyntaxToken.Underline)
            {
                letters.Add(i);
            }
        }

        var plain = string.Concat(letters.Select(p => input[p]));
        if (letters.Count == run.Length && IsFoldedSexRun(plain))
        {
            // "xy", "xx", "Xy": the sex chromosomes in lower case (SYM.LETTER_CASE, T-074); "x" alone is decided by the parser.
            for (var k = 0; k < plain.Length; k++)
            {
                yield return Letter(new TextSpan(run.Start + k, 1), char.ToUpperInvariant(plain[k]).ToString(), plain[k]);
            }

            yield break;
        }

        var pieces = Segment(plain);
        if (pieces is null)
        {
            if (letters.Count == run.Length && _caseFoldedByUpper.TryGetValue(plain.ToUpperInvariant(), out var listed))
            {
                // "Del", "T", "INV": one listed token in another case (SYM.LETTER_CASE, T-074).
                yield return Piece(run, listed, listed);
                yield break;
            }

            yield return new(TokenKind.UnknownWord, run, null, false);
            yield break;
        }

        var from = 0;
        foreach (var length in pieces)
        {
            var text = plain.Substring(from, length);
            var start = from == 0 ? run.Start : letters[from];
            var end = from + length < letters.Count ? letters[from + length] : run.End;
            var span = TextSpan.FromBounds(start, end);
            yield return Piece(span, text, null);
            from += length;
        }
    }

    /// <summary>A listed piece <paramref name="text"/>; <paramref name="listed"/> is set when it was written in another case.</summary>
    private RawToken Piece(TextSpan span, string text, string? listed)
    {
        if (_chromosomeLetters.Contains(text))
        {
            return new(TokenKind.ChromosomeLetter, span, null, false, listed);
        }

        var entry = _vocab.Find(text);
        var kind = entry?.Kind == "punctuation" && entry.Tokens.Contains("×") ? TokenKind.Multiply : TokenKind.Word;
        return new(kind, span, entry?.KbRef, entry is not null && entry.IsTypedForm(text), listed);
    }

    private static RawToken Letter(TextSpan span, string listed, char written) =>
        new(TokenKind.ChromosomeLetter, span, null, false, listed[0] == written ? null : listed);

    /// <summary>Two or more sex chromosome letters, at least one in lower case (<c>xy</c>, <c>xx</c>, <c>Xy</c>).</summary>
    private static bool IsFoldedSexRun(string run) =>
        run.Length >= 2 && run.All(c => c is 'x' or 'X' or 'y' or 'Y') && run.Any(char.IsLower);

    /// <summary>
    /// Letter tokens by their case-folded form, for forms that fold to exactly one listed token (SYM.LETTER_CASE, T-074):
    /// <c>del</c>, <c>Ph</c>, <c>Y</c>. Clashing folds (<c>c</c>/<c>C</c>, <c>i</c>/<c>I</c>, <c>x</c>/<c>X</c>, …) and
    /// the arm letters <c>p</c>/<c>q</c> (BAND.ARM_SYMBOL) are left out.
    /// </summary>
    private static FrozenDictionary<string, string> CaseFoldedPieces(IEnumerable<string> pieces) =>
        pieces
            .GroupBy(p => p.ToUpperInvariant(), StringComparer.Ordinal)
            .Where(g => g.Count() == 1 && g.Key is not ("P" or "Q"))
            .ToFrozenDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);

    /// <summary>A raw token; <see cref="Listed"/> is the listed form of a token written in another case.</summary>
    private readonly record struct RawToken(TokenKind Kind, TextSpan Span, string? VocabRef, bool Typed, string? Listed = null);

    /// <summary>Piece lengths covering <paramref name="text"/> (fewest pieces, ties: longest first piece), or <c>null</c>.</summary>
    private List<int>? Segment(string text)
    {
        var n = text.Length;
        // best[i] = fewest pieces covering text[i..]; next[i] = length of the first piece.
        var best = new int[n + 1];
        var next = new int[n + 1];
        Array.Fill(best, int.MaxValue);
        best[n] = 0;
        for (var i = n - 1; i >= 0; i--)
        {
            for (var len = Math.Min(_longestPiece, n - i); len >= 1; len--)
            {
                if (best[i + len] != int.MaxValue && best[i + len] + 1 < best[i] && _letterPieces.Contains(text.Substring(i, len)))
                {
                    best[i] = best[i + len] + 1;
                    next[i] = len;
                }
            }
        }

        if (best[0] == int.MaxValue)
        {
            return null;
        }

        var result = new List<int>();
        for (var i = 0; i < n; i += next[i])
        {
            result.Add(next[i]);
        }

        return result;
    }
}
