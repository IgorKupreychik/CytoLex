using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using CytoLex.Desktop.ViewModels;

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
    /// The highlights of <paramref name="result"/> in the editor text (T-097): the diagnostics' highlights over
    /// <see cref="EditionText.Text"/> mapped onto the typed text, then one <see cref="HighlightKind.AutoCorrected"/>
    /// highlight per applied autocorrection.
    /// </summary>
    public static ImmutableArray<Highlight> Build(EditionText text, EditionResult result, SampleContext? context)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(result);

        var highlights = Build(text.Text, result, context);
        if (!text.IsCorrected)
        {
            return highlights;
        }

        var builder = ImmutableArray.CreateBuilder<Highlight>();
        builder.AddRange(highlights.Select(h => h with { Span = text.ToEditor(h.Span) }));
        foreach (var c in text.AutoCorrection!.Corrections)
        {
            var span = Clamp(c.OriginalSpan, text.Original.Length);
            builder.Add(new Highlight(span, HighlightKind.AutoCorrected, Severity.Info, span.Length == 0 ? c.NewText : null, -1, NoteOf(c)));
        }

        return builder.ToImmutable();
    }

    /// <summary>The tooltip of an autocorrected spot: <c>Autocorrected (SYM.LETTER_CASE): "xy" → "XY" — Write … (ISCN 2016 §3, p.…)</c>.</summary>
    public static string NoteOf(AppliedCorrection correction)
    {
        ArgumentNullException.ThrowIfNull(correction);
        var cite = correction.Citations.IsDefaultOrEmpty ? string.Empty : $" ({correction.Citations[0].Rendered})";
        return $"Autocorrected ({correction.RuleId}): {ChangeOf(correction)} — {correction.Description}{cite}";
    }

    /// <summary>The change of a correction: <c>"xy" → "XY"</c>, <c>removed " "</c> or <c>inserted ","</c>.</summary>
    public static string ChangeOf(AppliedCorrection correction)
    {
        ArgumentNullException.ThrowIfNull(correction);
        return correction.NewText.Length == 0 ? $"removed \"{correction.OriginalText}\""
            : correction.OriginalText.Length == 0 ? $"inserted \"{correction.NewText}\""
            : $"\"{correction.OriginalText}\" → \"{correction.NewText}\"";
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
