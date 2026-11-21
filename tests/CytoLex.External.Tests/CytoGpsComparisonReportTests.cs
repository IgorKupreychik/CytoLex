using CytoLex.Core.Batch;
using CytoLex.Core.Validation;
using CytoLex.External.Comparison;
using CytoLex.External.CytoGps;

namespace CytoLex.External.Tests;

/// <summary>
/// The CytoLex vs. CytoGPS comparison report (T-051 acceptance), against the real local CytoGPS build. Skips with the
/// "not configured" reason without a <c>cytogps</c> setup in <c>config/local.yaml</c>. Input: every distinct string of the
/// KB sample batch <c>all.csv</c> (book strings only, T-048); triage: the committed
/// <c>tests/data/comparison/cytogps-2016-triage.yaml</c>. Writes <c>build/review/comparison/cytogps-2016.*</c>.
/// </summary>
public sealed class CytoGpsComparisonReportTests
{
    [Fact]
    public async Task SampleBatch_WritesTheComparisonReport()
    {
        var checker = CytoGpsChecker.FromLocalConfig();
        var availability = checker.CheckAvailability();
        if (!availability.IsConfigured)
        {
            Assert.Skip(availability.Reason!);
        }

        var read = BatchReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "data", "batch", "2016", "all.csv"));
        var items = read.Items.DistinctBy(i => i.Input, StringComparer.Ordinal).ToList();
        var triage = TriageFile.Load(Path.Combine(AppContext.BaseDirectory, "data", "comparison", "cytogps-2016-triage.yaml"));

        var result = await ComparisonEngine.CompareAsync(items, Validator.Default, checker, CytoGpsCoverage.Instance, triage,
            cancellationToken: TestContext.Current.CancellationToken);
        var dir = Path.Combine(checker.Config.RepoRoot, "build", "review", "comparison");
        var paths = ComparisonReport.WriteAll(dir, result, triage);

        Assert.True(result.CheckerStatus == ExternalBatchStatus.Completed, result.CheckerMessage);
        Assert.Equal(items.Count, result.Items.Length);
        Assert.Equal(items.Count, result.Matrix.Total);
        Assert.Equal(0, result.Matrix.NotCompared);
        Assert.True(result.Matrix.NotSupported > 0, "the sample has arr and detailed-system strings");
        Assert.True(result.Matrix.Compared > 0);
        Assert.All(result.Items.Where(i => i.Agreement == Agreement.NotSupported), i => Assert.NotNull(i.Reason));
        Assert.All(paths, p => Assert.True(File.Exists(p), p));
        TestContext.Current.SendDiagnosticMessage(
            $"Comparison: {result.Matrix} → {dir}");
    }
}
