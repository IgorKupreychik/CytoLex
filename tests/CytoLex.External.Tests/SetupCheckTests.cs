using CytoLex.External.CytoGps;

namespace CytoLex.External.Tests;

/// <summary>The per-field checks of the CytoGPS setup used by the desktop settings view (T-089).</summary>
public sealed class SetupCheckTests
{
    [Fact]
    public void Check_FakeSetup_EveryFieldOk()
    {
        using var fake = new FakeCytoGps();
        var report = CytoGpsSetupCheck.Check(fake.Config(), fake.JarSha1);

        Assert.True(report.IsConfigured);
        Assert.Equal(["source_dir", "java_home", "javax_json_jar"], report.Fields.Select(f => f.Key));
        Assert.All(report.Fields, f => Assert.True(f.IsSet && f.IsOk, f.Message));
    }

    [Fact]
    public void Check_EmptyConfig_EveryFieldNotSet()
    {
        using var fake = new FakeCytoGps();
        var report = CytoGpsSetupCheck.Check(fake.Config() with { SourceDir = null, JavaHome = null, JavaxJsonJar = null });

        Assert.False(report.IsConfigured);
        Assert.All(report.Fields, f => Assert.False(f.IsSet));
        Assert.All(report.Fields, f => Assert.Equal("Not set.", f.Message));
    }

    [Fact]
    public void Check_ReportsEachFieldOnItsOwn()
    {
        using var fake = new FakeCytoGps();
        var config = fake.Config() with { SourceDir = Path.Combine(fake.Root, "missing"), JavaHome = fake.Root };

        var report = CytoGpsSetupCheck.Check(config, fake.JarSha1);

        Assert.Contains("does not exist", report["source_dir"].Message, StringComparison.Ordinal);
        Assert.Contains("is not a JDK", report["java_home"].Message, StringComparison.Ordinal);
        Assert.True(report["javax_json_jar"].IsOk);

        // The adapter still names the first problem only, with the same message.
        var availability = new CytoGpsChecker(config, null, fake.Runner, fake.JarSha1).CheckAvailability();
        Assert.Equal("CytoGPS not configured: " + report["source_dir"].Message, availability.Reason);
    }

    [Fact]
    public void Check_NotASourceTree_AndWrongJar()
    {
        using var fake = new FakeCytoGps();
        var report = CytoGpsSetupCheck.Check(fake.Config() with { SourceDir = fake.Root });

        Assert.Contains("is not a CytoGPS source tree", report["source_dir"].Message, StringComparison.Ordinal);
        Assert.True(report["java_home"].IsOk);
        Assert.Contains("expected " + CytoGpsBuild.JavaxJsonSha1, report["javax_json_jar"].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_MissingAntlrJar()
    {
        using var fake = new FakeCytoGps();
        File.Delete(Path.Combine(fake.Root, "cytogps", CytoGpsBuild.AntlrJarPath));

        var report = CytoGpsSetupCheck.Check(fake.Config(), fake.JarSha1);

        Assert.Contains("the ANTLR runtime", report["source_dir"].Message, StringComparison.Ordinal);
    }
}
