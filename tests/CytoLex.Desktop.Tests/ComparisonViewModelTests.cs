using System.Collections.Immutable;
using CytoLex.Core.Validation;
using CytoLex.Desktop.ViewModels;
using CytoLex.External;
using CytoLex.External.Comparison;

namespace CytoLex.Desktop.Tests;

/// <summary>T-052: the CytoGPS comparison in the Batch tab, with a fake checker (no process runs).</summary>
public sealed class ComparisonViewModelTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cytolex-comparison-vm-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static string Sample(string file) => Path.Combine(AppContext.BaseDirectory, "data", "batch", "2016", file);

    private string PathOf(string name) => Path.Combine(_dir.FullName, name);

    private string Write(string name, string content)
    {
        var path = PathOf(name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A checker that rejects the strings <see cref="Rejects"/> picks; with <see cref="Gate"/> set, it waits for it.</summary>
    private sealed class FakeChecker(ExternalBatchStatus status = ExternalBatchStatus.Completed) : IExternalChecker
    {
        public string Name => "CytoGPS";

        public string Edition => "2016";

        public int Calls { get; private set; }

        public Func<string, bool> Rejects { get; init; } = s => s.Contains("del(", StringComparison.Ordinal);

        public TaskCompletionSource? Gate { get; init; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Throws { get; init; }

        public ExternalAvailability CheckAvailability() => ExternalAvailability.Configured;

        public async Task<ExternalBatchResult> CheckAsync(IReadOnlyList<string> inputs, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            Started.TrySetResult();
            progress?.Report($"Running CytoGPS on {inputs.Count} string(s)");
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (Throws)
            {
                throw new InvalidOperationException("boom");
            }

            var message = status == ExternalBatchStatus.Failed ? "CytoGPS failed: java not found" : null;
            ImmutableArray<ExternalItemResult> items =
            [
                .. inputs.Select(s =>
                {
                    var verdict = status == ExternalBatchStatus.Failed ? ExternalVerdict.Failed
                        : Rejects(s) ? ExternalVerdict.Invalid : ExternalVerdict.Valid;
                    ImmutableArray<ExternalMessage> messages = verdict == ExternalVerdict.Invalid
                        ? [new ExternalMessage(ExternalMessageKind.Validation, "rejected by fake")]
                        : [];
                    return new ExternalItemResult(s, s, verdict, verdict == ExternalVerdict.Valid ? "Success" : "Validation error", null, messages, []);
                }),
            ];
            return new ExternalBatchResult(Name, Edition, status, message, [], items);
        }
    }

    private static BatchViewModel Batch(IExternalChecker? checker, string? triagePath = null, string? setupError = null)
    {
        var main = new MainWindowViewModel();
        var comparison = new ComparisonViewModel(Validator.Default, checker, setupError, triagePath);
        return new BatchViewModel(Validator.Default, main.EditionChoices, main.SelectedEditionChoice, null, comparison);
    }

    private static void Filter(BatchViewModel batch, BatchFilter filter) => batch.SelectedFilter = batch.Filters.Single(f => f.Filter == filter);

    [Fact]
    public async Task SampleBatch_ComparesEveryRow_FiltersDisagreements_KeepsUnsupportedOut()
    {
        var input = Write("sample.txt", File.ReadAllText(Sample("karyotype.txt")) + File.ReadAllText(Sample("arr.txt")));
        var checker = new FakeChecker();
        var batch = Batch(checker);
        var comparison = batch.Comparison;
        await batch.LoadAsync(input);
        Assert.False(comparison.CompareCommand.CanExecute(null)); // the batch has no result yet
        Assert.Contains("Run the batch", comparison.StatusText, StringComparison.Ordinal);

        await batch.RunCommand.ExecuteAsync(null);
        var cytoLexCells = batch.Rows.Select(r => r.Cells[0].Text).ToList();
        Assert.True(comparison.CompareCommand.CanExecute(null));
        await comparison.CompareCommand.ExecuteAsync(null);

        Assert.Equal(ComparisonState.Done, comparison.State);
        Assert.Equal(1, checker.Calls);
        var result = comparison.Result!;
        Assert.Equal(batch.Items.Length, result.Matrix.Total);
        Assert.True(result.Matrix.Disagreeing > 0);
        Assert.True(result.Matrix.NotSupported >= File.ReadAllLines(Sample("arr.txt")).Length);
        Assert.StartsWith("ISCN 2016 vs CytoGPS (with autocorrection):", comparison.MatrixText, StringComparison.Ordinal);
        Assert.Equal(cytoLexCells, batch.Rows.Select(r => r.Cells[0].Text)); // CytoLex results untouched
        Assert.All(batch.Rows, r => Assert.True(r.Comparison!.HasItem));
        Assert.Contains(batch.Rows, r => r.Comparison!.AgreementText == "n/s");

        Filter(batch, BatchFilter.Disagreements);
        Assert.Equal(result.Matrix.Disagreeing, batch.Rows.Count);
        Assert.All(batch.Rows, r => Assert.StartsWith("≠", r.Comparison!.AgreementText, StringComparison.Ordinal));
        Filter(batch, BatchFilter.Untriaged);
        Assert.Equal(result.Matrix.Disagreeing, batch.Rows.Count); // no triage file

        // Selecting a disagreement fills the side-by-side detail.
        var row = batch.Rows.First(r => r.Comparison!.Item!.Agreement == Agreement.OnlyCheckerInvalid);
        Assert.Contains("✓ valid", row.Comparison!.CytoLexText, StringComparison.Ordinal);
        Assert.Equal("CytoGPS: ✗ not valid (Validation error)", row.Comparison.CheckerText);
        Assert.Contains("rejected by fake", row.Comparison.CheckerDetails, StringComparison.Ordinal);

        // A re-run of the batch keeps the comparison; a new file drops it.
        Filter(batch, BatchFilter.All);
        await batch.RunCommand.ExecuteAsync(null);
        Assert.Same(result, comparison.Result);
        Assert.All(batch.Rows, r => Assert.True(r.Comparison!.HasItem));
        await batch.LoadAsync(Write("other.txt", "46,XX\n"));
        Assert.Null(comparison.Result);
        Assert.Equal(ComparisonState.Idle, comparison.State);
    }

    [Fact]
    public async Task ValidAfterAutocorrection_CountsAsValid_FollowingTheCheckbox()
    {
        const string cased = "46,xy";
        const string spaced = "47,XY, +21";
        var checker = new FakeChecker();
        var batch = Batch(checker);
        var comparison = batch.Comparison;
        Assert.True(batch.AutoCorrect);
        Assert.True(comparison.AutoCorrect);
        Assert.Contains("with autocorrection", comparison.CompareText, StringComparison.Ordinal);

        await batch.LoadAsync(Write("in.txt", $"{cased}\n{spaced}\n"));
        await batch.RunCommand.ExecuteAsync(null);
        await comparison.CompareCommand.ExecuteAsync(null);

        Assert.True(comparison.Result!.AutoCorrect);
        Assert.Equal([Agreement.BothValid, Agreement.BothValid], comparison.Result.Items.Select(i => i.Agreement));
        Assert.All(comparison.Result.Items, i => Assert.True(i.CytoLexAutoCorrected));
        Assert.Contains("2 valid for CytoLex after autocorrection", comparison.MatrixText, StringComparison.Ordinal);
        var row = batch.Rows[0].Comparison!;
        Assert.Equal("CytoLex ISCN 2016: ✓ valid after autocorrection", row.CytoLexText);
        Assert.Contains("Corrected: 46,XY", row.CytoLexRules, StringComparison.Ordinal);

        // Unticking drops the comparison (and the batch result); compared again, they are CytoLex-only rejections.
        batch.AutoCorrect = false;
        Assert.False(comparison.AutoCorrect);
        Assert.Null(comparison.Result);
        Assert.Contains("as written", comparison.CompareText, StringComparison.Ordinal);
        await batch.RunCommand.ExecuteAsync(null);
        await comparison.CompareCommand.ExecuteAsync(null);

        Assert.False(comparison.Result!.AutoCorrect);
        Assert.Equal([Agreement.OnlyCytoLexInvalid, Agreement.OnlyCytoLexInvalid], comparison.Result.Items.Select(i => i.Agreement));
        Assert.Equal("CytoLex ISCN 2016: ✗ not valid", batch.Rows[0].Comparison!.CytoLexText);
        Assert.Equal(2, checker.Calls);
    }

    [Fact]
    public async Task NotConfigured_SaysWhy_AndTheBatchStillRuns()
    {
        var batch = new MainWindowViewModel().Batch;
        var comparison = batch.Comparison;
        Assert.Equal(ComparisonState.NotConfigured, comparison.State);
        Assert.StartsWith("CytoGPS not configured:", comparison.StatusText, StringComparison.Ordinal);
        Assert.Contains(CytoGpsPanelViewModel.SetupPointer, comparison.StatusText, StringComparison.Ordinal);

        await batch.LoadAsync(Write("in.txt", "46,XX\n46XX\n"));
        await batch.RunCommand.ExecuteAsync(null);
        Assert.Equal(2, batch.Rows.Count);
        Assert.False(comparison.CompareCommand.CanExecute(null));
        Assert.Equal(ComparisonState.NotConfigured, comparison.State);
        Assert.All(batch.Rows, r => Assert.False(r.Comparison!.HasItem));
    }

    [Fact]
    public async Task CheckerFails_NothingCompared_CytoLexUntouched()
    {
        var batch = Batch(new FakeChecker(ExternalBatchStatus.Failed));
        await batch.LoadAsync(Write("in.txt", "46,XX\n46XX\n"));
        await batch.RunCommand.ExecuteAsync(null);
        await batch.Comparison.CompareCommand.ExecuteAsync(null);

        Assert.Equal(ComparisonState.Done, batch.Comparison.State);
        Assert.Contains("did not run", batch.Comparison.StatusText, StringComparison.Ordinal);
        Assert.Contains("java not found", batch.Comparison.StatusText, StringComparison.Ordinal);
        Assert.Equal(2, batch.Comparison.Result!.Matrix.NotCompared);
        Assert.All(batch.Rows, r => Assert.Equal("–", r.Comparison!.AgreementText));
        Assert.Equal(["✓", "✗ 1E"], batch.Rows.Select(r => r.Cells[0].Text[..Math.Min(4, r.Cells[0].Text.Length)]));
    }

    [Fact]
    public async Task CheckerThrows_ShowsFailure()
    {
        var batch = Batch(new FakeChecker { Throws = true });
        await batch.LoadAsync(Write("in.txt", "46,XX\n"));
        await batch.RunCommand.ExecuteAsync(null);
        await batch.Comparison.CompareCommand.ExecuteAsync(null);

        Assert.Equal(ComparisonState.Failed, batch.Comparison.State);
        Assert.Contains("boom", batch.Comparison.StatusText, StringComparison.Ordinal);
        Assert.Single(batch.Rows);
    }

    [Fact]
    public async Task Cancel_StopsTheRun()
    {
        var checker = new FakeChecker { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var batch = Batch(checker);
        await batch.LoadAsync(Write("in.txt", "46,XX\n"));
        await batch.RunCommand.ExecuteAsync(null);

        var run = batch.Comparison.CompareCommand.ExecuteAsync(null);
        await checker.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(batch.Comparison.IsRunning);
        Assert.False(batch.Comparison.CompareCommand.CanExecute(null));
        batch.Comparison.CancelRun();
        await run;

        Assert.Equal(ComparisonState.Cancelled, batch.Comparison.State);
        Assert.Null(batch.Comparison.Result);
    }

    [Fact]
    public async Task Triage_EditSaveReopen_StaleEntries_AndReport()
    {
        const string disagreeing = "46,XX,del(5)(q13q33)"; // CytoLex valid, fake rejects
        const string agreeing = "46,XX";
        var triagePath = PathOf("triage.yaml");
        File.WriteAllText(triagePath, "# Synthetic test triage.\nformat: cytolex-triage\nversion: 1\nchecker: \"CytoGPS\"\nedition: \"2016\"\n"
            + "entries:\n  - iscn: \"46,XY,old\"\n    label: our-bug\n    confirmed: true\n");
        var batch = Batch(new FakeChecker(), triagePath);
        var comparison = batch.Comparison;
        Assert.Equal("Triage: triage.yaml · 1 entry", comparison.TriageText);

        await batch.LoadAsync(Write("in.txt", $"{agreeing}\n{disagreeing}\n"));
        await batch.RunCommand.ExecuteAsync(null);
        await comparison.CompareCommand.ExecuteAsync(null);

        Assert.Equal("46,XY,old", Assert.Single(comparison.StaleTriage).Iscn);
        var row = batch.Rows.Single(r => r.Input == disagreeing).Comparison!;
        Assert.True(row.IsUntriaged);
        Assert.Equal("not triaged", row.TriageText);
        Assert.False(batch.Rows.Single(r => r.Input == agreeing).Comparison!.ApplyTriageCommand.CanExecute(null));

        row.EditLabel = TriageLabelChoice.Of(TriageLabel.OurBug);
        row.EditConfirmed = true;
        row.EditNote = "synthetic";
        row.ApplyTriageCommand.Execute(null);

        Assert.True(comparison.IsTriageDirty);
        Assert.Equal("our-bug", row.TriageText);
        Assert.False(row.IsUntriaged);
        Assert.True(row.RemoveTriageCommand.CanExecute(null));
        Filter(batch, BatchFilter.Untriaged);
        Assert.Empty(batch.Rows);

        comparison.RemoveStaleCommand.Execute(comparison.StaleTriage[0]);
        Assert.Empty(comparison.StaleTriage);
        Assert.False(comparison.HasStaleTriage);

        Assert.True(comparison.SaveTriage());
        Assert.False(comparison.IsTriageDirty);
        var saved = File.ReadAllText(triagePath);
        Assert.StartsWith("# Synthetic test triage.\n", saved, StringComparison.Ordinal);
        var entry = Assert.Single(TriageFile.Load(triagePath).Entries);
        Assert.Equal((disagreeing, TriageLabel.OurBug, true, "synthetic"), (entry.Iscn, entry.Label, entry.Confirmed, entry.Note));

        // Remove → back to untriaged; reopen the saved file → labelled again.
        row.RemoveTriageCommand.Execute(null);
        Assert.Equal("not triaged", row.TriageText);
        Assert.True(comparison.OpenTriage(triagePath));
        Assert.Equal("our-bug", row.TriageText);
        Assert.Equal("synthetic", row.EditNote);

        // A broken or foreign triage file keeps the current one.
        Assert.False(comparison.OpenTriage(Write("bad.yaml", "format: nope\n")));
        Assert.True(comparison.HasTriageError);
        Assert.False(comparison.OpenTriage(Write("other.yaml", "format: cytolex-triage\nversion: 1\nchecker: \"X\"\nedition: \"2016\"\nentries: []\n")));
        Assert.Equal(triagePath, comparison.TriagePath);

        var files = await comparison.ExportReportAsync(Directory.CreateDirectory(PathOf("report")).FullName);
        Assert.Equal(4, files.Count);
        Assert.All(files, f => Assert.True(File.Exists(f)));
    }

    /// <summary>The real local CytoGPS on the karyotype sample with the committed triage file (skips when not configured).</summary>
    [Fact]
    public async Task RealCytoGps_OnTheKaryotypeSample()
    {
        var (checker, error) = ExternalCheckers.CytoGpsSetup.TryCreate();
        if (checker is null || checker.CheckAvailability() is { IsConfigured: false })
        {
            Assert.Skip("CytoGPS not configured: " + (error ?? checker?.CheckAvailability().Reason));
        }

        var batch = Batch(checker, ComparisonViewModel.DefaultTriagePath());
        await batch.LoadAsync(Sample("karyotype.txt"));
        await batch.RunCommand.ExecuteAsync(null);
        await batch.Comparison.CompareCommand.ExecuteAsync(null);

        var result = batch.Comparison.Result!;
        Assert.Equal(ExternalBatchStatus.Completed, result.CheckerStatus);
        Assert.Equal(batch.Items.Length, result.Matrix.Total);
        Assert.True(result.Matrix.Compared > 0);
        Assert.All(batch.Rows, r => Assert.NotEmpty(r.Comparison!.AgreementText));
        Assert.False(batch.Comparison.IsTriageDirty);
        TestContext.Current.SendDiagnosticMessage(batch.Comparison.MatrixText);
    }

    [Fact]
    public void MissingTriageFile_StartsEmpty_AndSaveNeedsAPath()
    {
        var comparison = Batch(new FakeChecker(), PathOf("missing.yaml")).Comparison;
        Assert.Null(comparison.TriagePath);
        Assert.Empty(comparison.Triage.Entries);
        Assert.False(comparison.SaveTriage());
        Assert.True(comparison.SaveTriageAs(PathOf("new.yaml")));
        Assert.Equal("Triage: new.yaml · 0 entries", comparison.TriageText);
    }
}
