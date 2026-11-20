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
}

/// <summary>One highlight in the input editor.</summary>
/// <param name="Span">The range in the input; zero-length for <see cref="HighlightKind.Insert"/>.</param>
/// <param name="Kind">How it is drawn.</param>
/// <param name="Severity">The diagnostic's severity (worst over the contexts for a conditional one); sets the colour.</param>
/// <param name="InsertText">The expected text shown in grey at an insertion marker; <c>null</c> when unknown.</param>
/// <param name="DiagnosticIndex">The index of the diagnostic in <see cref="EditionResult.Diagnostics"/>.</param>
public sealed record Highlight(TextSpan Span, HighlightKind Kind, Severity Severity, string? InsertText, int DiagnosticIndex);
