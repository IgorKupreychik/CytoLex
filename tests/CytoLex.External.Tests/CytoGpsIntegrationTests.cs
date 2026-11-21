using CytoLex.External.CytoGps;

namespace CytoLex.External.Tests;

/// <summary>
/// The adapter against the real local CytoGPS build (T-049 acceptance). Skips with the "not configured" reason when
/// <c>config/local.yaml</c> has no complete <c>cytogps</c> section. Inputs: the KB sample batch (book strings only, T-048).
/// </summary>
public sealed class CytoGpsIntegrationTests
{
    [Fact]
    public async Task SampleBatch_ReturnsNormalizedResults()
    {
        var checker = CytoGpsChecker.FromLocalConfig();
        var availability = checker.CheckAvailability();
        if (!availability.IsConfigured)
        {
            Assert.Skip(availability.Reason!);
        }

        var sample = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "data", "batch", "2016", "karyotype.txt"))
            .Where(l => l.Length > 0)
            .ToList();
        // Spot checks from docs/research/cytogps.md §5.1.
        string[] spot = ["46,XY,del(5)(q33q13)", "46,XY,t(9;22)(q34;q11.2)", "46,XY,t(9;22)(q34;q11.2)×2"];

        var result = await checker.CheckAsync([.. sample, .. spot], cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Status == ExternalBatchStatus.Completed, result.Message);
        Assert.Equal(916, result.Bands.Length);
        Assert.Equal(sample.Count + spot.Length, result.Items.Length);
        Assert.All(result.Items, i =>
        {
            Assert.Contains(i.Verdict, new[] { ExternalVerdict.Valid, ExternalVerdict.Invalid });
            Assert.Contains(i.ToolStatus, CytoGpsJson.Statuses);
            Assert.All(i.Clones, c => Assert.Equal(916, c.Fusion.Length));
        });

        var byInput = result.Items.TakeLast(spot.Length).ToList();
        Assert.Equal(ExternalVerdict.Invalid, byInput[0].Verdict);
        var t922 = byInput[1];
        Assert.Equal(ExternalVerdict.Valid, t922.Verdict);
        var fused = Assert.Single(t922.Clones).Fusion
            .Select((count, i) => (count, band: result.Bands[i]))
            .Where(x => x.count > 0)
            .Select(x => x.band)
            .ToList();
        Assert.Contains("9q34.1", fused);
        Assert.Contains("9q34.3", fused);
        Assert.Contains("22q11.21", fused);
        Assert.Equal(ExternalVerdict.Valid, byInput[2].Verdict); // × sent as x
        Assert.Equal("46,XY,t(9;22)(q34;q11.2)x2", byInput[2].SentInput);
    }
}
