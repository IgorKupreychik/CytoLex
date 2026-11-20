using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Context;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The single-string check (T-036): validates <see cref="Input"/> in the default edition on every change and exposes the
/// detected segments, the context, validity, the diagnostics and the editor highlights.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    /// <summary>Book strings to try (docs/design/core.md §13 worked examples).</summary>
    public static IReadOnlyList<string> ExampleStrings { get; } =
    [
        "46,XY,del(5)(q13q33)",
        "46XX",
        "46,XX,del (5)(q13q33)",
        "47,XXX",
        "47,XY,+X",
        "46,XX.ish 5p15.2(D5S23×2)",
    ];

    private readonly Validator? _validator;

    /// <summary>The view model over <see cref="Validator.Default"/>.</summary>
    public MainWindowViewModel()
        : this(null)
    {
    }

    /// <summary>The view model over <paramref name="validator"/> (<c>null</c> = <see cref="Validator.Default"/>).</summary>
    public MainWindowViewModel(Validator? validator)
    {
        try
        {
            _validator = validator ?? Validator.Default;
            Edition = _validator.DefaultEdition;
            Status = $"ISCN {Edition} · implemented editions: {string.Join(", ", _validator.ImplementedEditions)}";
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.Text.Json.JsonException)
        {
            Edition = "?";
            Status = "The knowledge base could not be loaded: " + ex.Message;
        }

        Validate();
    }

    /// <summary>The edition validated against (the newest implemented one; the selector is T-037).</summary>
    public string Edition { get; }

    /// <summary>The ISCN string.</summary>
    [ObservableProperty]
    public partial string Input { get; set; } = string.Empty;

    /// <summary>The example picked in the examples box; loads it into <see cref="Input"/>.</summary>
    [ObservableProperty]
    public partial string? SelectedExample { get; set; }

    /// <summary>Status line (KB / edition).</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>The verdict, e.g. <c>Valid in ISCN 2016 as constitutional only</c>.</summary>
    [ObservableProperty]
    public partial string ValidityText { get; set; } = string.Empty;

    /// <summary>Whether the input is valid in the edition (for the verdict colour).</summary>
    [ObservableProperty]
    public partial bool IsValid { get; set; }

    /// <summary>Whether there is a verdict to show (the input is not blank).</summary>
    [ObservableProperty]
    public partial bool HasResult { get; set; }

    /// <summary>The sample context: hint, inferred or unknown, with its markers.</summary>
    [ObservableProperty]
    public partial string ContextText { get; set; } = string.Empty;

    /// <summary>Diagnostic counts.</summary>
    [ObservableProperty]
    public partial string CountsText { get; set; } = string.Empty;

    /// <summary>The editor highlights of the current result.</summary>
    [ObservableProperty]
    public partial ImmutableArray<Highlight> Highlights { get; set; } = [];

    /// <summary>The diagnostic selected in the list.</summary>
    [ObservableProperty]
    public partial DiagnosticItemViewModel? SelectedDiagnostic { get; set; }

    /// <summary>The detected segments with their notation types.</summary>
    public ObservableCollection<SegmentItemViewModel> Segments { get; } = [];

    /// <summary>The diagnostics of the edition, in span order.</summary>
    public ObservableCollection<DiagnosticItemViewModel> Diagnostics { get; } = [];

    /// <summary>The last report; <c>null</c> for blank input or without a validator.</summary>
    public ValidationReport? Report { get; private set; }

    partial void OnInputChanged(string value) => Validate();

    partial void OnSelectedExampleChanged(string? value)
    {
        if (value is not null)
        {
            Input = value;
        }
    }

    /// <summary>Validates <see cref="Input"/> and refreshes every result property.</summary>
    public void Validate()
    {
        Segments.Clear();
        Diagnostics.Clear();
        SelectedDiagnostic = null;
        Report = null;

        var input = Input;
        if (_validator is null || string.IsNullOrWhiteSpace(input))
        {
            HasResult = false;
            IsValid = false;
            ValidityText = _validator is null ? string.Empty : "Enter an ISCN string.";
            ContextText = CountsText = string.Empty;
            Highlights = [];
            return;
        }

        var report = _validator.Validate(input);
        var result = report.Results[0];
        Report = report;

        foreach (var segment in report.Segments)
        {
            Segments.Add(SegmentItemViewModel.From(input, segment, result.Segments.FirstOrDefault(s => s.Segment.Index == segment.Index)));
        }

        for (var i = 0; i < result.Diagnostics.Length; i++)
        {
            Diagnostics.Add(new DiagnosticItemViewModel(input, result.Diagnostics[i], i));
        }

        HasResult = true;
        IsValid = result.Status == EditionStatus.Implemented && result.Validity.IsValid;
        ValidityText = VerdictOf(result, report.Context);
        ContextText = ContextOf(input, report.Context);
        CountsText = CountsOf(result.Counts);
        Highlights = HighlightBuilder.Build(input, result, report.Context.Effective);
    }

    internal static string VerdictOf(EditionResult result, ContextInfo context)
    {
        if (result.Status == EditionStatus.NotImplemented)
        {
            return result.Reason ?? $"ISCN {result.Edition}: not implemented";
        }

        var v = result.Validity;
        var all = Enum.GetValues<SampleContext>().Length;
        var text = !v.IsValid ? $"Not valid in ISCN {result.Edition}"
            : context.Effective is null && v.ValidContexts.Length < all
                ? $"Valid in ISCN {result.Edition} as {string.Join(" or ", v.ValidContexts.Select(DiagnosticItemViewModel.Label))} only"
                : $"Valid in ISCN {result.Edition}";
        return v.IsComplete ? text : text + " (as far as implemented)";
    }

    internal static string ContextOf(string input, ContextInfo context)
    {
        var markers = context.Markers.IsDefaultOrEmpty
            ? string.Empty
            : " (markers: " + string.Join(", ", context.Markers.Select(m => $"{m.Span.Of(input)} → {DiagnosticItemViewModel.Label(m.Indicates)}")) + ")";
        if (context.Hint is { } hint)
        {
            return $"Context: {DiagnosticItemViewModel.Label(hint)} (given){markers}";
        }

        if (context.Effective is { } effective)
        {
            return $"Context: {DiagnosticItemViewModel.Label(effective)}{markers}";
        }

        return context.IsContradictory
            ? $"Context: unknown, the markers contradict each other{markers}"
            : "Context: unknown (no markers); context-dependent findings are shown per context";
    }

    private static string CountsOf(SeverityCounts c)
    {
        var text = $"{c.Errors} error(s), {c.Warnings} warning(s), {c.Infos} info(s)";
        return c.Conditional > 0 ? text + $", {c.Conditional} context-dependent" : text;
    }
}
