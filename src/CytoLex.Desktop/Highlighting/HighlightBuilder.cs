using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.Highlighting;

/// <summary>
/// Turns the diagnostics of one edition into editor highlights (docs/design/core.md §10): the highlight follows from the
/// diagnostic's own level-1 fix — a replace underlines the edited span, an insert gives an insertion marker with the grey
/// expected text, a delete strikes the span through. A diagnostic without an edit underlines its span (or, when the span
/// is empty, shows an insertion marker without text).
/// </summary>
public static class HighlightBuilder
{
    /// <summary>The highlights of <paramref name="result"/> over <paramref name="input"/>, in diagnostic order.</summary>
    /// <param name="input">The validated input (spans are clamped to it).</param>
    /// <param name="result">The edition result.</param>
    /// <param name="context">The effective sample context, if known: picks the fix alternative of a conditional diagnostic.</param>
    public static ImmutableArray<Highlight> Build(string input, EditionResult result, SampleContext? context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(result);

        var highlights = ImmutableArray.CreateBuilder<Highlight>();
        for (var i = 0; i < result.Diagnostics.Length; i++)
        {
            var diagnostic = result.Diagnostics[i];
            if (OwnEditFix(diagnostic, context) is { } fix)
            {
                foreach (var edit in fix.Edits)
                {
                    var span = Clamp(edit.Span, input.Length);
                    var kind = span.Length == 0 ? HighlightKind.Insert
                        : edit.NewText.Length == 0 ? HighlightKind.Strike
                        : HighlightKind.Underline;
                    highlights.Add(new Highlight(span, kind, diagnostic.Severity, kind == HighlightKind.Insert ? edit.NewText : null, i));
                }
            }
            else
            {
                var span = Clamp(diagnostic.Span, input.Length);
                var kind = span.Length == 0 ? HighlightKind.Insert : HighlightKind.Underline;
                highlights.Add(new Highlight(span, kind, diagnostic.Severity, null, i));
            }
        }

        return highlights.ToImmutable();
    }

    /// <summary>
    /// The diagnostic's own level-1 fix: the first edit fix for <paramref name="context"/> (or context-free). The
    /// "all occurrences" fix comes after it (<c>DiagnosticFactory.WithRuleWideFixes</c> appends it), so it is never picked.
    /// </summary>
    public static Fix? OwnEditFix(Diagnostic diagnostic, SampleContext? context)
    {
        var edits = diagnostic.Fixes.IsDefault ? [] : diagnostic.Fixes.Where(f => f.IsEdit).ToList();
        if (context is { } known)
        {
            return edits.FirstOrDefault(f => f.ForContext is null || f.ForContext == known);
        }

        // Unknown context: a conditional diagnostic's alternatives are for the contexts in which it is a finding.
        return edits.FirstOrDefault();
    }

    private static TextSpan Clamp(TextSpan span, int length)
    {
        var start = Math.Clamp(span.Start, 0, length);
        var end = Math.Clamp(span.End, start, length);
        return TextSpan.FromBounds(start, end);
    }
}
