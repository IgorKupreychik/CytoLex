using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CytoLex.External;
using CytoLex.External.Comparison;

namespace CytoLex.Desktop.ViewModels;

/// <summary>Where the CytoGPS panel is (T-081).</summary>
public enum CytoGpsState
{
    /// <summary>CytoGPS is not set up; nothing runs.</summary>
    NotConfigured,

    /// <summary>Not checked for the current input (yet).</summary>
    Idle,

    /// <summary>Waiting for the typing pause before an automatic check.</summary>
    Waiting,

    /// <summary>CytoGPS is running on the current input.</summary>
    Running,

    /// <summary>CytoGPS gave a verdict for the current input.</summary>
    Done,

    /// <summary>CytoGPS failed on the current input (build, crash, timeout, unreadable output).</summary>
    Failed,

    /// <summary>The check was cancelled.</summary>
    Cancelled,
}

/// <summary>
/// The CytoGPS view of the single-string check (T-081; a view tab since T-098): runs the external checker (the T-049 adapter) on demand, or
/// automatically after a typing pause, in the background, and shows its verdict, raw status, messages, the repaired string,
/// the string as sent and an agreement badge against the CytoLex verdict in the checker's edition. CytoGPS is optional:
/// nothing here feeds back into CytoLex, nothing throws into the app, a new input cancels the running check and a result
/// for an older input is never shown.
/// </summary>
public partial class CytoGpsPanelViewModel : ObservableObject
{
    /// <summary>The default typing pause before an automatic check.</summary>
    public static readonly TimeSpan DefaultAutoDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>Where to read how to set CytoGPS up.</summary>
    public const string SetupPointer = "To set it up, see README → \"CytoGPS (optional)\".";

    private readonly IExternalChecker? _checker;
    private readonly IExternalCoverage _coverage;
    private readonly Func<string, string, bool?> _cytoLexVerdict;
    private readonly TimeSpan _autoDelay;
    private readonly string? _notConfigured;
    private string _input = string.Empty;
    private CancellationTokenSource? _run;

    /// <summary>Creates the panel.</summary>
    /// <param name="checker">The external checker; <c>null</c> when it could not be created (<paramref name="setupError"/> says why).</param>
    /// <param name="setupError">Why there is no checker (e.g. malformed <c>config/local.yaml</c>).</param>
    /// <param name="cytoLexVerdict">The CytoLex verdict of (input, edition): valid, not valid, or <c>null</c> when not validated.</param>
    /// <param name="coverage">The notation the checker can't read (default: <see cref="CytoGpsCoverage"/>).</param>
    /// <param name="autoDelay">The typing pause before an automatic check (default <see cref="DefaultAutoDelay"/>).</param>
    public CytoGpsPanelViewModel(
        IExternalChecker? checker,
        string? setupError,
        Func<string, string, bool?> cytoLexVerdict,
        IExternalCoverage? coverage = null,
        TimeSpan? autoDelay = null)
    {
        ArgumentNullException.ThrowIfNull(cytoLexVerdict);
        _checker = checker;
        _cytoLexVerdict = cytoLexVerdict;
        _coverage = coverage ?? CytoGpsCoverage.Instance;
        _autoDelay = autoDelay ?? DefaultAutoDelay;
        Name = checker?.Name ?? "CytoGPS";
        Edition = checker?.Edition ?? "2016";
        _notConfigured = checker is null ? setupError ?? "no setup." : Availability(checker);
        State = _notConfigured is null ? CytoGpsState.Idle : CytoGpsState.NotConfigured;
        NotConfiguredReason = _notConfigured;
    }

    /// <summary>The checker's name, e.g. <c>CytoGPS</c>.</summary>
    public string Name { get; }

    /// <summary>The ISCN edition the checker follows; the agreement badge compares with CytoLex in this edition.</summary>
    public string Edition { get; }

    /// <summary>The panel's state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header), nameof(StateText), nameof(IsRunning), nameof(IsConfigured), nameof(HasVerdict),
        nameof(IsValid), nameof(IsInvalid), nameof(IsNotImplemented), nameof(IsNotConfigured))]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand), nameof(CancelCommand))]
    public partial CytoGpsState State { get; private set; }

    /// <summary>Whether CytoGPS is set up (a check can run).</summary>
    public bool IsConfigured => State != CytoGpsState.NotConfigured;

    /// <summary>Whether CytoGPS is not set up.</summary>
    public bool IsNotConfigured => State == CytoGpsState.NotConfigured;

    /// <summary>Whether a check is waiting or running.</summary>
    public bool IsRunning => State is CytoGpsState.Running or CytoGpsState.Waiting;

    /// <summary>Whether CytoGPS gave a verdict for the current input.</summary>
    public bool HasVerdict => State == CytoGpsState.Done;

    /// <summary>Why CytoGPS is not set up (<c>CytoGPS not configured: …</c> names the missing key, path or file).</summary>
    [ObservableProperty]
    public partial string? NotConfiguredReason { get; private set; }

    /// <summary>Why CytoGPS's grammar doesn't cover the input (e.g. <c>detailed system</c>), or <c>null</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnsupported), nameof(UnsupportedText))]
    public partial string? UnsupportedReason { get; private set; }

    /// <summary>Whether the input is outside CytoGPS's grammar.</summary>
    public bool IsUnsupported => UnsupportedReason is not null;

    /// <summary>The note for input outside CytoGPS's grammar.</summary>
    public string? UnsupportedText => UnsupportedReason is { } reason
        ? $"Not supported by {Name}: {reason}. Its result is shown but not compared with CytoLex."
        : null;

    /// <summary>The adapter's latest progress message while running.</summary>
    [ObservableProperty]
    public partial string? ProgressText { get; private set; }

    /// <summary>The checker's verdict for the current input (<see cref="HasVerdict"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header), nameof(StateText), nameof(IsValid), nameof(IsInvalid))]
    public partial ExternalVerdict? Verdict { get; private set; }

    /// <summary>The checker's own status (CytoGPS: one of its five statuses), or why the item failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToolStatus))]
    public partial string? ToolStatus { get; private set; }

    /// <summary>Whether <see cref="ToolStatus"/> is set.</summary>
    public bool HasToolStatus => ToolStatus is not null;

    /// <summary>The checker's messages.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessages))]
    public partial ImmutableArray<ExternalMessage> Messages { get; private set; } = [];

    /// <summary>Whether there are messages.</summary>
    public bool HasMessages => !Messages.IsDefaultOrEmpty;

    /// <summary>The checker's repaired string, when it repaired one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRevisedInput))]
    public partial string? RevisedInput { get; private set; }

    /// <summary>Whether the checker repaired the string.</summary>
    public bool HasRevisedInput => RevisedInput is not null;

    /// <summary>The string as sent to the checker (printed symbols as their typed ASCII forms).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSentInput))]
    public partial string? SentInput { get; private set; }

    /// <summary>Whether the sent string is known.</summary>
    public bool HasSentInput => SentInput is not null;

    /// <summary>The failure message (batch failure such as a compile error or timeout, or the item's own failure).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    public partial string? FailureText { get; private set; }

    /// <summary>Whether <see cref="FailureText"/> is set.</summary>
    public bool HasFailure => FailureText is not null;

    /// <summary>How the checker's verdict compares with CytoLex's (set with a verdict, or for unsupported notation).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAgreement), nameof(IsAgreeing), nameof(IsDisagreeing))]
    public partial Agreement? AgreementKind { get; private set; }

    /// <summary>The agreement badge, e.g. <c>Agrees with CytoLex 2016: both valid</c>.</summary>
    [ObservableProperty]
    public partial string? AgreementText { get; private set; }

    /// <summary>Whether the badge is shown.</summary>
    public bool HasAgreement => AgreementKind is not null;

    /// <summary>Whether both verdicts agree.</summary>
    public bool IsAgreeing => AgreementKind is Agreement.BothValid or Agreement.BothInvalid;

    /// <summary>Whether the verdicts disagree.</summary>
    public bool IsDisagreeing => AgreementKind is Agreement.OnlyCytoLexInvalid or Agreement.OnlyCheckerInvalid;

    /// <summary>Whether a check starts automatically after a typing pause (off by default: one Java process per check).</summary>
    [ObservableProperty]
    public partial bool IsAutoCheck { get; set; }

    /// <summary>The view tab header, e.g. <c>CytoGPS ✓ valid</c>.</summary>
    public string Header => State switch
    {
        CytoGpsState.NotConfigured => $"{Name} · not configured",
        CytoGpsState.Running or CytoGpsState.Waiting => $"{Name} …",
        CytoGpsState.Done when Verdict == ExternalVerdict.Valid => $"{Name} ✓ valid",
        CytoGpsState.Done when Verdict == ExternalVerdict.Invalid => $"{Name} ✗ not valid",
        CytoGpsState.Failed => $"{Name} · failed",
        _ => Name,
    };

    /// <summary>The state line of the panel.</summary>
    public string StateText => State switch
    {
        CytoGpsState.NotConfigured => $"{Name} not configured: {NotConfiguredReason}",
        CytoGpsState.Idle => $"Not checked yet. {Name} runs on demand (one Java process per check; ISCN {Edition}).",
        CytoGpsState.Waiting => $"Checking with {Name} after the typing pause…",
        CytoGpsState.Running => $"Running {Name}…",
        CytoGpsState.Done when Verdict == ExternalVerdict.Valid => $"Valid according to {Name} (ISCN {Edition})",
        CytoGpsState.Done => $"Not valid according to {Name} (ISCN {Edition})",
        CytoGpsState.Failed => $"{Name} failed; no verdict.",
        CytoGpsState.Cancelled => "Cancelled.",
        _ => string.Empty,
    };

    /// <summary>Whether CytoGPS accepts the input.</summary>
    public bool IsValid => State == CytoGpsState.Done && Verdict == ExternalVerdict.Valid;

    /// <summary>Whether CytoGPS rejects the input.</summary>
    public bool IsInvalid => State == CytoGpsState.Done && Verdict == ExternalVerdict.Invalid;

    /// <summary>Greyed-out header: not configured.</summary>
    public bool IsNotImplemented => State == CytoGpsState.NotConfigured;

    /// <summary>
    /// The input changed (called after every validation): cancels a running check and clears the result; starts an
    /// automatic check after the typing pause when <see cref="IsAutoCheck"/> is on. The same input again keeps the result.
    /// </summary>
    public void SetInput(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.Equals(input, _input, StringComparison.Ordinal))
        {
            return;
        }

        StopRun();
        _input = input;
        ClearResult();
        UnsupportedReason = string.IsNullOrWhiteSpace(input) ? null : _coverage.Unsupported(input);
        if (IsConfigured)
        {
            State = CytoGpsState.Idle;
            if (IsAutoCheck && CanCheck())
            {
                CurrentRun = RunAsync(_autoDelay);
            }
        }

        CheckCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The latest check (for tests and the window); completes when it ends, never faults.</summary>
    internal Task? CurrentRun { get; private set; }

    /// <summary>Checks the current input now.</summary>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private void Check() => CurrentRun = RunAsync(TimeSpan.Zero);

    private bool CanCheck() => _checker is not null && IsConfigured && !IsRunning && !string.IsNullOrWhiteSpace(_input);

    /// <summary>Cancels the running check.</summary>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    public void Cancel()
    {
        if (_run is null)
        {
            return;
        }

        StopRun();
        ProgressText = null;
        State = CytoGpsState.Cancelled;
    }

    partial void OnIsAutoCheckChanged(bool value)
    {
        if (value && State is CytoGpsState.Idle && CanCheck())
        {
            CurrentRun = RunAsync(_autoDelay);
        }
    }

    private async Task RunAsync(TimeSpan delay)
    {
        var checker = _checker!;
        StopRun();
        var run = new CancellationTokenSource();
        _run = run;
        var input = _input;
        var token = run.Token;
        ClearResult();
        try
        {
            if (delay > TimeSpan.Zero)
            {
                State = CytoGpsState.Waiting;
                await Task.Delay(delay, token).ConfigureAwait(true);
            }

            State = CytoGpsState.Running;
            var progress = new Progress<string>(message =>
            {
                if (_run == run && State == CytoGpsState.Running)
                {
                    ProgressText = message;
                }
            });

            // Off the UI thread: the adapter hashes and may compile the sources before its first await.
            var result = await Task.Run(() => checker.CheckAsync([input], progress, token), token).ConfigureAwait(true);
            if (_run == run && !token.IsCancellationRequested)
            {
                Show(input, result);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Cancelled by a new input (the state belongs to it) or by Cancel (which set the state).
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The adapter never throws for tool problems; anything else still stays in this view.
            if (_run == run)
            {
                ShowFailure($"{Name} check failed: {ex.Message}");
            }
        }
        finally
        {
            if (_run == run)
            {
                _run = null;
                ProgressText = null;
                CheckCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }

            run.Dispose();
        }
    }

    private void StopRun()
    {
        var run = _run;
        _run = null;
        if (run is not null)
        {
            try
            {
                run.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already finished.
            }
        }
    }

    private void Show(string input, ExternalBatchResult result)
    {
        var item = result.Items.IsDefaultOrEmpty ? null : result.Items[0];
        SentInput = item?.SentInput;
        if (result.Status == ExternalBatchStatus.NotConfigured)
        {
            NotConfiguredReason = result.Message ?? item?.ToolStatus;
            State = CytoGpsState.NotConfigured;
            return;
        }

        if (item is null || item.Verdict is not (ExternalVerdict.Valid or ExternalVerdict.Invalid))
        {
            ToolStatus = item?.ToolStatus;
            ShowFailure(result.Message ?? item?.ToolStatus ?? $"{Name} returned no result.");
            return;
        }

        Verdict = item.Verdict;
        ToolStatus = item.ToolStatus;
        Messages = item.Messages.IsDefault ? [] : item.Messages;
        RevisedInput = string.IsNullOrEmpty(item.RevisedInput) || string.Equals(item.RevisedInput, item.SentInput, StringComparison.Ordinal)
            ? null
            : item.RevisedInput;
        (AgreementKind, AgreementText) = Compare(item.Verdict, input);
        State = CytoGpsState.Done;
    }

    private void ShowFailure(string message)
    {
        FailureText = message;
        if (UnsupportedReason is not null)
        {
            (AgreementKind, AgreementText) = (Agreement.NotSupported, UnsupportedText);
        }

        State = CytoGpsState.Failed;
    }

    /// <summary>
    /// Compares the shown result with CytoLex's verdict again (the input is the same, but the verdict changed, e.g. the
    /// Autocorrect checkbox was toggled).
    /// </summary>
    public void RefreshAgreement()
    {
        if (State == CytoGpsState.Done && Verdict is { } verdict)
        {
            (AgreementKind, AgreementText) = Compare(verdict, _input);
        }
    }

    private (Agreement, string) Compare(ExternalVerdict verdict, string input)
    {
        if (UnsupportedReason is { } reason)
        {
            return (Agreement.NotSupported, $"Not compared: not supported by {Name} ({reason})");
        }

        if (_cytoLexVerdict(input, Edition) is not { } cytoLexValid)
        {
            return (Agreement.NotCompared, $"Not compared: CytoLex has no ISCN {Edition} verdict");
        }

        var checkerValid = verdict == ExternalVerdict.Valid;
        string Word(bool valid) => valid ? "valid" : "not valid";
        return (cytoLexValid, checkerValid) switch
        {
            (true, true) => (Agreement.BothValid, $"Agrees with CytoLex {Edition}: both valid"),
            (false, false) => (Agreement.BothInvalid, $"Agrees with CytoLex {Edition}: both not valid"),
            _ => (cytoLexValid ? Agreement.OnlyCheckerInvalid : Agreement.OnlyCytoLexInvalid,
                $"Disagrees: CytoLex {Edition} {Word(cytoLexValid)}, {Name} {Word(checkerValid)}"),
        };
    }

    private void ClearResult()
    {
        Verdict = null;
        ToolStatus = null;
        Messages = [];
        RevisedInput = null;
        SentInput = null;
        FailureText = null;
        AgreementKind = null;
        AgreementText = null;
        ProgressText = null;
    }

    private static string? Availability(IExternalChecker checker)
    {
        try
        {
            var availability = checker.CheckAvailability();
            return availability.IsConfigured ? null : availability.Reason ?? "unknown reason.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ex.Message;
        }
    }
}
