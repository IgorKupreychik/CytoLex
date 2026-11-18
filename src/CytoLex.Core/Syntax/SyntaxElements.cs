using System.Collections.Immutable;
using System.Text;
using CytoLex.Core.Text;

namespace CytoLex.Core.Syntax;

/// <summary>Whitespace kept around a token, so the tree reproduces the input exactly (docs/design/core.md §4).</summary>
/// <param name="Span">Its position in the input.</param>
/// <param name="Text">The whitespace characters.</param>
public readonly record struct Trivia(TextSpan Span, string Text);

/// <summary>A token or a node of the syntax tree.</summary>
public abstract class SyntaxElement
{
    /// <summary>The span without surrounding whitespace.</summary>
    public abstract TextSpan Span { get; }

    /// <summary>The span including leading and trailing whitespace.</summary>
    public abstract TextSpan FullSpan { get; }

    /// <summary>The tokens under this element, in input order (missing tokens included).</summary>
    public abstract IEnumerable<SyntaxToken> Tokens();

    /// <summary>The exact input text this element covers, whitespace included (missing tokens add nothing).</summary>
    public string ToFullString()
    {
        var sb = new StringBuilder();
        foreach (var t in Tokens())
        {
            t.AppendFullText(sb);
        }

        return sb.ToString();
    }
}

/// <summary>
/// A lexical token (docs/design/core.md §4). Whitespace after a token is its trailing trivia; only the first
/// token of a segment has leading trivia. A <see cref="IsMissing"/> token is zero-width and inserted by error
/// recovery; its <see cref="Text"/> is the expected text.
/// </summary>
public sealed class SyntaxToken : SyntaxElement
{
    /// <summary>Combining low line: marks an underlined (homologue-distinguishing) character (T-003, T-042).</summary>
    public const char Underline = '̲';

    public SyntaxToken(
        TokenKind kind,
        TextSpan span,
        string text,
        ImmutableArray<Trivia> leading,
        ImmutableArray<Trivia> trailing,
        bool isMissing = false,
        string? vocabRef = null,
        bool isTypedForm = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        Kind = kind;
        _span = span;
        Text = text;
        Leading = leading.IsDefault ? [] : leading;
        Trailing = trailing.IsDefault ? [] : trailing;
        IsMissing = isMissing;
        VocabRef = vocabRef;
        IsTypedForm = isTypedForm;
    }

    private readonly TextSpan _span;

    public TokenKind Kind { get; }

    public override TextSpan Span => _span;

    public override TextSpan FullSpan => TextSpan.FromBounds(
        Leading.IsEmpty ? _span.Start : Leading[0].Span.Start,
        Trailing.IsEmpty ? _span.End : Trailing[^1].Span.End);

    /// <summary>The token text as written (for a missing token: the expected text).</summary>
    public string Text { get; }

    /// <summary>The text without underline marks (U+0332), e.g. <c>10</c> for <c>1̲0̲</c>.</summary>
    public string ValueText => IsUnderlined ? Text.Replace(Underline.ToString(), "", StringComparison.Ordinal) : Text;

    public ImmutableArray<Trivia> Leading { get; }

    public ImmutableArray<Trivia> Trailing { get; }

    /// <summary>Inserted by error recovery; not in the input.</summary>
    public bool IsMissing { get; }

    /// <summary>The vocab item this token is (<c>vocab:symbols/del</c>), when it is a listed symbol or code.</summary>
    public string? VocabRef { get; }

    /// <summary>A typed form of a printed symbol: <c>-</c>/<c>−</c> for <c>–</c>, <c>x</c> for <c>×</c>, <c>-&gt;</c> for <c>→</c> (T-053, T-063).</summary>
    public bool IsTypedForm { get; }

    /// <summary>The token contains underline marks (U+0332).</summary>
    public bool IsUnderlined => Text.Contains(Underline, StringComparison.Ordinal);

    /// <summary>Whitespace follows the token.</summary>
    public bool HasTrailingTrivia => !Trailing.IsEmpty;

    public override IEnumerable<SyntaxToken> Tokens()
    {
        yield return this;
    }

    internal void AppendFullText(StringBuilder sb)
    {
        foreach (var t in Leading)
        {
            sb.Append(t.Text);
        }

        if (!IsMissing)
        {
            sb.Append(Text);
        }

        foreach (var t in Trailing)
        {
            sb.Append(t.Text);
        }
    }

    public override string ToString() => IsMissing ? $"{Kind}<missing '{Text}'>" : $"{Kind} '{Text}'";
}

/// <summary>An inner node of the syntax tree: a kind and its children, in input order (docs/design/core.md §4).</summary>
public sealed class SyntaxNode : SyntaxElement
{
    public SyntaxNode(SyntaxKind kind, ImmutableArray<SyntaxElement> children)
    {
        if (children.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A syntax node needs at least one child.", nameof(children));
        }

        Kind = kind;
        Children = children;
    }

    public SyntaxKind Kind { get; }

    public ImmutableArray<SyntaxElement> Children { get; }

    public override TextSpan Span
    {
        get
        {
            var tokens = Tokens().ToList();
            var present = tokens.Where(t => !t.IsMissing).ToList();
            return present.Count == 0
                ? new TextSpan(tokens[0].Span.Start, 0)
                : TextSpan.FromBounds(present[0].Span.Start, present[^1].Span.End);
        }
    }

    public override TextSpan FullSpan => TextSpan.FromBounds(Children[0].FullSpan.Start, Children[^1].FullSpan.End);

    public override IEnumerable<SyntaxToken> Tokens() => Children.SelectMany(c => c.Tokens());

    /// <summary>The direct child nodes of <paramref name="kind"/>.</summary>
    public IEnumerable<SyntaxNode> ChildNodes(SyntaxKind kind) =>
        Children.OfType<SyntaxNode>().Where(n => n.Kind == kind);

    /// <summary>This node and every node below it, depth first.</summary>
    public IEnumerable<SyntaxNode> DescendantNodesAndSelf()
    {
        yield return this;
        foreach (var child in Children.OfType<SyntaxNode>())
        {
            foreach (var n in child.DescendantNodesAndSelf())
            {
                yield return n;
            }
        }
    }

    public override string ToString() => $"{Kind} '{ToFullString()}'";
}
