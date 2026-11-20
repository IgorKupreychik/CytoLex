using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Media;
using CytoLex.Core.Context;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.ViewModels;

/// <summary>One row of the diagnostics list: a <see cref="Diagnostic"/> rendered as text.</summary>
public sealed class DiagnosticItemViewModel
{
    public DiagnosticItemViewModel(string input, Diagnostic diagnostic, int index)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(diagnostic);

        Diagnostic = diagnostic;
        Index = index;
        Span = diagnostic.Span;
        SeverityLabel = Label(diagnostic.Severity);
        SeverityBrush = SeverityBrushes.For(diagnostic.Severity);
        RuleId = diagnostic.RuleId;
        Message = diagnostic.Message;
        Detail = diagnostic.Detail;
        Location = LocationOf(input, diagnostic.Span);
        Condition = diagnostic.Condition is { } condition ? ConditionText(condition) : null;
        Review = diagnostic.BookAmbiguous
            ? "Book-ambiguous rule (capped at warning)" + (diagnostic.ReviewNote is { Length: > 0 } note ? ": " + note.Trim() : string.Empty)
            : null;
        Citations = diagnostic.Citations.IsDefaultOrEmpty ? null : string.Join("; ", diagnostic.Citations.Select(c => c.Rendered));
        Fixes = diagnostic.Fixes.IsDefaultOrEmpty ? [] : [.. diagnostic.Fixes.Select(FixText)];
    }

    /// <summary>The diagnostic shown.</summary>
    public Diagnostic Diagnostic { get; }

    /// <summary>Its index in the edition's diagnostics.</summary>
    public int Index { get; }

    /// <summary>Its primary span in the input.</summary>
    public TextSpan Span { get; }

    /// <summary><c>error</c>, <c>warning</c> or <c>info</c>.</summary>
    public string SeverityLabel { get; }

    /// <summary>The severity colour.</summary>
    public IBrush SeverityBrush { get; }

    /// <summary>The KB rule ID or core code.</summary>
    public string RuleId { get; }

    /// <summary>The KB rule title.</summary>
    public string Message { get; }

    /// <summary>What the check found, if it said more than the title.</summary>
    public string? Detail { get; }

    /// <summary>Where: <c>col 7–10: "del "</c> or <c>insert at col 3</c> (1-based columns).</summary>
    public string Location { get; }

    /// <summary>Per-context severities of a conditional diagnostic, e.g. <c>error if acquired; none if constitutional</c>.</summary>
    public string? Condition { get; }

    /// <summary>The book-ambiguous note, if the KB rule needs review.</summary>
    public string? Review { get; }

    /// <summary>The rendered KB citations.</summary>
    public string? Citations { get; }

    /// <summary>The fixes as text: level-1 description and preview, or the level-3 explanation.</summary>
    public ImmutableArray<string> Fixes { get; }

    /// <summary>Whether <see cref="Detail"/> is set.</summary>
    public bool HasDetail => Detail is not null;

    /// <summary>Whether <see cref="Condition"/> is set.</summary>
    public bool HasCondition => Condition is not null;

    /// <summary>Whether <see cref="Review"/> is set.</summary>
    public bool HasReview => Review is not null;

    /// <summary>Whether <see cref="Citations"/> is set.</summary>
    public bool HasCitations => Citations is not null;

    internal static string Label(Severity severity) => severity switch
    {
        Severity.Error => "error",
        Severity.Warning => "warning",
        _ => "info",
    };

    internal static string Label(SampleContext context) => context switch
    {
        SampleContext.Constitutional => "constitutional",
        _ => "acquired",
    };

    internal static string LocationOf(string input, TextSpan span)
    {
        var inv = CultureInfo.InvariantCulture;
        if (span.Length == 0)
        {
            return string.Create(inv, $"insert at col {span.Start + 1}");
        }

        var text = span.Start >= 0 && span.End <= input.Length ? span.Of(input) : string.Empty;
        return span.Length == 1
            ? string.Create(inv, $"col {span.Start + 1}: \"{text}\"")
            : string.Create(inv, $"col {span.Start + 1}–{span.End}: \"{text}\"");
    }

    private static string ConditionText(ContextCondition condition) => string.Join(
        "; ",
        Enum.GetValues<SampleContext>().Select(c => condition.In(c) is { } s
            ? $"{Label(s)} if {Label(c)}"
            : $"no finding if {Label(c)}"));

    private static string FixText(Fix fix)
    {
        var context = fix.ForContext is { } c ? $" (if {Label(c)})" : string.Empty;
        return fix.Level switch
        {
            FixLevel.Deterministic when fix.Preview is not null => $"Fix{context}: {fix.Description} → {fix.Preview}",
            FixLevel.Explanation => $"Explanation{context}: {fix.Description}",
            _ => $"Suggestion{context}: {fix.Description}",
        };
    }
}
