using System.Collections.Immutable;
using CytoLex.Core.Text;

namespace CytoLex.Core.Syntax;

/// <summary>
/// The state of one parse (T-083): the segment's tokens, the position and the recovery diagnostics, shared by the grammars
/// that parse one segment (<see cref="KaryotypeGrammar"/>, <see cref="ArrGrammar"/>, the FISH grammars).
/// </summary>
internal sealed class TokenCursor(ImmutableArray<SyntaxToken> tokens, string input)
{
    private int _pos;

    /// <summary>The recoveries reported so far, each under a KB rule (docs/design/core.md §8.4).</summary>
    public List<SyntaxDiagnostic> Diagnostics { get; } = [];

    /// <summary>The input the tokens were read from.</summary>
    public string Input => input;

    /// <summary>The index of the current token.</summary>
    public int Position => _pos;

    public SyntaxToken Current => tokens[_pos];

    public SyntaxToken Previous => tokens[Math.Max(_pos - 1, 0)];

    public bool AtEnd => Current.Kind == TokenKind.EndOfSegment;

    /// <summary>The token at <paramref name="index"/> (clamped to the end-of-segment token).</summary>
    public SyntaxToken TokenAt(int index) => tokens[Math.Clamp(index, 0, tokens.Length - 1)];

    public SyntaxToken Peek(int k) => tokens[Math.Min(_pos + k, tokens.Length - 1)];

    public SyntaxToken Eat()
    {
        var t = tokens[_pos];
        if (t.Kind != TokenKind.EndOfSegment)
        {
            _pos++;
        }

        return t;
    }

    public string TextOf(TextSpan span) => span.Of(input);
}
