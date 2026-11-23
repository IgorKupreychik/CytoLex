using System.Collections.Frozen;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Syntax;

/// <summary>
/// The token and recovery helpers every grammar uses (T-083), over the <see cref="TokenCursor"/> the grammars of one parse
/// share. A grammar that needs another grammar takes it as a constructor argument.
/// </summary>
internal abstract class GrammarBase(TokenCursor cursor)
{
    protected TokenCursor Cursor { get; } = cursor;

    protected SyntaxToken Current => Cursor.Current;

    protected SyntaxToken Previous => Cursor.Previous;

    protected bool AtEnd => Cursor.AtEnd;

    /// <summary>The current token directly follows the previous one (no whitespace between).</summary>
    protected bool Adjacent => Cursor.Position > 0 && !Previous.HasTrailingTrivia;

    protected SyntaxToken Peek(int k) => Cursor.Peek(k);

    protected bool At(TokenKind kind) => Current.Kind == kind;

    protected bool AtWord(string word) => Current.Kind == TokenKind.Word && Current.Text == word;

    protected static bool IsWord(SyntaxToken t, FrozenSet<string> set) => t.Kind == TokenKind.Word && set.Contains(t.Text);

    protected SyntaxToken Eat() => Cursor.Eat();

    protected string TextOf(TextSpan span) => Cursor.TextOf(span);

    /// <summary>A zero-width token at the insertion point (the end of the previous token).</summary>
    protected SyntaxToken Missing(TokenKind kind, string text)
    {
        var at = Cursor.Position > 0 ? Previous.Span.End : Current.Span.Start;
        return new SyntaxToken(kind, new TextSpan(at, 0), text, [], [], isMissing: true);
    }

    /// <summary>A zero-width token at the insertion point, reported as missing under <paramref name="rule"/>.</summary>
    protected SyntaxToken MissingReported(TokenKind kind, string text, string rule, string message)
    {
        var missing = Missing(kind, text);
        Report(rule, missing.Span, SyntaxDiagnosticKind.Missing, message, text);
        return missing;
    }

    protected void Report(string rule, TextSpan span, SyntaxDiagnosticKind kind, string message, string? expected = null) =>
        Cursor.Diagnostics.Add(new SyntaxDiagnostic(rule, span, kind, expected, message));

    /// <summary>Reports a recovery whose fix is not a plain insert or replace of the span (T-035), e.g. a reorder.</summary>
    protected void Report(string rule, TextSpan span, SyntaxDiagnosticKind kind, string message, params TextEdit[] edits) =>
        Cursor.Diagnostics.Add(new SyntaxDiagnostic(rule, span, kind, null, message) { Edits = [.. edits] });

    protected static TextEdit Delete(TextSpan span) => new(span, string.Empty);

    protected static TextEdit Insert(int offset, string text) => new(new TextSpan(offset, 0), text);

    protected static SyntaxNode Node(SyntaxKind kind, IEnumerable<SyntaxElement> children) => new(kind, [.. children]);

    protected static SyntaxNode Node(SyntaxKind kind, params SyntaxElement[] children) => new(kind, [.. children]);

    protected static TextSpan Cover(SyntaxElement first, SyntaxElement last) => TextSpan.FromBounds(first.Span.Start, last.Span.End);

    /// <summary>Skips tokens up to (not including) one that <paramref name="stop"/> accepts; at least one token is skipped.</summary>
    protected SyntaxNode Skip(Func<SyntaxToken, bool> stop, string rule, string message)
    {
        var skipped = new List<SyntaxElement>();
        do
        {
            skipped.Add(Eat());
        }
        while (!AtEnd && !stop(Current));

        var node = Node(SyntaxKind.Skipped, skipped);
        Report(rule, node.Span, SyntaxDiagnosticKind.Skipped, message);
        return node;
    }
}
