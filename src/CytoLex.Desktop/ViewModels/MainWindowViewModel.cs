using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Context;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;
using CytoLex.Desktop.Rules;
using CytoLex.External;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The single-string check (T-036): validates <see cref="Input"/> on every change in the edition picked in the selector, or in
/// every known edition (T-037), and exposes the context plus one <see cref="EditionPanelViewModel"/> per edition (verdict,
/// segments, diagnostics, abnormalities by cell line (T-069), literal translation (T-059), editor highlights). The optional
/// CytoGPS tab (T-081, <see cref="CytoGps"/>) sits after the edition tabs; it only reads the input and never changes them.
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
    private bool _applying;

    /// <summary>The view model over <see cref="Validator.Default"/>.</summary>
    public MainWindowViewModel()
        : this(null)
    {
    }

    /// <summary>The view model over <paramref name="validator"/> (<c>null</c> = <see cref="Validator.Default"/>).</summary>
    public MainWindowViewModel(Validator? validator)
        : this(validator, RuleStore.DefaultPath)
    {
    }

    /// <summary>The view model over <paramref name="validator"/> with the rule browser over <paramref name="ruleDatabasePath"/>.</summary>
    public MainWindowViewModel(Validator? validator, string ruleDatabasePath)
        : this(validator, ruleDatabasePath, null, null)
    {
    }

    /// <summary>
    /// The view model with the CytoGPS tab (T-081) over <paramref name="externalChecker"/>; <c>null</c> = not configured,
    /// with <paramref name="externalSetupError"/> as the reason.
    /// </summary>
    public MainWindowViewModel(Validator? validator, string ruleDatabasePath, IExternalChecker? externalChecker, string? externalSetupError)
    {
        CytoGps = new CytoGpsPanelViewModel(
            externalChecker,
            externalChecker is null ? externalSetupError ?? "no setup (see config/local.yaml → cytogps)." : null,
            CytoLexVerdict);
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

        Batch = new BatchViewModel(_validator, EditionChoices, SelectedEditionChoice, OpenInSingleCheck);
        Rules = new RuleBrowserViewModel(ruleDatabasePath, SelectedEditionChoice?.Edition, CheckExample);
        Validate();
    }

    /// <summary>Index of the main tab: <see cref="SingleTab"/>, <see cref="BatchTab"/> or <see cref="RulesTab"/>.</summary>
    [ObservableProperty]
    public partial int SelectedMainTab { get; set; }

    /// <summary>The single-string check tab.</summary>
    public const int SingleTab = 0;

    /// <summary>The batch tab (T-038).</summary>
    public const int BatchTab = 1;

    /// <summary>The rule browser tab (T-039).</summary>
    public const int RulesTab = 2;

    /// <summary>Batch validation (T-038).</summary>
    public BatchViewModel Batch { get; }

    /// <summary>The rule browser (T-039).</summary>
    public RuleBrowserViewModel Rules { get; }

    /// <summary>Opens a translation link (T-059) of <paramref name="edition"/> in the rule browser: a rule or a vocab item.</summary>
    /// <returns>Whether the entry was found (bands have no browser entry and return <c>false</c> without switching tabs).</returns>
    public bool OpenKbEntry(string edition, KbRef kbRef)
    {
        ArgumentNullException.ThrowIfNull(edition);
        if (kbRef.Kind == KbRefKind.Band)
        {
            return false;
        }

        var found = Rules.OpenKbRef(edition, kbRef);
        SelectedMainTab = RulesTab;
        return found;
    }

    /// <summary>The genome builds offered for the coordinates (T-059).</summary>
    public static IReadOnlyList<CoordinateBuildChoice> CoordinateBuilds { get; } =
    [
        new(CoordinateBuild.Grch38, "GRCh38"),
        new(CoordinateBuild.Grch37, "GRCh37"),
    ];

    /// <summary>Whether the translation shows approximate band coordinates and segment sizes (T-059; off by default). Changing it re-validates.</summary>
    [ObservableProperty]
    public partial bool ShowCoordinates { get; set; }

    /// <summary>The build whose coordinates are shown; changing it re-renders the coordinates only (both builds are in the result).</summary>
    [ObservableProperty]
    public partial CoordinateBuildChoice SelectedCoordinateBuild { get; set; } = CoordinateBuilds[0];

    /// <summary>
    /// The editor offset under the mouse (T-059), set by the view; <c>null</c> when the mouse is not over the text. Marks the
    /// translation part at the offset in the selected panel and its span in the editor.
    /// </summary>
    [ObservableProperty]
    public partial int? HoverOffset { get; set; }

    /// <summary>A translation card is under the mouse (<c>hovered</c>) or left: marks it and its span in the editor.</summary>
    public void HoverTranslationItem(TranslationItemViewModel item, bool hovered)
    {
        ArgumentNullException.ThrowIfNull(item);
        var translation = SelectedPanel?.Translation;
        if (hovered)
        {
            translation?.Highlight(item);
            HoverSpan = item.IsSummary ? null : item.Span;
        }
        else if (item.IsHighlighted)
        {
            translation?.Highlight(null);
            if (HoverSpan == item.Span)
            {
                HoverSpan = null;
            }
        }
    }

    partial void OnHoverOffsetChanged(int? value)
    {
        var translation = SelectedPanel?.Translation;
        if (translation is null)
        {
            return;
        }

        if (value is { } offset && translation.HighlightAt(offset) is { } span)
        {
            HoverSpan = span;
        }
        else
        {
            translation.Highlight(null);
            HoverSpan = null;
        }
    }

    partial void OnShowCoordinatesChanged(bool value) => Validate();

    partial void OnSelectedCoordinateBuildChanged(CoordinateBuildChoice value)
    {
        foreach (var panel in Panels)
        {
            panel.Translation?.SetBuild(value.Build);
        }
    }

    /// <summary>Opens the KB rule of <paramref name="diagnostic"/> in the rule browser (diagnostic → rule link).</summary>
    /// <returns>Whether the rule was found.</returns>
    public bool OpenRule(DiagnosticItemViewModel diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (!diagnostic.HasKbRule || diagnostic.Edition is not { } edition)
        {
            return false;
        }

        var found = Rules.OpenRule(edition, diagnostic.RuleId);
        SelectedMainTab = RulesTab;
        return found;
    }

    // A rule browser example → the single-string check, in the rule's edition when it is a choice.
    private void CheckExample(string input, string edition) =>
        OpenInSingleCheck(input, EditionChoices.FirstOrDefault(c => c.Edition == edition));

    /// <summary>Shows <paramref name="input"/> in the single-string check, in <paramref name="choice"/> when given (from a batch row).</summary>
    public void OpenInSingleCheck(string input, EditionChoice? choice)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (choice is not null && EditionChoices.Contains(choice))
        {
            SelectedEditionChoice = choice;
        }

        Input = input.ReplaceLineEndings(" ");
        SelectedMainTab = SingleTab;
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

    /// <summary>The CytoGPS tab (T-081): optional, on demand, never blocks CytoLex.</summary>
    public CytoGpsPanelViewModel CytoGps { get; }

    /// <summary>The result tabs: <see cref="Panels"/>, then <see cref="CytoGps"/> (T-081).</summary>
    public ObservableCollection<IResultTab> ResultTabs { get; } = [];

    /// <summary>
    /// The result tab shown. An edition tab sets <see cref="SelectedPanel"/>; the CytoGPS tab keeps it, so the editor still
    /// draws that edition's highlights.
    /// </summary>
    [ObservableProperty]
    public partial IResultTab? SelectedResultTab { get; set; }

    /// <summary>Whether the CytoGPS tab is shown.</summary>
    public bool IsCytoGpsTabSelected => SelectedResultTab is CytoGpsPanelViewModel;

    partial void OnSelectedResultTabChanged(IResultTab? value)
    {
        if (value is EditionPanelViewModel panel)
        {
            SelectedPanel = panel;
        }

        OnPropertyChanged(nameof(IsCytoGpsTabSelected));
    }

    // The CytoLex verdict of the agreement badge: from the shown report when it has the edition, else one extra validation.
    private bool? CytoLexVerdict(string input, string edition)
    {
        var result = Report is { } report && string.Equals(report.Input, input, StringComparison.Ordinal)
            ? report.Results.FirstOrDefault(r => r.Edition == edition)
            : null;
        if (result is null && _validator is not null && _validator.ImplementedEditions.Contains(edition))
        {
            result = _validator.Validate(input, new ValidationOptions { Editions = EditionSelection.One(edition) }).Results.FirstOrDefault();
        }

        return result is { Status: EditionStatus.Implemented } ? result.Validity.IsValid : null;
    }

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

    /// <summary>What the last Apply did (T-040), e.g. <c>Applied: insert a comma</c>; cleared when the input is typed.</summary>
    [ObservableProperty]
    public partial string? LastApplied { get; set; }

    /// <summary>Whether <see cref="LastApplied"/> is set.</summary>
    public bool HasLastApplied => LastApplied is not null;

    /// <summary>
    /// Applies a fix (T-040): sets <see cref="Input"/> to <paramref name="newText"/>, which re-validates, and notes what was
    /// applied. The editor takes the new text as one undoable change.
    /// </summary>
    public void ApplyFix(string newText, string description)
    {
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentNullException.ThrowIfNull(description);
        _applying = true;
        try
        {
            Input = newText;
        }
        finally
        {
            _applying = false;
        }

        LastApplied = "Applied: " + description;
    }

    partial void OnLastAppliedChanged(string? value) => OnPropertyChanged(nameof(HasLastApplied));

    /// <summary>Asks the editor to select <paramref name="span"/>.</summary>
    public void SelectSpan(TextSpan span)
    {
        SelectedSpan = span;
        OnPropertyChanged(nameof(SelectedSpan));
    }

    partial void OnInputChanged(string value)
    {
        if (!_applying)
        {
            LastApplied = null;
        }

        Validate();
    }

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
        if (SelectedResultTab is not CytoGpsPanelViewModel)
        {
            SelectedResultTab = value;
        }

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
        var sideTab = SelectedPanel?.SelectedSideTab ?? EditionPanelViewModel.AbnormalitiesTab; // and the right-hand tab (T-059)
        var cytoGpsShown = SelectedResultTab is CytoGpsPanelViewModel; // and the CytoGPS tab (T-081)
        SelectedResultTab = null;
        SelectedPanel = null;
        ResultTabs.Clear();
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
            CytoGps.SetInput(string.Empty);
            return;
        }

        var editions = choice.Edition is { } edition ? EditionSelection.One(edition) : EditionSelection.All;
        var options = new ValidationOptions { Editions = editions, IncludeTranslation = true, IncludeCoordinates = ShowCoordinates };
        var report = _validator.Validate(input, options);
        Report = report;
        foreach (var result in report.Results)
        {
            Panels.Add(new EditionPanelViewModel(report, result, ApplyFix, SelectedCoordinateBuild.Build, (edition, r) => OpenKbEntry(edition, r))
            {
                SelectedSideTab = sideTab,
            });
        }

        HasResult = true;
        Placeholder = string.Empty;
        ContextText = ContextOf(input, report.Context);
        foreach (var panel in Panels)
        {
            ResultTabs.Add(panel);
        }

        ResultTabs.Add(CytoGps);
        SelectedPanel = Panels.FirstOrDefault(p => p.Edition == previous)
            ?? Panels.FirstOrDefault(p => p.IsImplemented)
            ?? Panels.FirstOrDefault();
        if (cytoGpsShown)
        {
            SelectedResultTab = CytoGps;
        }

        // After the CytoLex results are complete: CytoGPS only reads the input (a new input cancels its running check).
        CytoGps.SetInput(input);
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

/// <summary>An entry of the genome build selector of the translation coordinates (T-059).</summary>
/// <param name="Build">The build.</param>
/// <param name="Label">The text shown, e.g. <c>GRCh38</c>.</param>
public sealed record CoordinateBuildChoice(CoordinateBuild Build, string Label)
{
    /// <inheritdoc/>
    public override string ToString() => Label;
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
