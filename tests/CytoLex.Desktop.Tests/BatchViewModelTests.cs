using CytoLex.Core.Batch;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-038: the Batch tab's view model (load → run → summary/filter → export → open in the single-string check).</summary>
public sealed class BatchViewModelTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cytolex-batch-vm-");

    private static string Sample(string file) => Path.Combine(AppContext.BaseDirectory, "data", "batch", "2016", file);

    public void Dispose() => _dir.Delete(recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir.FullName, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task Load_Run_Summary_Filter()
    {
        var main = new MainWindowViewModel();
        var batch = main.Batch;
        Assert.False(batch.RunCommand.CanExecute(null));

        await batch.LoadAsync(Write("in.csv", "id,iscn\nok,\"46,XX\"\nbad,46XX\nish,\"46,XX.ish 5p15.2(D5S23×2)\"\n"));
        Assert.Equal(3, batch.Items.Length);
        Assert.False(batch.HasWarnings);
        Assert.True(batch.RunCommand.CanExecute(null));
        Assert.False(batch.CanExport);

        await batch.RunCommand.ExecuteAsync(null);

        Assert.True(batch.HasResult);
        Assert.True(batch.CanExport);
        Assert.False(batch.IsRunning);
        Assert.Equal(["2016"], batch.Editions);
        Assert.Equal(3, batch.Rows.Count);
        Assert.Equal(batch.ProgressMax, batch.Progress);
        var summary = Assert.Single(batch.Summaries);
        Assert.StartsWith("ISCN 2016: 2 valid, 1 not valid of 3", summary, StringComparison.Ordinal);

        var bad = batch.Rows.Single(r => r.Id == "bad");
        Assert.True(bad.IsNotValid);
        Assert.StartsWith("✗ 1E", Assert.Single(bad.Cells).Text, StringComparison.Ordinal);
        Assert.Contains("KARYO.COUNT_SEX_ORDER", bad.FirstProblem, StringComparison.Ordinal);
        Assert.Equal("✓*", batch.Rows.Single(r => r.Id == "ish").Cells[0].Text);

        batch.SelectedFilter = batch.Filters.Single(f => f.Filter == BatchFilter.NotValid);
        Assert.Equal(["bad"], batch.Rows.Select(r => r.Id));
        batch.SelectedFilter = batch.Filters.Single(f => f.Filter == BatchFilter.Incomplete);
        Assert.Equal(["ish"], batch.Rows.Select(r => r.Id));
        batch.SelectedFilter = batch.Filters.Single(f => f.Filter == BatchFilter.Valid);
        Assert.Equal(["ok", "ish"], batch.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task AllEditions_OneCellPerEdition_AndEditionChangeDropsTheResult()
    {
        var batch = new MainWindowViewModel().Batch;
        batch.SelectedEditionChoice = EditionChoice.All;
        await batch.LoadAsync(Write("in.txt", "46,XX\n"));
        await batch.RunCommand.ExecuteAsync(null);

        var row = Assert.Single(batch.Rows);
        Assert.Equal(["2016", "2020", "2024"], row.Cells.Select(c => c.Edition));
        Assert.Equal(["✓", "–", "–"], row.Cells.Select(c => c.Text));
        Assert.Contains("ISCN 2020: not implemented", batch.Summaries);

        batch.SelectedEditionChoice = batch.EditionChoices[0];
        Assert.False(batch.HasResult);
        Assert.Empty(batch.Rows);
    }

    [Fact]
    public async Task LargeSample_RunsAndExports_RoundTrip()
    {
        var batch = new MainWindowViewModel().Batch;
        await batch.LoadAsync(Sample("large.csv"));
        Assert.True(batch.Items.Length >= 1000);

        await batch.RunCommand.ExecuteAsync(null);
        Assert.Equal(batch.Items.Length, batch.Rows.Count);

        var csv = Path.Combine(_dir.FullName, "out.csv");
        var json = Path.Combine(_dir.FullName, "out.json");
        await batch.ExportAsync(csv);
        await batch.ExportAsync(json);

        Assert.Equal(batch.Items, BatchReader.ReadFile(csv).Items);
        using var stream = File.OpenRead(json);
        Assert.Equal(batch.Items, BatchExport.ReadJson(stream).Items.Select(i => i.Item));

        // The exported CSV loads again as batch input.
        await batch.LoadAsync(csv);
        Assert.Equal(BatchReader.ReadFile(Sample("large.csv")).Items, batch.Items);
    }

    [Fact]
    public async Task Open_ShowsTheStringInTheSingleCheck()
    {
        var main = new MainWindowViewModel { SelectedMainTab = MainWindowViewModel.BatchTab };
        var batch = main.Batch;
        await batch.LoadAsync(Write("in.txt", "46XX\n"));
        await batch.RunCommand.ExecuteAsync(null);

        batch.SelectedRow = batch.Rows[0];
        batch.OpenSelectedCommand.Execute(null);

        Assert.Equal(MainWindowViewModel.SingleTab, main.SelectedMainTab);
        Assert.Equal("46XX", main.Input);
        Assert.Equal("2016", main.SelectedEditionChoice?.Edition);
        Assert.False(Assert.Single(main.Panels).IsValid);
    }

    [Fact]
    public async Task Load_ReaderWarnings_AndMissingFile()
    {
        var batch = new MainWindowViewModel().Batch;
        await batch.LoadAsync(Write("in.csv", "id,iscn\nx1,\nx2,\"46,XX\"\n"));
        Assert.True(batch.HasWarnings);
        Assert.Single(batch.Warnings);
        Assert.Single(batch.Items);

        await batch.LoadAsync(Path.Combine(_dir.FullName, "missing.txt"));
        Assert.StartsWith("Could not read missing.txt", batch.Status, StringComparison.Ordinal);
        Assert.Single(batch.Items); // the previous file stays loaded
    }
}
