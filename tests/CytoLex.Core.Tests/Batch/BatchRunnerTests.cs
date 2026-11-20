using System.Diagnostics;
using System.Text.Json;
using CytoLex.Core.Batch;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Batch;

/// <summary>T-038: batch runs over the sample files (1,000+ strings), summaries, cancellation; the sample files themselves.</summary>
public class BatchRunnerTests(ITestOutputHelper output)
{
    private static Validator V => Validator.Default;

    [Fact]
    public async Task LargeBatch_Completes_InInputOrder_AsSequentialValidation()
    {
        var read = BatchReader.ReadFile(BatchSamples.PathOf("large.csv"));
        Assert.Empty(read.Warnings);
        Assert.True(read.Items.Length >= 1000, $"large.csv has {read.Items.Length} strings");

        var options = new ValidationOptions { Editions = EditionSelection.All };
        var progress = new List<int>();
        var watch = Stopwatch.StartNew();
        var result = await BatchRunner.RunAsync(V, read.Items, options, new SyncProgress(progress.Add), TestContext.Current.CancellationToken);
        watch.Stop();
        output.WriteLine($"{read.Items.Length} strings × {result.Editions.Length} editions in {watch.ElapsedMilliseconds} ms");

        Assert.Equal(V.KnownEditions, result.Editions);
        Assert.Equal(read.Items, result.Items.Select(i => i.Item));
        Assert.Equal(read.Items.Length, progress.Max());
        foreach (var (item, report) in result.Items.Take(300))
        {
            Assert.Equal(item.Input, report.Input);
            Assert.Equal(
                JsonSerializer.Serialize(V.Validate(item.Input, options), Validator.JsonOptions),
                JsonSerializer.Serialize(report, Validator.JsonOptions));
        }
    }

    [Fact]
    public async Task Cancellation_StopsTheRun()
    {
        var items = BatchReader.ReadFile(BatchSamples.PathOf("large.csv")).Items;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BatchRunner.RunAsync(V, items, null, null, cts.Token));
    }

    [Fact]
    public void Summary_CountsPerEdition()
    {
        List<BatchItem> items = [new(1, null, "46,XX"), new(2, null, "46XX"), new(3, null, "46,XX.ish 5p15.2(D5S23×2)"), new(4, null, "47,XY,+23")];

        var result = BatchRunner.Run(V, items, new ValidationOptions { Editions = EditionSelection.All });
        var summaries = BatchSummary.Of(result);

        Assert.Equal(V.KnownEditions, summaries.Select(s => s.Edition));
        var s2016 = summaries.Single(s => s.Edition == "2016");
        Assert.Equal(4, s2016.Total);
        Assert.Equal(2, s2016.Valid);
        Assert.Equal(2, s2016.NotValid);
        Assert.Equal(0, s2016.NotImplemented);
        Assert.Equal(1, s2016.Incomplete); // the ish segment
        Assert.Contains(s2016.TopRules, r => r.RuleId == "KARYO.COUNT_SEX_ORDER" && r.Strings == 1);
        Assert.All(summaries.Where(s => s.Edition != "2016"), s => Assert.Equal(4, s.NotImplemented));
    }

    [Fact]
    public void EmptyBatch()
    {
        var result = BatchRunner.Run(V, []);

        Assert.Empty(result.Items);
        Assert.Equal([V.DefaultEdition], result.Editions);
    }

    [Theory]
    [InlineData("karyotype.txt")]
    [InlineData("arr.txt")]
    [InlineData("combined.txt")]
    [InlineData("all.csv")]
    [InlineData("all.tsv")]
    [InlineData("large.csv")]
    public void SampleFile_ReadsWithoutWarnings(string file)
    {
        var read = BatchReader.ReadFile(BatchSamples.PathOf(file));

        Assert.Empty(read.Warnings);
        Assert.NotEmpty(read.Items);
    }

    [Fact]
    public void SampleFiles_TypeColumn_MatchesSegmenter()
    {
        var warnings = new List<string>();
        var records = Csv.Parse(File.ReadAllText(BatchSamples.PathOf("all.csv")), ',', warnings).Where(r => !r.IsBlank).ToList();
        Assert.Empty(warnings);
        var header = records[0].Fields.ToList();
        int iscn = header.IndexOf("iscn"), type = header.IndexOf("type");

        var mismatches = records.Skip(1)
            .Select(r => (Input: r.Fields[iscn], Declared: r.Fields[type], Actual: string.Join('+', Segmenter.Split(r.Fields[iscn]).Select(s => s.Type.ToKbName()))))
            .Where(x => x.Declared != x.Actual)
            .Select(x => $"{x.Input}: {x.Declared} ≠ {x.Actual}")
            .ToList();
        Assert.Empty(mismatches);
    }

    [Theory]
    [InlineData("karyotype.txt", "karyotype")]
    [InlineData("arr.txt", "arr")]
    public void SampleTextFiles_HoldTheirType(string file, string type)
    {
        var items = BatchReader.ReadFile(BatchSamples.PathOf(file)).Items;

        Assert.All(items, i => Assert.Equal(type, Assert.Single(Segmenter.Split(i.Input)).Type.ToKbName()));
    }

    /// <summary>Reports on the calling thread (<see cref="Progress{T}"/> posts to the thread pool).</summary>
    private sealed class SyncProgress(Action<int> report) : IProgress<int>
    {
        private readonly Lock _lock = new();

        public void Report(int value)
        {
            lock (_lock)
            {
                report(value);
            }
        }
    }
}
