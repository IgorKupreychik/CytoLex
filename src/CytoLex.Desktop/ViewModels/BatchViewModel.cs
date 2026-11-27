using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CytoLex.Core.Batch;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>Which rows the batch table shows.</summary>
public enum BatchFilter
{
    /// <summary>Every string.</summary>
    All,

    /// <summary>Not valid in at least one implemented edition.</summary>
    NotValid,

    /// <summary>Valid in every implemented edition.</summary>
    Valid,

    /// <summary>A segment or construct was not validated ("valid as far as implemented").</summary>
    Incomplete,

    /// <summary>Autocorrection changed the string in at least one edition (T-097).</summary>
    AutoCorrected,

    /// <summary>An association rule matched in an implemented edition (T-110).</summary>
    WithAssociations,

    /// <summary>CytoLex and the external checker disagree (T-052).</summary>
    Disagreements,

    /// <summary>A disagreement without a confirmed triage entry (T-052).</summary>
    Untriaged,
}

/// <summary>An entry of the batch filter box.</summary>
public sealed record BatchFilterChoice(BatchFilter Filter, string Label)
{
    /// <inheritdoc/>
    public override string ToString() => Label;
}

/// <summary>
/// Batch validation (T-038): loads a text/CSV/TSV file (<see cref="BatchReader"/>), validates every string in the chosen
/// edition(s) off the UI thread with progress and cancel (<see cref="BatchRunner"/>), shows a summary per edition and a
/// filterable table, exports to CSV/JSON (<see cref="BatchExport"/>), and opens a row in the single-string check.
/// <see cref="Comparison"/> compares the batch with CytoGPS (T-052) without touching the CytoLex result.
/// File pickers live in the view; this class takes local paths.
/// </summary>
public partial class BatchViewModel : ObservableObject
{
    private readonly Validator? _validator;
    private readonly Action<string, EditionChoice?>? _openInSingleCheck;
    private ImmutableArray<BatchRowViewModel> _allRows = [];

    /// <summary>The view model over <paramref name="validator"/> (<c>null</c>: the KB did not load; nothing can run).</summary>
    /// <param name="validator">The validator.</param>
    /// <param name="editionChoices">The edition selector entries (shared with the single-string check).</param>
    /// <param name="defaultChoice">The entry selected at start.</param>
    /// <param name="openInSingleCheck">Opens a string in the single-string check, in the given edition choice.</param>
    /// <param name="comparison">The comparison with the external checker (T-052); default: not configured.</param>
    public BatchViewModel(
        Validator? validator,
        IReadOnlyList<EditionChoice> editionChoices,
        EditionChoice? defaultChoice,
        Action<string, EditionChoice?>? openInSingleCheck,
        ComparisonViewModel? comparison = null)
    {
        ArgumentNullException.ThrowIfNull(editionChoices);
        _validator = validator;
        Comparison = comparison ?? new ComparisonViewModel(validator, null, "no setup.", null);
        Comparison.AutoCorrect = AutoCorrect;
        _openInSingleCheck = openInSingleCheck;
        EditionChoices = editionChoices;
        SelectedEditionChoice = defaultChoice;
        SelectedFilter = Filters[0];
        Status = validator is null ? "The knowledge base could not be loaded." : "Open a text (.txt: one string per line) or CSV/TSV file (header row; string column 'iscn', else the first).";
    }

    /// <summary>The filter box entries.</summary>
    public IReadOnlyList<BatchFilterChoice> Filters { get; } =
    [
        new(BatchFilter.All, "All strings"),
        new(BatchFilter.NotValid, "Not valid"),
        new(BatchFilter.Valid, "Valid"),
        new(BatchFilter.Incomplete, "Valid as far as implemented / partly validated"),
        new(BatchFilter.AutoCorrected, "Autocorrected"),
        new(BatchFilter.WithAssociations, "With literature associations"),
        new(BatchFilter.Disagreements, "Disagrees with CytoGPS"),
        new(BatchFilter.Untriaged, "Disagrees with CytoGPS, not triaged"),
    ];

    /// <summary>The comparison with the external checker (T-052).</summary>
    public ComparisonViewModel Comparison { get; }

    /// <summary>Whether the strings are autocorrected (T-097; on by default); changing it drops the result (run again).</summary>
    [ObservableProperty]
    public partial bool AutoCorrect { get; set; } = true;

    /// <summary>Whether some row of the result was autocorrected (shows the "Corrected" column).</summary>
    [ObservableProperty]
    public partial bool HasCorrectedRows { get; set; }

    /// <summary>Whether some row has a literature association (shows the "Associations" column, T-110).</summary>
    [ObservableProperty]
    public partial bool HasAssociationRows { get; set; }

    /// <summary>The tooltip of the "Associations" column header: the disclaimer.</summary>
    public static string AssociationsColumnTip => AssociationsViewModel.DisclaimerText;

    /// <summary>The edition selector entries.</summary>
    public IReadOnlyList<EditionChoice> EditionChoices { get; }

    /// <summary>The edition(s) to validate against; changing it drops the result (run again).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial EditionChoice? SelectedEditionChoice { get; set; }

    /// <summary>The loaded file.</summary>
    [ObservableProperty]
    public partial string? FilePath { get; set; }

    /// <summary>The strings of the loaded file.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial ImmutableArray<BatchItem> Items { get; set; } = [];

    /// <summary>The reader's warnings for the loaded file.</summary>
    public ObservableCollection<string> Warnings { get; } = [];

    /// <summary>Whether the reader had warnings.</summary>
    [ObservableProperty]
    public partial bool HasWarnings { get; set; }

    /// <summary>Whether a run is in progress.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    public partial bool IsRunning { get; set; }

    /// <summary>Strings finished in the current run.</summary>
    [ObservableProperty]
    public partial int Progress { get; set; }

    /// <summary>Strings in the current run.</summary>
    [ObservableProperty]
    public partial int ProgressMax { get; set; } = 1;

    /// <summary>The last run's result; <c>null</c> before a run or after the input or edition changed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    public partial BatchResult? Result { get; set; }

    /// <summary>Whether there is a result to show.</summary>
    public bool HasResult => Result is not null;

    partial void OnItemsChanged(ImmutableArray<BatchItem> value) => Comparison.SetInput(value, Result is not null);

    partial void OnResultChanged(BatchResult? value) => Comparison.SetInput(Items, value is not null);

    /// <summary>Whether the result can be exported (there is one and no run is in progress).</summary>
    public bool CanExport => Result is not null && !IsRunning;

    /// <summary>One summary line per edition of the result.</summary>
    public ObservableCollection<string> Summaries { get; } = [];

    /// <summary>The editions of the result (table header cells).</summary>
    [ObservableProperty]
    public partial ImmutableArray<string> Editions { get; set; } = [];

    /// <summary>The picked filter.</summary>
    [ObservableProperty]
    public partial BatchFilterChoice? SelectedFilter { get; set; }

    /// <summary>The rows that pass the filter.</summary>
    public ObservableCollection<BatchRowViewModel> Rows { get; } = [];

    /// <summary>The selected row.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedCommand))]
    public partial BatchRowViewModel? SelectedRow { get; set; }

    /// <summary>Status line: what was loaded, run or exported, or what went wrong.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>Loads the strings of <paramref name="path"/> (format by extension) and drops the previous result.</summary>
    public async Task LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        BatchReadResult read;
        try
        {
            read = await Task.Run(() => BatchReader.ReadFile(path)).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not read {Path.GetFileName(path)}: {ex.Message}";
            return;
        }

        FilePath = path;
        Items = read.Items;
        Warnings.Clear();
        foreach (var w in read.Warnings)
        {
            Warnings.Add(w);
        }

        HasWarnings = Warnings.Count > 0;
        ClearResult();
        Status = $"{Path.GetFileName(path)}: {Items.Length} string(s)"
            + (HasWarnings ? $", {Warnings.Count} warning(s)" : string.Empty)
            + (Items.IsEmpty ? string.Empty : " · press Run");
    }

    private bool CanRun() => _validator is not null && !Items.IsEmpty && !IsRunning;

    /// <summary>Validates the loaded strings; cancel with <c>RunCancelCommand</c>.</summary>
    [RelayCommand(CanExecute = nameof(CanRun), IncludeCancelCommand = true)]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var choice = SelectedEditionChoice ?? EditionChoice.All;
        // Associations (T-109) go into the batch exports; the view of them is T-110.
        var options = new ValidationOptions
        {
            Editions = choice.Edition is { } e ? EditionSelection.One(e) : EditionSelection.All,
            AutoCorrect = AutoCorrect,
            IncludeAssociations = true,
        };
        ClearResult();
        IsRunning = true;
        Progress = 0;
        ProgressMax = Math.Max(1, Items.Length);
        Status = $"Validating {Items.Length} string(s)…";
        var progress = new Progress<int>(n =>
        {
            if (IsRunning)
            {
                Progress = Math.Max(Progress, n);
            }
        });
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await BatchRunner.RunAsync(_validator!, Items, options, progress, cancellationToken).ConfigureAwait(true);
            Show(result);
            Status = $"Validated {result.Items.Length} string(s) in {watch.Elapsed.TotalSeconds:0.0} s.";
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Exports the result to <paramref name="path"/>: JSON (full reports) for <c>.json</c>, else the CSV summary.</summary>
    public async Task ExportAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Result is not { } result)
        {
            return;
        }

        try
        {
            await Task.Run(() => BatchExport.WriteFile(path, result)).ConfigureAwait(true);
            Status = $"Exported {result.Items.Length} string(s) to {path}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not write {Path.GetFileName(path)}: {ex.Message}";
        }
    }

    private bool CanOpenSelected() => SelectedRow is not null;

    /// <summary>Opens the selected row in the single-string check.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    private void OpenSelected()
    {
        if (SelectedRow is { } row)
        {
            Open(row);
        }
    }

    /// <summary>Opens <paramref name="row"/> in the single-string check, in the edition choice of the run.</summary>
    public void Open(BatchRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var editions = Result?.Editions ?? [];
        var choice = editions.Length == 1 ? EditionChoices.FirstOrDefault(c => c.Edition == editions[0]) : EditionChoice.All;
        _openInSingleCheck?.Invoke(row.Result.Item.Input, choice);
    }

    partial void OnSelectedEditionChoiceChanged(EditionChoice? value)
    {
        if (Result is not null)
        {
            ClearResult();
            Status = "Edition changed · press Run.";
        }
    }

    partial void OnAutoCorrectChanged(bool value)
    {
        Comparison.AutoCorrect = value;
        if (Result is not null)
        {
            ClearResult();
            Status = "Autocorrection " + (value ? "on" : "off") + " · press Run.";
        }
    }

    partial void OnSelectedFilterChanged(BatchFilterChoice? value) => ApplyFilter();

    private void Show(BatchResult result)
    {
        Result = result;
        Editions = result.Editions;
        Progress = ProgressMax;
        Summaries.Clear();
        foreach (var s in BatchSummary.Of(result, topRules: 3))
        {
            Summaries.Add(SummaryLine(s));
        }

        _allRows = [.. result.Items.Select(i => new BatchRowViewModel(i, Comparison))];
        Comparison.Attach(_allRows);
        HasCorrectedRows = _allRows.Any(r => r.IsAutoCorrected);
        HasAssociationRows = _allRows.Any(r => r.HasAssociations);
        ApplyFilter();
    }

    internal static string SummaryLine(EditionSummary s)
    {
        if (s.NotImplemented == s.Total && s.Total > 0)
        {
            return $"ISCN {s.Edition}: not implemented";
        }

        var afterAuto = s.ValidAfterAutoCorrection > 0 ? $"{s.ValidAfterAutoCorrection} valid after autocorrection, " : string.Empty;
        var line = $"ISCN {s.Edition}: {s.Valid} valid, {afterAuto}{s.NotValid} not valid of {s.Total}";
        if (s.Incomplete > 0)
        {
            line += $" · {s.Incomplete} validated only in part";
        }

        if (!s.TopRules.IsEmpty)
        {
            line += " · most frequent: " + string.Join(", ", s.TopRules.Select(r => $"{r.RuleId} ({r.Strings})"));
        }

        return line;
    }

    private void ClearResult()
    {
        Result = null;
        Editions = [];
        Summaries.Clear();
        _allRows = [];
        Comparison.Attach([]);
        HasCorrectedRows = false;
        HasAssociationRows = false;
        SelectedRow = null;
        Rows.Clear();
        Progress = 0;
    }

    private void ApplyFilter()
    {
        var filter = SelectedFilter?.Filter ?? BatchFilter.All;
        SelectedRow = null;
        Rows.Clear();
        foreach (var row in _allRows.Where(r => filter switch
        {
            BatchFilter.NotValid => r.IsNotValid,
            BatchFilter.Valid => r.IsValid,
            BatchFilter.Incomplete => r.IsIncomplete,
            BatchFilter.AutoCorrected => r.IsAutoCorrected,
            BatchFilter.WithAssociations => r.HasAssociations,
            BatchFilter.Disagreements => r.Comparison?.IsDisagreement ?? false,
            BatchFilter.Untriaged => r.Comparison?.IsUntriaged ?? false,
            _ => true,
        }))
        {
            Rows.Add(row);
        }
    }
}
