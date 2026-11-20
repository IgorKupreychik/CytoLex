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
    public DiagnosticItemViewModel(string input, Diagnostic diagnostic, int index, string? edition = null, ApplyFixHandler? applyFix = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(diagnostic);

        Diagnostic = diagnostic;
        Index = index;
        Span = diagnostic.Span;
        SeverityLabel = Label(diagnostic.Severity);
        SeverityBrush = SeverityBrushes.For(diagnostic.Severity);
        RuleId = diagnostic.RuleId;
        Edition = edition;

        // KB rules always carry a citation; core codes (CORE.*) have none and no rule to open.
        HasKbRule = edition is not null && !diagnostic.Citations.IsDefaultOrEmpty;
        Message = diagnostic.Message;
        Detail = diagnostic.Detail;
        Location = LocationOf(input, diagnostic.Span);
        Condition = diagnostic.Condition is { } condition ? ConditionText(condition) : null;
        Review = diagnostic.BookAmbiguous
            ? "Book-ambiguous rule (capped at warning)" + (diagnostic.ReviewNote is { Length: > 0 } note ? ": " + note.Trim() : string.Empty)
            : null;
        Citations = diagnostic.Citations.IsDefaultOrEmpty ? null : string.Join("; ", diagnostic.Citations.Select(c => c.Rendered));
        Fixes = diagnostic.Fixes.IsDefaultOrEmpty ? [] : [.. diagnostic.Fixes.Select(f => new FixItemViewModel(input, f, applyFix))];
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

    /// <summary>The edition of the result the diagnostic belongs to; <c>null</c> if not given.</summary>
    public string? Edition { get; }

    /// <summary>Whether <see cref="RuleId"/> is a KB rule that the rule browser can open (T-039).</summary>
    public bool HasKbRule { get; }

    /// <summary>Whether <see cref="RuleId"/> is a core code (shown as plain text).</summary>
    public bool IsCoreCode => !HasKbRule;

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

    /// <summary>The fixes: level-1 fixes with a diff and Apply (T-040), or the level-3 explanation.</summary>
    public ImmutableArray<FixItemViewModel> Fixes { get; }

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
}
