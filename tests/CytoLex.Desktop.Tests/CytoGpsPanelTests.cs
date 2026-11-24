using CytoLex.Desktop.ViewModels;
using CytoLex.External;
using CytoLex.External.Comparison;

namespace CytoLex.Desktop.Tests;

/// <summary>The CytoGPS tab of the single-string check (T-081), with a fake checker.</summary>
public class CytoGpsPanelTests
{
    private const string _valid = "46,XY,del(5)(q13q33)";
    private const string _invalid = "46XX";

    /// <summary>A checker whose answer the test controls; each call waits for <see cref="Release"/> unless it answers at once.</summary>
    private sealed class FakeChecker(ExternalAvailability? availability = null) : IExternalChecker
    {
        private TaskCompletionSource<Func<string, ExternalBatchResult>> _answer = NewSource();

        public string Name => "CytoGPS";

        public string Edition => "2016";

        public List<string> Inputs { get; } = [];

        public int Calls => Inputs.Count;

        public CancellationToken LastToken { get; private set; }

        /// <summary>When set, every call answers at once with it.</summary>
        public Func<string, ExternalBatchResult>? Immediate { get; set; }

        public TaskCompletionSource Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ExternalAvailability CheckAvailability() => availability ?? ExternalAvailability.Configured;

        public async Task<ExternalBatchResult> CheckAsync(IReadOnlyList<string> inputs, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            Inputs.Add(inputs[0]);
            LastToken = cancellationToken;
            Started.TrySetResult();
            if (Immediate is { } immediate)
            {
                return immediate(inputs[0]);
            }

            var answer = await _answer.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return answer(inputs[0]);
        }

        public void Release(Func<string, ExternalBatchResult> answer) => _answer.TrySetResult(answer);

        public void Reset()
        {
            _answer = NewSource();
            Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static TaskCompletionSource<Func<string, ExternalBatchResult>> NewSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ThrowingChecker : IExternalChecker
    {
        public string Name => "CytoGPS";

        public string Edition => "2016";

        public ExternalAvailability CheckAvailability() => ExternalAvailability.Configured;

        public Task<ExternalBatchResult> CheckAsync(IReadOnlyList<string> inputs, IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }

    private static ExternalBatchResult Answer(
        string input,
        ExternalVerdict verdict,
        string status,
        string? revised = null,
        string? sent = null,
        params ExternalMessage[] messages) =>
        new("CytoGPS", "2016", ExternalBatchStatus.Completed, null, [],
            [new ExternalItemResult(input, sent ?? input, verdict, status, revised, [.. messages], [])]);

    private static ExternalBatchResult Failed(string input, string message) =>
        new("CytoGPS", "2016", ExternalBatchStatus.Failed, message, [],
            [new ExternalItemResult(input, input, ExternalVerdict.Failed, message, null, [], [])]);

    private static CytoGpsPanelViewModel Panel(IExternalChecker? checker, Func<string, string, bool?>? verdict = null, TimeSpan? delay = null) =>
        new(checker, checker is null ? "no setup." : null, verdict ?? ((_, _) => true), autoDelay: delay ?? TimeSpan.Zero);

    private static async Task Settle(CytoGpsPanelViewModel panel)
    {
        if (panel.CurrentRun is { } run)
        {
            await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void NotConfigured_NoChecker_ShowsReasonAndCannotRun()
    {
        var panel = Panel(null);
        panel.SetInput(_valid);
        Assert.Equal(CytoGpsState.NotConfigured, panel.State);
        Assert.Equal("CytoGPS not configured: no setup.", panel.StateText);
        Assert.Equal("CytoGPS · not configured", panel.Header);
        Assert.True(panel.IsNotImplemented);
        Assert.False(panel.CheckCommand.CanExecute(null));
        Assert.Contains("CytoGPS (optional)", CytoGpsPanelViewModel.SetupPointer, StringComparison.Ordinal);
    }

    [Fact]
    public void NotConfigured_CheckerUnavailable_NeverCalled()
    {
        var checker = new FakeChecker(ExternalAvailability.NotConfigured("cytogps.source_dir is not set in config/local.yaml."));
        var panel = Panel(checker);
        panel.IsAutoCheck = true;
        panel.SetInput(_valid);
        Assert.Equal(CytoGpsState.NotConfigured, panel.State);
        Assert.Equal("CytoGPS not configured: cytogps.source_dir is not set in config/local.yaml.", panel.StateText);
        Assert.False(panel.CheckCommand.CanExecute(null));
        Assert.Equal(0, checker.Calls);
    }

    [Fact]
    public void NotConfigured_MainWindow_CytoGpsViewWithReason()
    {
        var vm = new MainWindowViewModel { Input = _valid };
        Assert.Equal(ResultView.CytoGps, Enum.GetValues<ResultView>()[^1]); // the last view tab
        Assert.DoesNotContain(vm.Panels, p => p.ChipText.StartsWith("CytoGPS", StringComparison.Ordinal));
        Assert.Equal(CytoGpsState.NotConfigured, vm.CytoGps.State);
        Assert.StartsWith("CytoGPS not configured: ", vm.CytoGps.StateText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Success_Valid_AgreesWithCytoLex()
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Valid, "VALID") };
        var vm = new MainWindowViewModel(null, Rules.RuleStore.DefaultPath, checker, null) { Input = _valid };
        Assert.Equal(CytoGpsState.Idle, vm.CytoGps.State);
        Assert.Equal(0, checker.Calls); // on demand only

        vm.CytoGps.CheckCommand.Execute(null);
        await Settle(vm.CytoGps);

        Assert.Equal([_valid], checker.Inputs);
        Assert.Equal(CytoGpsState.Done, vm.CytoGps.State);
        Assert.Equal(ExternalVerdict.Valid, vm.CytoGps.Verdict);
        Assert.Equal("VALID", vm.CytoGps.ToolStatus);
        Assert.Equal("CytoGPS ✓ valid", vm.CytoGps.Header);
        Assert.True(vm.CytoGps.IsValid);
        Assert.Equal(Agreement.BothValid, vm.CytoGps.AgreementKind);
        Assert.Equal("Agrees with CytoLex 2016: both valid", vm.CytoGps.AgreementText);
        Assert.True(vm.CytoGps.IsAgreeing);
    }

    [Fact]
    public async Task Success_Invalid_WithMessagesRepairAndSentForm()
    {
        var message = new ExternalMessage(ExternalMessageKind.Grammar, "missing comma");
        var checker = new FakeChecker
        {
            Immediate = s => Answer(s, ExternalVerdict.Invalid, "GRAMMAR_FAILED", revised: "46,XX", sent: s + "-sent", messages: message),
        };
        var panel = Panel(checker, verdict: (_, _) => false);
        panel.SetInput(_invalid);
        panel.CheckCommand.Execute(null);
        await Settle(panel);

        Assert.Equal(CytoGpsState.Done, panel.State);
        Assert.Equal("CytoGPS ✗ not valid", panel.Header);
        Assert.True(panel.IsInvalid);
        Assert.Equal("GRAMMAR_FAILED", panel.ToolStatus);
        Assert.Equal(message, Assert.Single(panel.Messages));
        Assert.Equal("46,XX", panel.RevisedInput);
        Assert.Equal(_invalid + "-sent", panel.SentInput);
        Assert.Equal(Agreement.BothInvalid, panel.AgreementKind);
    }

    [Fact]
    public async Task Disagreement_IsShownAsSuch()
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Invalid, "VALIDATION_FAILED") };
        var panel = Panel(checker, verdict: (_, _) => true);
        panel.SetInput(_valid);
        panel.CheckCommand.Execute(null);
        await Settle(panel);
        Assert.Equal(Agreement.OnlyCheckerInvalid, panel.AgreementKind);
        Assert.Equal("Disagrees: CytoLex 2016 valid, CytoGPS not valid", panel.AgreementText);
        Assert.True(panel.IsDisagreeing);
    }

    [Fact]
    public async Task Agreement_UsesCytoLex2016_EvenWhenAnotherEditionIsShown()
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Valid, "VALID") };
        var vm = new MainWindowViewModel(null, Rules.RuleStore.DefaultPath, checker, null);
        vm.SelectedEditionChoice = vm.EditionChoices.First(c => c.Edition == "2020");
        vm.Input = _invalid;
        Assert.DoesNotContain(vm.Panels, p => p.Edition == "2016");

        vm.CytoGps.CheckCommand.Execute(null);
        await Settle(vm.CytoGps);
        Assert.Equal(Agreement.OnlyCytoLexInvalid, vm.CytoGps.AgreementKind);
        Assert.Equal("Disagrees: CytoLex 2016 not valid, CytoGPS valid", vm.CytoGps.AgreementText);
    }

    [Fact]
    public async Task Agreement_FollowsTheAutocorrectCheckbox()
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Valid, "VALID") };
        var vm = new MainWindowViewModel(null, Rules.RuleStore.DefaultPath, checker, null) { Input = "46,xy" };
        Assert.True(vm.AutoCorrect);

        vm.CytoGps.CheckCommand.Execute(null);
        await Settle(vm.CytoGps);
        Assert.Equal(Agreement.BothValid, vm.CytoGps.AgreementKind); // valid after autocorrection counts as valid

        vm.AutoCorrect = false; // same input: no new check, the badge is compared again
        Assert.Equal(1, checker.Calls);
        Assert.Equal(Agreement.OnlyCytoLexInvalid, vm.CytoGps.AgreementKind);
    }

    [Fact]
    public async Task Failure_ShowsAdapterMessageInTabOnly()
    {
        var checker = new FakeChecker { Immediate = s => Failed(s, "CytoGPS build failed (javac exit 1):\nerror: x") };
        var vm = new MainWindowViewModel(null, Rules.RuleStore.DefaultPath, checker, null) { Input = _valid };
        var before = vm.SelectedPanel;
        vm.CytoGps.CheckCommand.Execute(null);
        await Settle(vm.CytoGps);

        Assert.Equal(CytoGpsState.Failed, vm.CytoGps.State);
        Assert.Equal("CytoGPS build failed (javac exit 1):\nerror: x", vm.CytoGps.FailureText);
        Assert.Equal("CytoGPS · failed", vm.CytoGps.Header);
        Assert.Null(vm.CytoGps.AgreementKind);
        Assert.Same(before, vm.SelectedPanel);
        Assert.True(vm.SelectedPanel!.IsValid);
    }

    [Fact]
    public async Task Failure_ThrowingChecker_StaysInTab()
    {
        var panel = Panel(new ThrowingChecker());
        panel.SetInput(_valid);
        panel.CheckCommand.Execute(null);
        await Settle(panel);
        Assert.Equal(CytoGpsState.Failed, panel.State);
        Assert.Equal("CytoGPS check failed: boom", panel.FailureText);
        Assert.True(panel.CheckCommand.CanExecute(null)); // can try again
    }

    [Fact]
    public async Task Timeout_IsAFailureWithTheAdapterMessage()
    {
        const string timeout = "CytoGPS timed out after 600 s (timeout_seconds).";
        var checker = new FakeChecker { Immediate = s => Failed(s, timeout) };
        var panel = Panel(checker);
        panel.SetInput(_valid);
        panel.CheckCommand.Execute(null);
        await Settle(panel);
        Assert.Equal(CytoGpsState.Failed, panel.State);
        Assert.Equal(timeout, panel.FailureText);
    }

    [Fact]
    public async Task Cancel_StopsTheRunAndCancelsTheToken()
    {
        var checker = new FakeChecker();
        var panel = Panel(checker);
        panel.SetInput(_valid);
        panel.CheckCommand.Execute(null);
        await checker.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(CytoGpsState.Running, panel.State);
        Assert.False(panel.CheckCommand.CanExecute(null));
        Assert.True(panel.CancelCommand.CanExecute(null));

        panel.CancelCommand.Execute(null);
        await Settle(panel);
        Assert.True(checker.LastToken.IsCancellationRequested);
        Assert.Equal(CytoGpsState.Cancelled, panel.State);
        Assert.Equal("Cancelled.", panel.StateText);

        checker.Release(s => Answer(s, ExternalVerdict.Valid, "VALID")); // too late: ignored
        Assert.Null(panel.Verdict);
    }

    [Fact]
    public async Task InputChangedWhileRunning_CancelsAndNeverShowsTheStaleResult()
    {
        var checker = new FakeChecker();
        var vm = new MainWindowViewModel(null, Rules.RuleStore.DefaultPath, checker, null) { Input = _valid };
        vm.CytoGps.CheckCommand.Execute(null);
        var first = vm.CytoGps.CurrentRun!;
        await checker.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var firstToken = checker.LastToken;

        vm.Input = _invalid;
        Assert.True(firstToken.IsCancellationRequested);
        Assert.Equal(CytoGpsState.Idle, vm.CytoGps.State);

        checker.Release(s => Answer(s, ExternalVerdict.Valid, "VALID"));
        await first.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(CytoGpsState.Idle, vm.CytoGps.State);
        Assert.Null(vm.CytoGps.Verdict);
        Assert.Null(vm.CytoGps.AgreementKind);
    }

    [Fact]
    public async Task SameInputAgain_KeepsTheResult()
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Valid, "VALID") };
        var vm = new MainWindowViewModel(null, Rules.RuleStore.DefaultPath, checker, null) { Input = _valid };
        vm.CytoGps.CheckCommand.Execute(null);
        await Settle(vm.CytoGps);

        vm.ShowCoordinates = true; // re-validates the same input
        Assert.Equal(CytoGpsState.Done, vm.CytoGps.State);
        Assert.Equal(1, checker.Calls);
    }

    [Theory]
    [InlineData("arr[GRCh38] 7q11.23(73330452_74769135)×1", "arr segment")]
    [InlineData("46,XX,der(1)(1pter→1q21::3q21→3qter)", "detailed system")]
    public async Task UnsupportedNotation_NotComparedButShown(string input, string reason)
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Invalid, "GRAMMAR_FAILED") };
        var panel = Panel(checker, verdict: (_, _) => true);
        panel.SetInput(input);
        Assert.Equal(reason, panel.UnsupportedReason);
        Assert.Equal($"Not supported by CytoGPS: {reason}. Its result is shown but not compared with CytoLex.", panel.UnsupportedText);

        panel.CheckCommand.Execute(null);
        await Settle(panel);
        Assert.Equal(ExternalVerdict.Invalid, panel.Verdict);
        Assert.Equal(Agreement.NotSupported, panel.AgreementKind);
        Assert.Equal($"Not compared: not supported by CytoGPS ({reason})", panel.AgreementText);
        Assert.False(panel.IsDisagreeing);
    }

    [Fact]
    public void AutoCheckOff_TypingNeverRunsCytoGps()
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Valid, "VALID") };
        var vm = new MainWindowViewModel(null, Rules.RuleStore.DefaultPath, checker, null);
        foreach (var text in new[] { "4", "46", "46,", "46,X", "46,XX" })
        {
            vm.Input = text;
        }

        Assert.False(vm.CytoGps.IsAutoCheck);
        Assert.Equal(0, checker.Calls);
        Assert.Null(vm.CytoGps.CurrentRun);
    }

    [Fact]
    public async Task AutoCheckOn_RunsOnceForTheLatestInput()
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Valid, "VALID") };
        var panel = Panel(checker, delay: TimeSpan.FromMilliseconds(200));
        panel.IsAutoCheck = true;
        panel.SetInput("46,X");
        Assert.Equal(CytoGpsState.Waiting, panel.State);
        panel.SetInput(_valid); // within the pause: the first one never runs
        await Settle(panel);

        Assert.Equal([_valid], checker.Inputs);
        Assert.Equal(CytoGpsState.Done, panel.State);
    }

    [Fact]
    public async Task AutoCheckTurnedOn_ChecksTheCurrentInput()
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Valid, "VALID") };
        var panel = Panel(checker);
        panel.SetInput(_valid);
        panel.IsAutoCheck = true;
        await Settle(panel);
        Assert.Equal([_valid], checker.Inputs);
    }

    [Fact]
    public void BlankInput_CannotRun()
    {
        var panel = Panel(new FakeChecker());
        panel.SetInput("  ");
        Assert.False(panel.CheckCommand.CanExecute(null));
    }

    [Fact]
    public void CytoLexResults_SameWithAndWithoutChecker()
    {
        string[] inputs = [_valid, _invalid, "47,XXX", "46,XX,del (5)(q13q33)", "46,XX.ish 5p15.2(D5S23×2)", "arr[GRCh38] 7q11.23(73330452_74769135)×1"];
        var without = new MainWindowViewModel();
        var with = new MainWindowViewModel(null, Rules.RuleStore.DefaultPath, new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Invalid, "X") }, null);
        foreach (var input in inputs)
        {
            without.Input = input;
            with.Input = input;
            Assert.Equal(Describe(without), Describe(with));
            Assert.Equal(without.Highlights, with.Highlights);
        }
    }

    private static string Describe(MainWindowViewModel vm) => string.Join(
        "\n",
        vm.Panels.Select(p => $"{p.ChipText}|{p.ValidityText}|{p.CountsText}|"
            + string.Join(";", p.Diagnostics.Select(d => $"{d.RuleId}@{d.Location}:{d.Message}"))));

    [Fact]
    public void CytoGpsView_StaysSelectedAcrossRevalidation_AndEditorKeepsEditionHighlights()
    {
        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Valid, "VALID") };
        var vm = new MainWindowViewModel(null, Rules.RuleStore.DefaultPath, checker, null) { Input = _invalid };
        var panel = vm.SelectedPanel;
        Assert.NotNull(panel);
        Assert.NotEmpty(vm.Highlights);

        vm.SelectedViewIndex = (int)ResultView.CytoGps;
        Assert.True(vm.IsCytoGpsViewSelected);
        Assert.Same(panel, vm.SelectedPanel);
        Assert.NotEmpty(vm.Highlights);

        vm.Input = "46XY";
        Assert.Equal(ResultView.CytoGps, vm.SelectedView);
        Assert.NotNull(vm.SelectedPanel);
        Assert.NotEmpty(vm.Highlights);

        vm.SelectedView = ResultView.Diagnostics;
        Assert.False(vm.IsCytoGpsViewSelected);
        Assert.Equal((int)ResultView.Diagnostics, vm.SelectedViewIndex);
    }

    [Fact]
    public async Task SetChecker_NotConfiguredToReady_AndBack()
    {
        var panel = Panel(null);
        panel.SetInput(_valid);
        Assert.Equal(CytoGpsState.NotConfigured, panel.State);

        var checker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Valid, "VALID") };
        panel.SetChecker(checker, null);
        Assert.Equal(CytoGpsState.Idle, panel.State);
        Assert.Null(panel.NotConfiguredReason);
        Assert.True(panel.CheckCommand.CanExecute(null));
        panel.CheckCommand.Execute(null);
        await Settle(panel);
        Assert.True(panel.IsValid);

        panel.SetChecker(null, "CytoGPS not configured: set 'source_dir'.");
        Assert.Equal(CytoGpsState.NotConfigured, panel.State);
        Assert.Equal("CytoGPS not configured: set 'source_dir'.", panel.StateText); // the prefix is not repeated
        Assert.Null(panel.Verdict);
        Assert.False(panel.CheckCommand.CanExecute(null));
    }

    [Fact]
    public async Task SetChecker_DuringARun_TheRunKeepsItsChecker_NextCheckUsesTheNewOne()
    {
        var oldChecker = new FakeChecker();
        var panel = Panel(oldChecker);
        panel.SetInput(_valid);
        panel.CheckCommand.Execute(null);
        await oldChecker.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var newChecker = new FakeChecker { Immediate = s => Answer(s, ExternalVerdict.Invalid, "INVALID") };
        panel.SetChecker(newChecker, null);
        Assert.Equal(CytoGpsState.Running, panel.State);
        Assert.False(oldChecker.LastToken.IsCancellationRequested);

        oldChecker.Release(s => Answer(s, ExternalVerdict.Valid, "VALID"));
        await Settle(panel);
        Assert.True(panel.IsValid); // the old checker's result is shown

        panel.CheckCommand.Execute(null);
        await Settle(panel);
        Assert.True(panel.IsInvalid);
        Assert.Equal(1, oldChecker.Calls);
        Assert.Equal(1, newChecker.Calls);
    }

    [Fact]
    public async Task SetChecker_NotConfiguredDuringARun_AppliesWhenTheRunEnds()
    {
        var checker = new FakeChecker();
        var panel = Panel(checker);
        panel.SetInput(_valid);
        panel.CheckCommand.Execute(null);
        await checker.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        panel.SetChecker(null, "gone");
        Assert.Equal(CytoGpsState.Running, panel.State);

        checker.Release(s => Answer(s, ExternalVerdict.Valid, "VALID"));
        await Settle(panel);
        Assert.Equal(CytoGpsState.NotConfigured, panel.State);
        Assert.Equal("gone", panel.NotConfiguredReason);
    }
}
