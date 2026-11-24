using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.Highlighting;

/// <summary>How a highlight is drawn (decisions → Fix suggestions; docs/design/core.md §10).</summary>
public enum HighlightKind
{
    /// <summary>A wrong range (a replace, or a diagnostic without an edit): underlined.</summary>
    Underline,

    /// <summary>Missing text: an insertion marker, followed by the expected text in grey.</summary>
    Insert,

    /// <summary>Extra text (a delete): struck through.</summary>
    Strike,

    /// <summary>
    /// A spot autocorrection changed (T-097): a soft dotted grey underline, not an error style; an insertion shows a grey
    /// marker with the inserted text. <see cref="Highlight.DiagnosticIndex"/> is -1 and <see cref="Highlight.Note"/> names the rule.
    /// </summary>
    AutoCorrected,
}

/// <summary>One highlight in the input editor.</summary>
/// <param name="Span">The range in the input; zero-length for <see cref="HighlightKind.Insert"/>.</param>
/// <param name="Kind">How it is drawn.</param>
/// <param name="Severity">The diagnostic's severity (worst over the contexts for a conditional one); sets the colour.</param>
/// <param name="InsertText">The expected text shown in grey at an insertion marker; <c>null</c> when unknown.</param>
/// <param name="DiagnosticIndex">The index of the diagnostic in <see cref="EditionResult.Diagnostics"/>; -1 for <see cref="HighlightKind.AutoCorrected"/>.</param>
/// <param name="Note">The editor tooltip of an <see cref="HighlightKind.AutoCorrected"/> highlight, e.g. <c>Autocorrected (SYM.LETTER_CASE): …</c>.</param>
public sealed record Highlight(TextSpan Span, HighlightKind Kind, Severity Severity, string? InsertText, int DiagnosticIndex, string? Note = null)
{
    /// <summary>Whether it is drawn as an insertion marker (zero-length, by the insertion generator).</summary>
    public bool IsInsertion => Kind == HighlightKind.Insert || (Kind == HighlightKind.AutoCorrected && Span.Length == 0);

    /// <summary>Whether it covers <paramref name="offset"/> of the editor text (an insertion: the character after the point).</summary>
    public bool Covers(int offset) => Span.Length == 0
        ? offset == Span.Start
        : Span.Start <= offset && offset < Span.End;
}
