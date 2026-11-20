using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Context;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The single-string check (T-036): validates <see cref="Input"/> on every change in the edition picked in the selector, or in
/// every known edition (T-037), and exposes the context plus one <see cref="EditionPanelViewModel"/> per edition (verdict,
/// segments, diagnostics, abnormalities by cell line (T-069), editor highlights).
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
    private EditionPanelViewModel? _observedPanel;

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
            var implemented = _validator.ImplementedEditions;
            EditionChoices =
            [
                .. _validator.KnownEditions.Select(e => new EditionChoice(e, implemented.Contains(e) ? $"ISCN {e}" : $"ISCN {e} (not implemented)")),
                EditionChoice.All,
            ];
            SelectedEditionChoice = EditionChoices.First(c => c.Edition == _validator.DefaultEdition);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.Text.Json.JsonException)
        {
            _validator = null;
            EditionChoices = [];
            Status = "The knowledge base could not be loaded: " + ex.Message;
        }

        Validate();
    }

    /// <summary>The editions to pick from (T-037): every known edition, oldest first, then "All editions".</summary>
    public IReadOnlyList<EditionChoice> EditionChoices { get; }

    /// <summary>The picked edition or "All editions"; changing it re-validates.</summary>
    [ObservableProperty]
    public partial EditionChoice? SelectedEditionChoice { get; set; }

    /// <summary>The ISCN string.</summary>
    [ObservableProperty]
    public partial string Input { get; set; } = string.Empty;

    /// <summary>The example picked in the examples box; loads it into <see cref="Input"/>.</summary>
    [ObservableProperty]
    public partial string? SelectedExample { get; set; }

    /// <summary>Status line (KB / editions).</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>Whether there are results to show (the input is not blank).</summary>
    [ObservableProperty]
    public partial bool HasResult { get; set; }

    /// <summary>Shown instead of results for blank input (<c>Enter an ISCN string.</c>).</summary>
    [ObservableProperty]
    public partial string Placeholder { get; set; } = string.Empty;

    /// <summary>The sample context: hint, inferred or unknown, with its markers (the same in every edition).</summary>
    [ObservableProperty]
    public partial string ContextText { get; set; } = string.Empty;

    /// <summary>One panel per validated edition (one tab each).</summary>
    public ObservableCollection<EditionPanelViewModel> Panels { get; } = [];

    /// <summary>The panel (tab) shown; the editor draws its highlights.</summary>
    [ObservableProperty]
    public partial EditionPanelViewModel? SelectedPanel { get; set; }

    /// <summary>The editor highlights of <see cref="SelectedPanel"/>.</summary>
    public ImmutableArray<Highlight> Highlights => SelectedPanel?.Highlights ?? [];

    /// <summary>The span the editor should select (a clicked diagnostic, card or part); raised on every <see cref="SelectSpan"/>, even for the same span.</summary>
    public TextSpan? SelectedSpan { get; private set; }

    /// <summary>The span of the card under the mouse, drawn as a background in the editor.</summary>
    [ObservableProperty]
    public partial TextSpan? HoverSpan { get; set; }

    /// <summary>The last report; <c>null</c> for blank input or without a validator.</summary>
    public ValidationReport? Report { get; private set; }

    /// <summary>Asks the editor to select <paramref name="span"/>.</summary>
    public void SelectSpan(TextSpan span)
    {
        SelectedSpan = span;
        OnPropertyChanged(nameof(SelectedSpan));
    }

    partial void OnInputChanged(string value) => Validate();

    partial void OnSelectedEditionChoiceChanged(EditionChoice? value) => Validate();

    partial void OnSelectedExampleChanged(string? value)
    {
        if (value is not null)
        {
            Input = value;
        }
    }

    partial void OnSelectedPanelChanged(EditionPanelViewModel? value)
    {
        if (_observedPanel is not null)
        {
            _observedPanel.PropertyChanged -= OnPanelPropertyChanged;
        }

        _observedPanel = value;
        if (value is not null)
        {
            value.PropertyChanged += OnPanelPropertyChanged;
        }

        HoverSpan = null;
        OnPropertyChanged(nameof(Highlights));
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditionPanelViewModel.SelectedDiagnostic) && sender is EditionPanelViewModel { SelectedDiagnostic: { } item })
        {
            SelectSpan(item.Span);
        }
    }

    /// <summary>Validates <see cref="Input"/> in the selected edition(s) and rebuilds the panels.</summary>
    public void Validate()
    {
        // Keep the shown tab across re-validation ("All editions" while typing).
        var previous = SelectedPanel?.Edition;
        SelectedPanel = null;
        Panels.Clear();
        Report = null;
        if (_validator is null)
        {
            HasResult = false;
            Placeholder = ContextText = string.Empty;
            return;
        }

        var choice = SelectedEditionChoice ?? EditionChoice.All;
        Status = $"Implemented editions: {string.Join(", ", _validator.ImplementedEditions)} · showing: "
            + (choice.Edition is null ? "all editions" : $"ISCN {choice.Edition}");

        var input = Input;
        if (string.IsNullOrWhiteSpace(input))
        {
            HasResult = false;
            Placeholder = "Enter an ISCN string.";
            ContextText = string.Empty;
            return;
        }

        var editions = choice.Edition is { } edition ? EditionSelection.One(edition) : EditionSelection.All;
        var report = _validator.Validate(input, new ValidationOptions { Editions = editions });
        Report = report;
        foreach (var result in report.Results)
        {
            Panels.Add(new EditionPanelViewModel(report, result));
        }

        HasResult = true;
        Placeholder = string.Empty;
        ContextText = ContextOf(input, report.Context);
        SelectedPanel = Panels.FirstOrDefault(p => p.Edition == previous)
            ?? Panels.FirstOrDefault(p => p.IsImplemented)
            ?? Panels.FirstOrDefault();
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
}

/// <summary>An entry of the edition selector (T-037).</summary>
/// <param name="Edition">The edition, e.g. <c>2016</c>; <c>null</c> = all editions.</param>
/// <param name="Label">The text shown, e.g. <c>ISCN 2020 (not implemented)</c>.</param>
public sealed record EditionChoice(string? Edition, string Label)
{
    /// <summary>"All editions": one panel per known edition.</summary>
    public static EditionChoice All { get; } = new(null, "All editions");

    /// <inheritdoc/>
    public override string ToString() => Label;
}
