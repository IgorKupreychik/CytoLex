using CytoLex.External.CytoGps;

namespace CytoLex.External.Tests;

public sealed class CytoGpsCheckerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NotConfigured_EmptyConfig_ReturnsReasonPerItemWithoutRunningAnything()
    {
        using var fake = new FakeCytoGps();
        var config = new CytoGpsConfig(fake.RepoRoot, null, null, null, false, CytoGpsConfig.DefaultTimeout);
        var checker = new CytoGpsChecker(config, null, fake.Runner);

        var availability = checker.CheckAvailability();
        Assert.False(availability.IsConfigured);
        Assert.StartsWith("CytoGPS not configured: set 'source_dir'", availability.Reason, StringComparison.Ordinal);

        var result = await checker.CheckAsync(["46,XY", "47,XY,+21"], cancellationToken: Ct);
        Assert.Equal(ExternalBatchStatus.NotConfigured, result.Status);
        Assert.Equal(availability.Reason, result.Message);
        Assert.Equal(2, result.Items.Length);
        Assert.All(result.Items, i => Assert.Equal(ExternalVerdict.NotConfigured, i.Verdict));
        Assert.Equal(0, fake.Runner.JavacCalls + fake.Runner.JavaCalls);
    }

    [Theory]
    [InlineData("source_dir", "does not exist")]
    [InlineData("java_home", "is not a JDK")]
    [InlineData("javax_json_jar", "does not exist")]
    public void NotConfigured_WrongPath_NamesTheKey(string key, string expected)
    {
        using var fake = new FakeCytoGps();
        var c = fake.Config();
        var missing = Path.Combine(fake.Root, "missing");
        c = key switch
        {
            "source_dir" => c with { SourceDir = missing },
            "java_home" => c with { JavaHome = missing },
            _ => c with { JavaxJsonJar = missing },
        };

        var availability = new CytoGpsChecker(c, null, fake.Runner, fake.JarSha1).CheckAvailability();
        Assert.False(availability.IsConfigured);
        Assert.Contains(key, availability.Reason, StringComparison.Ordinal);
        Assert.Contains(expected, availability.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void NotConfigured_JarWithWrongHash_IsRejected()
    {
        using var fake = new FakeCytoGps();
        var availability = new CytoGpsChecker(fake.Config(), null, fake.Runner).CheckAvailability();
        Assert.False(availability.IsConfigured);
        Assert.Contains("expected " + CytoGpsBuild.JavaxJsonSha1, availability.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_NormalizesResultsInInputOrder()
    {
        using var fake = new FakeCytoGps();
        var checker = fake.Checker();
        Assert.True(checker.CheckAvailability().IsConfigured);

        var result = await checker.CheckAsync(["46,XY,del(5)(q13q99)", "46,XY,t(9;22)(q34;q11.2)×2", "", "46,XY"], cancellationToken: Ct);

        Assert.Equal(ExternalBatchStatus.Completed, result.Status);
        Assert.Equal("CytoGPS", result.Checker);
        Assert.Equal("2016", result.Edition);
        Assert.Equal(FakeCytoGps.Bands, result.Bands);
        Assert.Equal([ExternalVerdict.Invalid, ExternalVerdict.Valid, ExternalVerdict.Failed, ExternalVerdict.Valid], result.Items.Select(i => i.Verdict));
        Assert.Equal("Validation error", result.Items[0].ToolStatus);
        Assert.Equal(ExternalMessageKind.Validation, Assert.Single(result.Items[0].Messages).Kind);
        Assert.Equal("46,XY,t(9;22)(q34;q11.2)x2", result.Items[1].SentInput);
        Assert.Equal("46,XY,t(9;22)(q34;q11.2)×2", result.Items[1].Input);
        Assert.Equal([1, 0, 0], Assert.Single(result.Items[1].Clones).Loss);
        Assert.StartsWith("Empty string", result.Items[2].ToolStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("", fake.Runner.LastBatch);
    }

    [Fact]
    public async Task Check_CompilesOnceAndCachesInMemory()
    {
        using var fake = new FakeCytoGps();
        var checker = fake.Checker();

        await checker.CheckAsync(["46,XY", "46,XY", "47,XY,+21"], cancellationToken: Ct);
        Assert.Equal(["46,XY", "47,XY,+21"], fake.Runner.LastBatch); // distinct strings only
        var again = await checker.CheckAsync(["47,XY,+21", "46,XY"], cancellationToken: Ct);

        Assert.Equal(1, fake.Runner.JavacCalls);
        Assert.Equal(1, fake.Runner.JavaCalls);
        Assert.All(again.Items, i => Assert.Equal(ExternalVerdict.Valid, i.Verdict));

        // A new instance reuses the compiled build (fingerprint) but not the in-memory results.
        await fake.Checker().CheckAsync(["46,XY"], cancellationToken: Ct);
        Assert.Equal(1, fake.Runner.JavacCalls);
        Assert.Equal(2, fake.Runner.JavaCalls);
        Assert.False(Directory.Exists(Path.Combine(fake.Config().CacheDir, "results")));
    }

    [Fact]
    public async Task Check_SourceChange_Recompiles()
    {
        using var fake = new FakeCytoGps();
        await fake.Checker().CheckAsync(["46,XY"], cancellationToken: Ct);
        fake.Write("cytogps/src/main/KaryotypeRunner.java", "class KaryotypeRunner { int changed; }");
        await fake.Checker().CheckAsync(["46,XY"], cancellationToken: Ct);
        Assert.Equal(2, fake.Runner.JavacCalls);
    }

    [Fact]
    public async Task Check_DiskCache_OnlyWhenEnabled_AndReusedAcrossInstances()
    {
        using var fake = new FakeCytoGps();
        var first = await fake.Checker(diskCache: true).CheckAsync(["46,XY,del(5)(q13q99)", "46,XY"], cancellationToken: Ct);
        var results = Path.Combine(fake.Config().CacheDir, "results");
        Assert.True(Directory.Exists(results));

        var second = await fake.Checker(diskCache: true).CheckAsync(["46,XY", "46,XY,del(5)(q13q99)"], cancellationToken: Ct);
        Assert.Equal(1, fake.Runner.JavaCalls);
        Assert.Equal(first.Items.Reverse().Select(i => i.Verdict), second.Items.Select(i => i.Verdict));
        Assert.Equal(FakeCytoGps.Bands, second.Bands);
    }

    [Fact]
    public async Task Check_OutputMismatch_FailsTheUnmatchedItems()
    {
        using var fake = new FakeCytoGps();
        fake.Runner.DropLast = 1;
        var result = await fake.Checker().CheckAsync(["46,XY", "47,XY,+21"], cancellationToken: Ct);

        Assert.Equal(ExternalBatchStatus.Failed, result.Status);
        Assert.Contains("1 entries for 2 strings", result.Message, StringComparison.Ordinal);
        Assert.Equal(ExternalVerdict.Valid, result.Items[0].Verdict);
        Assert.Equal(ExternalVerdict.Failed, result.Items[1].Verdict);
    }

    [Fact]
    public async Task Check_ProcessFailure_FailsTheBatch()
    {
        using var fake = new FakeCytoGps();
        fake.Runner.ExitCode = 1;
        var result = await fake.Checker().CheckAsync(["46,XY"], cancellationToken: Ct);

        Assert.Equal(ExternalBatchStatus.Failed, result.Status);
        Assert.StartsWith("CytoGPS failed (exit 1)", result.Message, StringComparison.Ordinal);
        Assert.Equal(ExternalVerdict.Failed, Assert.Single(result.Items).Verdict);
    }
}
