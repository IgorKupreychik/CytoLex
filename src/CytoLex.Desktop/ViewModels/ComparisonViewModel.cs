using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CytoLex.Core.Batch;
using CytoLex.Core.Validation;
using CytoLex.External;
using CytoLex.External.Comparison;
using CytoLex.External.CytoGps;

namespace CytoLex.Desktop.ViewModels;

/// <summary>Where the batch comparison with CytoGPS stands (T-052).</summary>
public enum ComparisonState
{
    /// <summary>No checker: <see cref="ComparisonViewModel.StatusText"/> says why.</summary>
    NotConfigured,

    /// <summary>Ready; nothing compared yet (or the input changed).</summary>
    Idle,

    /// <summary>CytoLex and the checker are running.</summary>
    Running,

    /// <summary>A result is shown (the checker may still have failed: <see cref="ComparisonResult.CheckerStatus"/>).</summary>
    Done,

    /// <summary>The run broke off with an unexpected error.</summary>
    Failed,

    /// <summary>The run was cancelled.</summary>
    Cancelled,
}

/// <summary>An entry of the triage label box.</summary>
public sealed record TriageLabelChoice(TriageLabel Label, string Name)
{
    /// <summary>The four labels, by their YAML names.</summary>
    public static IReadOnlyList<TriageLabelChoice> All { get; } =
        [.. Enum.GetValues<TriageLabel>().Select(l => new TriageLabelChoice(l, TriageFile.LabelName(l)))];

    /// <summary>The entry of <paramref name="label"/>.</summary>
    public static TriageLabelChoice Of(TriageLabel label) => All.First(c => c.Label == label);

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>
/// The CytoLex vs. CytoGPS comparison in the Batch tab (T-052): runs <see cref="ComparisonEngine.CompareAsync"/> on the
/// loaded batch in the checker's edition, with or without autocorrection as the Batch tab's checkbox says (<see cref="AutoCorrect"/>:
/// a string valid after autocorrection counts as valid; the checker always gets it as written), off the UI thread with progress and cancel;
/// shows the agreement matrix, and keeps the triage file (open, edit per disagreement, save as YAML, stale entries,
/// report export). CytoGPS never blocks the batch: without a checker, or when it fails, this view says why and the
/// CytoLex results stay as they are. File pickers live in the view; this class takes local paths.
/// </summary>
public partial class ComparisonViewModel : ObservableObject
{
    private readonly Validator? _validator;
    private readonly IExternalChecker? _checker;
    private readonly IExternalCoverage _coverage;
    private readonly string? _notConfigured;
    private ImmutableArray<BatchItem> _items = [];
    private bool _hasBatchResult;
    private ImmutableArray<BatchRowViewModel> _rows = [];

    /// <summary>Creates the comparison.</summary>
    /// <param name="validator">CytoLex (<c>null</c>: the KB did not load).</param>
    /// <param name="checker">The external checker; <c>null</c> when it could not be created (<paramref name="setupError"/> says why).</param>
    /// <param name="setupError">Why there is no checker.</param>
    /// <param name="triagePath">The triage file opened at start (<c>null</c> or missing: an empty, unsaved one).</param>
    /// <param name="coverage">The notation the checker can't read (default: <see cref="CytoGpsCoverage"/>).</param>
    public ComparisonViewModel(Validator? validator, IExternalChecker? checker, string? setupError, string? triagePath, IExternalCoverage? coverage = null)
    {
        _validator = validator;
        _checker = checker;
        _coverage = coverage ?? CytoGpsCoverage.Instance;
        CheckerName = checker?.Name ?? "CytoGPS";
        Edition = checker?.Edition ?? CytoGpsChecker.CytoGpsEdition;
        Triage = TriageFile.Empty(CheckerName, Edition);
        if (checker is null)
        {
            _notConfigured = setupError ?? "no setup.";
        }
        else if (checker.CheckAvailability() is { IsConfigured: false } availability)
        {
            _notConfigured = availability.Reason ?? "not configured.";
        }

        State = _notConfigured is null ? ComparisonState.Idle : ComparisonState.NotConfigured;
        if (triagePath is not null && File.Exists(triagePath))
        {
            OpenTriage(triagePath);
        }

        UpdateStatus();
    }

    /// <summary>The committed triage file of the CytoGPS 2016 comparison, if the app runs from the repo.</summary>
    public static string? DefaultTriagePath() =>
        RepoLocator.Find() is { } root ? Path.Combine(root, "tests", "data", "comparison", "cytogps-2016-triage.yaml") : null;

    /// <summary>The checker's name.</summary>
    public string CheckerName { get; }

    /// <summary>The edition compared (the checker's).</summary>
    public string Edition { get; }

    /// <summary>The compare button text.</summary>
    public string CompareText => $"Compare with {CheckerName} (ISCN {Edition}, {Mode})";

    /// <summary><c>with autocorrection</c> or <c>as written</c>.</summary>
    private string Mode => AutoCorrect ? "with autocorrection" : "as written";

    /// <summary>
    /// Whether CytoLex autocorrects before comparing (follows the Batch tab's Autocorrect checkbox; on by default): a string
    /// valid after autocorrection counts as valid. Changing it drops the comparison (compare again).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompareText))]
    public partial bool AutoCorrect { get; set; } = true;

    partial void OnAutoCorrectChanged(bool value)
    {
        if (Result is null && State != ComparisonState.Running)
        {
            UpdateStatus();
            return;
        }

        CancelRun();
        Result = null;
        if (State != ComparisonState.NotConfigured)
        {
            State = ComparisonState.Idle;
        }

        ShowResult();
        UpdateStatus();
    }

    /// <summary>Whether a checker is configured.</summary>
    public bool IsConfigured => _notConfigured is null;

    /// <summary>Where the comparison stands.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyCanExecuteChangedFor(nameof(CompareCommand))]
    public partial ComparisonState State { get; set; }

    /// <summary>Whether a run is in progress.</summary>
    public bool IsRunning => State == ComparisonState.Running;

    /// <summary>The comparison shown; <c>null</c> before a run or after a new file was loaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    public partial ComparisonResult? Result { get; set; }

    /// <summary>Whether there is a comparison to show.</summary>
    public bool HasResult => Result is not null;

    /// <summary>What the comparison does or why it can't (not configured, failed, cancelled).</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    /// <summary>The agreement matrix line; empty without a result.</summary>
    [ObservableProperty]
    public partial string MatrixText { get; set; } = string.Empty;

    /// <summary>The checker's text progress during a run.</summary>
    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    /// <summary>Strings CytoLex has validated in the current run.</summary>
    [ObservableProperty]
    public partial int Progress { get; set; }

    /// <summary>Strings in the current run.</summary>
    [ObservableProperty]
    public partial int ProgressMax { get; set; } = 1;

    /// <summary>The triage file (labels of the disagreements).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriageText))]
    public partial TriageFile Triage { get; set; }

    /// <summary>Where the triage file is saved; <c>null</c> for a new file (Save as).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriageText))]
    [NotifyPropertyChangedFor(nameof(HasTriagePath))]
    public partial string? TriagePath { get; set; }

    /// <summary>Whether the triage file has a path (Save writes there).</summary>
    public bool HasTriagePath => TriagePath is not null;

    /// <summary>Whether the triage file has changes not yet saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriageText))]
    public partial bool IsTriageDirty { get; set; }

    /// <summary>The triage file line, e.g. <c>Triage: cytogps-2016-triage.yaml · 72 entries · unsaved changes</c>.</summary>
    public string TriageText =>
        "Triage: " + (TriagePath is null ? "new file" : Path.GetFileName(TriagePath))
        + $" · {Triage.Entries.Length} entr{(Triage.Entries.Length == 1 ? "y" : "ies")}"
        + (IsTriageDirty ? " · unsaved changes" : string.Empty);

    /// <summary>The last triage file problem (open/save), or <c>null</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTriageError))]
    public partial string? TriageError { get; set; }

    /// <summary>Whether <see cref="TriageError"/> is set.</summary>
    public bool HasTriageError => TriageError is not null;

    /// <summary>Triage entries whose string is not a disagreement in this batch.</summary>
    public ObservableCollection<TriageEntry> StaleTriage { get; } = [];

    /// <summary>Whether there are stale entries.</summary>
    [ObservableProperty]
    public partial bool HasStaleTriage { get; set; }

    /// <summary>Sets the batch to compare: a new file drops the comparison; a new batch result keeps it.</summary>
    /// <param name="items">The loaded strings.</param>
    /// <param name="hasBatchResult">Whether the batch has a CytoLex result (its rows exist).</param>
    public void SetInput(ImmutableArray<BatchItem> items, bool hasBatchResult)
    {
        if (items != _items)
        {
            CancelRun();
            _items = items;
            Result = null;
            if (State != ComparisonState.NotConfigured)
            {
                State = ComparisonState.Idle;
            }

            ShowResult();
        }

        _hasBatchResult = hasBatchResult;
        CompareCommand.NotifyCanExecuteChanged();
        UpdateStatus();
    }

    /// <summary>Attaches the comparison of each string to the batch rows (in input order; one row per loaded string).</summary>
    public void Attach(ImmutableArray<BatchRowViewModel> rows)
    {
        _rows = rows;
        ShowRows();
    }

    private bool CanCompare() => IsConfigured && _validator is not null && _hasBatchResult && !_items.IsEmpty && State != ComparisonState.Running;

    /// <summary>Compares the batch with the checker; cancel with <c>CompareCancelCommand</c>.</summary>
    [RelayCommand(CanExecute = nameof(CanCompare), IncludeCancelCommand = true)]
    private async Task CompareAsync(CancellationToken cancellationToken)
    {
        var items = _items;
        var triage = Triage;
        var autoCorrect = AutoCorrect;
        State = ComparisonState.Running;
        Progress = 0;
        ProgressMax = Math.Max(1, items.Length);
        ProgressText = null;
        UpdateStatus();
        var progress = new Progress<string>(m =>
        {
            if (IsRunning && items == _items)
            {
                ProgressText = m;
            }
        });
        var validated = new Progress<int>(n =>
        {
            if (IsRunning && items == _items)
            {
                Progress = Math.Max(Progress, n);
            }
        });
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Off the UI thread: the adapter hashes and may compile the sources before its first await.
            var result = await Task.Run(
                () => ComparisonEngine.CompareAsync(items, _validator!, _checker!, _coverage, triage, progress, validated, autoCorrect, cancellationToken),
                cancellationToken).ConfigureAwait(true);
            if (items != _items || autoCorrect != AutoCorrect)
            {
                return;
            }

            // The triage file may have been edited during the run.
            Result = ReferenceEquals(triage, Triage) ? result : result.WithTriage(Triage, _coverage);
            State = ComparisonState.Done;
            ShowResult();
            UpdateStatus(watch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (items == _items)
            {
                State = ComparisonState.Cancelled;
                UpdateStatus();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The engine never throws for checker problems; anything else still stays in this view.
            if (items == _items)
            {
                State = ComparisonState.Failed;
                StatusText = $"{CheckerName} comparison failed: {ex.Message} The CytoLex results are not affected.";
            }
        }
        finally
        {
            ProgressText = null;
        }
    }

    /// <summary>Stops a running comparison (window closed).</summary>
    public void CancelRun()
    {
        if (CompareCommand.IsRunning)
        {
            CompareCommand.Cancel();
        }
    }

    /// <summary>Opens a triage file; on a problem the current one stays and <see cref="TriageError"/> says why.</summary>
    /// <returns>Whether it was opened.</returns>
    public bool OpenTriage(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        TriageFile file;
        try
        {
            file = TriageFile.Load(path);
        }
        catch (Exception ex) when (ex is TriageFileException or IOException or UnauthorizedAccessException)
        {
            TriageError = $"Could not open {Path.GetFileName(path)}: {ex.Message}";
            return false;
        }

        if (file.Checker != CheckerName || file.Edition != Edition)
        {
            TriageError = $"{Path.GetFileName(path)} is for {file.Checker} {file.Edition}, not {CheckerName} {Edition}.";
            return false;
        }

        TriageError = null;
        TriagePath = path;
        SetTriage(file, dirty: false);
        return true;
    }

    /// <summary>Starts a new, empty triage file (saved with Save as).</summary>
    public void NewTriage()
    {
        TriageError = null;
        TriagePath = null;
        SetTriage(TriageFile.Empty(CheckerName, Edition), dirty: false);
    }

    /// <summary>Saves the triage file to its path.</summary>
    /// <returns>Whether it was saved (<c>false</c> without a path or on a write problem).</returns>
    public bool SaveTriage() => TriagePath is { } path && SaveTriageAs(path);

    /// <summary>Saves the triage file to <paramref name="path"/>, which becomes its path.</summary>
    /// <returns>Whether it was saved.</returns>
    public bool SaveTriageAs(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            Triage.Save(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TriageError = $"Could not write {Path.GetFileName(path)}: {ex.Message}";
            return false;
        }

        TriageError = null;
        TriagePath = path;
        IsTriageDirty = false;
        return true;
    }

    /// <summary>Puts <paramref name="entry"/> into the triage file (replacing the entry for its string).</summary>
    public void SetTriageEntry(TriageEntry entry) => SetTriage(Triage.With(entry), dirty: true);

    /// <summary>Removes the entry for <paramref name="iscn"/> from the triage file.</summary>
    public void RemoveTriageEntry(string iscn)
    {
        if (Triage.Find(iscn) is not null)
        {
            SetTriage(Triage.Without(iscn), dirty: true);
        }
    }

    /// <summary>Removes a stale triage entry.</summary>
    [RelayCommand]
    private void RemoveStale(TriageEntry? entry)
    {
        if (entry is not null)
        {
            RemoveTriageEntry(entry.Iscn);
        }
    }

    /// <summary>Writes the report files (Markdown, CSV, triage draft, KB-example draft) into <paramref name="directory"/>.</summary>
    /// <returns>The files written; empty without a result or on a write problem (<see cref="StatusText"/> says why).</returns>
    public async Task<IReadOnlyList<string>> ExportReportAsync(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (Result is not { } result)
        {
            return [];
        }

        var triage = Triage;
        try
        {
            var files = await Task.Run(() => ComparisonReport.WriteAll(directory, result, triage)).ConfigureAwait(true);
            StatusText = $"Wrote {files.Count} report file(s) to {directory}.";
            return files;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Could not write the report to {directory}: {ex.Message}";
            return [];
        }
    }

    private void SetTriage(TriageFile triage, bool dirty)
    {
        Triage = triage;
        IsTriageDirty = dirty;
        if (Result is { } result)
        {
            Result = result.WithTriage(triage, _coverage);
            ShowResult();
        }
    }

    private void ShowResult()
    {
        StaleTriage.Clear();
        foreach (var entry in Result?.StaleTriage ?? [])
        {
            StaleTriage.Add(entry);
        }

        HasStaleTriage = StaleTriage.Count > 0;
        MatrixText = Result is { } r ? Matrix(r) : string.Empty;
        ShowRows();
    }

    private void ShowRows()
    {
        var items = Result?.Items ?? [];
        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i].Comparison?.Show(i < items.Length ? items[i] : null);
        }
    }

    internal static string Matrix(ComparisonResult r)
    {
        var m = r.Matrix;
        var mode = r.AutoCorrect ? "with autocorrection" : "as written";
        var line = $"ISCN {r.Edition} vs {r.Checker} ({mode}): {m.Compared} compared · {m.Agreeing} agree · {m.Disagreeing} disagree "
            + $"({m.OnlyCytoLexInvalid} only CytoLex ✗, {m.OnlyCheckerInvalid} only {r.Checker} ✗)";
        if (m.NotSupported > 0)
        {
            line += $" · {m.NotSupported} not supported by {r.Checker}";
        }

        if (m.NotCompared > 0)
        {
            line += $" · {m.NotCompared} not compared";
        }

        var auto = r.Items.Count(i => i.CytoLexAutoCorrected);
        if (auto > 0)
        {
            line += $" · {auto} valid for CytoLex after autocorrection";
        }

        var untriaged = r.Items.Count(i => i.IsDisagreement && i.Triage is not { Confirmed: true });
        if (untriaged > 0)
        {
            line += $" · {untriaged} not triaged";
        }

        if (!r.StaleTriage.IsEmpty)
        {
            line += $" · {r.StaleTriage.Length} stale triage entr{(r.StaleTriage.Length == 1 ? "y" : "ies")}";
        }

        return line;
    }

    private string ModeNote => AutoCorrect
        ? "with autocorrection: strings valid after autocorrection count as valid for CytoLex"
        : "as written: without autocorrection";

    private void UpdateStatus(TimeSpan? elapsed = null)
    {
        StatusText = State switch
        {
            ComparisonState.NotConfigured => $"{CheckerName} not configured: {_notConfigured} {CytoGpsPanelViewModel.SetupPointer} The CytoLex batch works without it.",
            ComparisonState.Running => $"Comparing {_items.Length} string(s) with {CheckerName} (ISCN {Edition}, {Mode})…",
            ComparisonState.Cancelled => "Comparison cancelled.",
            ComparisonState.Done when Result is { CheckerStatus: not ExternalBatchStatus.Completed } r =>
                $"{CheckerName} did not run ({r.CheckerStatus}): {r.CheckerMessage} No string was compared; the CytoLex results are not affected.",
            ComparisonState.Done => $"Compared {Result?.Items.Length ?? 0} string(s) with {CheckerName}"
                + (elapsed is { } t ? $" in {t.TotalSeconds:0.0} s." : "."),
            ComparisonState.Failed => StatusText,
            _ when !_hasBatchResult => $"Run the batch, then compare it with {CheckerName} (ISCN {Edition}, {ModeNote}).",
            _ => $"Ready to compare with {CheckerName} (ISCN {Edition}, {ModeNote}).",
        };
    }
}
