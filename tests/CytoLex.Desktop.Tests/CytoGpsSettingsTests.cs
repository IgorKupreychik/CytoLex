using System.Security.Cryptography;
using CytoLex.Desktop.ViewModels;
using CytoLex.External;
using CytoLex.External.CytoGps;

namespace CytoLex.Desktop.Tests;

/// <summary>The Settings tab's CytoGPS setup (T-089) on a temp repo root with a fake CytoGPS setup (no Java runs).</summary>
public sealed class CytoGpsSettingsTests : IDisposable
{
    private const string _localYaml = """
        # My machine.
        iscn_pdf:
          "2016": "D:/books/ISCN 2016.pdf"   # keep me

        # CytoGPS (T-049)
        cytogps:
          source_dir: "C:/nowhere"          # unpacked source
          java_home: "C:/nojdk"
          disk_cache: false                 # T-048
        """;

    private const string _template = """
        # Copy to config/local.yaml (gitignored) and adjust paths for your machine.
        iscn_pdf:
          "2016": "C:/path/to/ISCN_2016.pdf"

        # cytogps:
        #   source_dir: "C:/path/to/CytoGPS"                  # unpacked CytoGPS
        #   java_home: "C:/path/to/jdk-21"                    # a JDK
        #   javax_json_jar: "C:/path/to/javax.json-1.1.4.jar" # SHA-1 checked
        #   disk_cache: false                                 # cache results
        #   timeout_seconds: 600                              # limit

        """;

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("cytolex-settings-");

    public CytoGpsSettingsTests()
    {
        Directory.CreateDirectory(Path.Combine(Repo, "config"));
        Write("cytogps/src/toolkit/AggregateJsonForBatchFile.java", "class AggregateJsonForBatchFile {}");
        Write("cytogps/target/generated-sources/antlr4/compiler/KaryotypeParser.java", "class KaryotypeParser {}");
        Write("cytogps/" + CytoGpsBuild.AntlrJarPath, "antlr");
        var exe = OperatingSystem.IsWindows() ? ".exe" : "";
        Write("jdk/bin/java" + exe, "");
        Write("jdk/bin/javac" + exe, "");
        Write("javax.json-1.1.4.jar", "javax.json");
    }

    private string Repo => Path.Combine(_root.FullName, "repo");

    private string ConfigPath => Path.Combine(Repo, "config", "local.yaml");

    private string SourceDir => Path.Combine(_root.FullName, "cytogps").Replace('\\', '/');

    private string JavaHome => Path.Combine(_root.FullName, "jdk").Replace('\\', '/');

    private string Jar => Path.Combine(_root.FullName, "javax.json-1.1.4.jar").Replace('\\', '/');

#pragma warning disable CA5350 // test fixture: matches the adapter's SHA-1 integrity check
    private string JarSha1 => Convert.ToHexStringLower(SHA1.HashData(File.ReadAllBytes(Jar)));
#pragma warning restore CA5350

    public void Dispose()
    {
        try
        {
            _root.Delete(recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root.FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private CytoGpsSettingsViewModel Settings(
        Func<string, string?>? environment = null,
        Func<CytoGpsConfig, IExternalChecker>? createChecker = null,
        Action? applied = null,
        string? repo = "")
    {
        var sha = JarSha1;
        return new CytoGpsSettingsViewModel(
            repo == "" ? Repo : repo,
            environment ?? (_ => null),
            c => CytoGpsSetupCheck.Check(c, sha),
            createChecker,
            applied);
    }

    private void FillValidSetup(CytoGpsSettingsViewModel settings)
    {
        settings.SourceDir.Text = SourceDir;
        settings.JavaHome.Text = JavaHome;
        settings.JavaxJsonJar.Text = Jar;
    }

    [Fact]
    public void Load_ShowsTheFileValues()
    {
        File.WriteAllText(ConfigPath, _localYaml);

        var settings = Settings();

        Assert.True(settings.IsEditable);
        Assert.Equal("C:/nowhere", settings.SourceDir.Text);
        Assert.Equal("C:/nojdk", settings.JavaHome.Text);
        Assert.Equal(string.Empty, settings.JavaxJsonJar.Text);
        Assert.Equal(string.Empty, settings.TimeoutSeconds.Text);
        Assert.False(settings.DiskCache);
        Assert.False(settings.IsDirty);
        Assert.False(settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Checks_PerField_FollowTheTyping()
    {
        File.WriteAllText(ConfigPath, _localYaml);
        var settings = Settings();

        Assert.True(settings.SourceDir.IsProblem);
        Assert.Contains("does not exist", settings.SourceDir.StatusText, StringComparison.Ordinal);
        Assert.Contains("is not a JDK", settings.JavaHome.StatusText, StringComparison.Ordinal);
        Assert.Equal("Not set.", settings.JavaxJsonJar.StatusText);
        Assert.False(settings.IsSetupOk);

        settings.SourceDir.Text = SourceDir;
        Assert.True(settings.SourceDir.IsOk);
        Assert.True(settings.JavaHome.IsProblem);

        settings.JavaHome.Text = JavaHome;
        settings.JavaxJsonJar.Text = Jar;
        Assert.All(settings.Fields, f => Assert.True(f.IsOk, f.StatusText));
        Assert.True(settings.IsSetupOk);
        Assert.True(settings.IsDirty);

        settings.TimeoutSeconds.Text = "soon";
        Assert.False(settings.IsTimeoutValid);
        Assert.True(settings.TimeoutSeconds.IsProblem);
        Assert.False(settings.SaveCommand.CanExecute(null));
        settings.TimeoutSeconds.Text = "120";
        Assert.True(settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void EnvironmentOverride_IsReadOnly_AndNotWritten()
    {
        File.WriteAllText(ConfigPath, _localYaml);
        var settings = Settings(environment: v => v switch
        {
            "CYTOLEX_CYTOGPS_JAVA_HOME" => JavaHome,
            "CYTOLEX_CYTOGPS_DISK_CACHE" => "true",
            _ => null,
        });

        Assert.True(settings.JavaHome.IsOverridden);
        Assert.True(settings.JavaHome.IsReadOnly);
        Assert.False(settings.JavaHome.CanBrowse);
        Assert.Equal(JavaHome, settings.JavaHome.Text);
        Assert.Contains("CYTOLEX_CYTOGPS_JAVA_HOME", settings.JavaHome.OverrideText, StringComparison.Ordinal);
        Assert.True(settings.JavaHome.IsOk);
        Assert.False(settings.SourceDir.IsOverridden);
        Assert.True(settings.IsDiskCacheOverridden);
        Assert.False(settings.CanEditDiskCache);
        Assert.True(settings.DiskCache);
        Assert.False(settings.IsDirty);

        settings.SetPickedPath(settings.JavaHome, "C:\\elsewhere");
        Assert.Equal(JavaHome, settings.JavaHome.Text);

        settings.SourceDir.Text = SourceDir;
        settings.SaveCommand.Execute(null);

        var text = File.ReadAllText(ConfigPath);
        Assert.Contains("  java_home: \"C:/nojdk\"", text, StringComparison.Ordinal);
        Assert.Contains("  disk_cache: false", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_KeepsOtherSectionsAndComments_ByteIdenticalApartFromTheEditedKeys()
    {
        File.WriteAllText(ConfigPath, _localYaml);
        var applied = 0;
        var settings = Settings(applied: () => applied++);

        settings.SetPickedPath(settings.SourceDir, SourceDir.Replace('/', '\\'));
        settings.TimeoutSeconds.Text = "90";
        settings.SaveCommand.Execute(null);

        Assert.Equal(1, applied);
        Assert.False(settings.IsStatusError);
        Assert.False(settings.IsDirty);
        var before = _localYaml.Split('\n');
        var after = File.ReadAllText(ConfigPath).Split('\n');
        Assert.Equal(before.Length + 2, after.Length); // + timeout_seconds + the final newline
        for (var i = 0; i < before.Length; i++)
        {
            if (before[i].StartsWith("  source_dir:", StringComparison.Ordinal))
            {
                Assert.StartsWith($"  source_dir: \"{SourceDir}\"", after[i], StringComparison.Ordinal);
                Assert.EndsWith("# unpacked source", after[i], StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal(before[i], after[i]);
            }
        }

        Assert.Equal("  timeout_seconds: 90", after[^2]);
        var config = CytoGpsConfig.Load(Repo, _ => null);
        Assert.Equal(SourceDir, config.SourceDir);
        Assert.Equal(TimeSpan.FromSeconds(90), config.Timeout);
    }

    [Fact]
    public void Save_NoFile_CreatesItFromTheTemplate()
    {
        File.WriteAllText(Path.Combine(Repo, "config", "local.example.yaml"), _template);
        var settings = Settings();
        Assert.Equal(string.Empty, settings.SourceDir.Text);

        FillValidSetup(settings);
        settings.DiskCache = true;
        settings.SaveCommand.Execute(null);

        var text = File.ReadAllText(ConfigPath);
        Assert.StartsWith("# Copy to config/local.yaml", text, StringComparison.Ordinal);
        Assert.Contains("\ncytogps:\n", text, StringComparison.Ordinal);
        Assert.Contains("#   timeout_seconds: 600", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        var config = CytoGpsConfig.Load(Repo, _ => null);
        Assert.Equal(Jar, config.JavaxJsonJar);
        Assert.True(config.DiskCache);
        Assert.True(new CytoGpsChecker(config).CheckAvailability().Reason?.Contains("SHA-1", StringComparison.Ordinal));
    }

    [Fact]
    public void Save_Clearing_CommentsTheKeyOut()
    {
        File.WriteAllText(ConfigPath, _localYaml);
        var settings = Settings();

        settings.JavaHome.Text = "  ";
        settings.SaveCommand.Execute(null);

        Assert.Contains("\n#   java_home: \"C:/nojdk\"\n", File.ReadAllText(ConfigPath), StringComparison.Ordinal);
        Assert.Equal(string.Empty, settings.JavaHome.Text);
    }

    [Fact]
    public void Revert_DropsTheEdits()
    {
        File.WriteAllText(ConfigPath, _localYaml);
        var settings = Settings();
        settings.SourceDir.Text = SourceDir;
        Assert.True(settings.RevertCommand.CanExecute(null));

        settings.RevertCommand.Execute(null);

        Assert.Equal("C:/nowhere", settings.SourceDir.Text);
        Assert.False(settings.IsDirty);
    }

    [Fact]
    public void NoRepoRoot_ReadOnly()
    {
        var settings = Settings(environment: v => v == "CYTOLEX_CYTOGPS_SOURCE_DIR" ? SourceDir : null, repo: null);

        Assert.False(settings.IsEditable);
        Assert.Contains("repo checkout", settings.ReadOnlyReason, StringComparison.Ordinal);
        Assert.All(settings.Fields, f => Assert.True(f.IsReadOnly));
        Assert.Equal(SourceDir, settings.SourceDir.Text);
        Assert.True(settings.SourceDir.IsOk);
        Assert.False(settings.SaveCommand.CanExecute(null));
        Assert.False(settings.TestCommand.CanExecute(null));
    }

    [Fact]
    public void MalformedFile_ReadOnlyWithReason()
    {
        File.WriteAllText(ConfigPath, "cytogps:\n  java_home: \"unclosed\n");

        var settings = Settings();

        Assert.False(settings.IsEditable);
        Assert.Contains("cannot be read", settings.ReadOnlyReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_RunsTheBookStringWithTheTypedSetup()
    {
        File.WriteAllText(ConfigPath, _localYaml);
        CytoGpsConfig? used = null;
        var checker = new AnsweringChecker(input => new ExternalBatchResult("CytoGPS", "2016", ExternalBatchStatus.Completed, null, [],
            [new ExternalItemResult(input, input, ExternalVerdict.Valid, "Success", null, [], [])]));
        var settings = Settings(createChecker: c =>
        {
            used = c;
            return checker;
        });
        FillValidSetup(settings);

        await settings.TestCommand.ExecuteAsync(null);

        Assert.Equal(["47,XY,+21"], checker.Inputs);
        Assert.Equal(SourceDir, used?.SourceDir);
        Assert.True(settings.IsTestOk);
        Assert.Contains("CytoGPS works: 47,XY,+21 is valid", settings.TestResult, StringComparison.Ordinal);
        Assert.Equal(_localYaml, File.ReadAllText(ConfigPath)); // a test saves nothing
    }

    [Fact]
    public async Task Test_ShowsTheAdaptersMessage()
    {
        File.WriteAllText(ConfigPath, _localYaml);
        var checker = new AnsweringChecker(input => new ExternalBatchResult("CytoGPS", "2016", ExternalBatchStatus.Failed,
            "Compiling CytoGPS failed (javac exit 1): …", [],
            [new ExternalItemResult(input, input, ExternalVerdict.Failed, "failed", null, [], [])]));
        var settings = Settings(createChecker: _ => checker);

        await settings.TestCommand.ExecuteAsync(null);

        Assert.False(settings.IsTestOk);
        Assert.True(settings.IsTestFailed);
        Assert.StartsWith("Compiling CytoGPS failed", settings.TestResult, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_SaveSwitchesTheCytoGpsTabToReady_WithoutRestart()
    {
        File.WriteAllText(ConfigPath, _localYaml);
        var sha = JarSha1;
        (IExternalChecker?, string?) Create() =>
            (new CytoGpsChecker(CytoGpsConfig.Load(Repo, _ => null), null, ProcessRunner.Instance, sha), null);
        var (checker, error) = Create();
        var vm = new MainWindowViewModel(
            null,
            Rules.RuleStore.DefaultPath,
            checker,
            error,
            Repo,
            applied => new CytoGpsSettingsViewModel(Repo, _ => null, c => CytoGpsSetupCheck.Check(c, sha), null, applied),
            Create)
        { Input = "46,XY" };
        Assert.Equal(CytoGpsState.NotConfigured, vm.CytoGps.State);
        Assert.False(vm.Batch.Comparison.IsConfigured);

        vm.OpenSettings();
        Assert.Equal(MainWindowViewModel.SettingsTab, vm.SelectedMainTab);
        FillValidSetup(vm.CytoGpsSettings);
        vm.CytoGpsSettings.SaveCommand.Execute(null);

        Assert.Equal(CytoGpsState.Idle, vm.CytoGps.State);
        Assert.True(vm.CytoGps.CheckCommand.CanExecute(null));
        Assert.True(vm.Batch.Comparison.IsConfigured);
        Assert.Equal(ComparisonState.Idle, vm.Batch.Comparison.State);

        // Breaking the setup again switches back.
        vm.CytoGpsSettings.JavaHome.Text = "C:/nojdk";
        vm.CytoGpsSettings.SaveCommand.Execute(null);
        Assert.Equal(CytoGpsState.NotConfigured, vm.CytoGps.State);
        Assert.Contains("is not a JDK", vm.CytoGps.StateText, StringComparison.Ordinal);
        Assert.False(vm.Batch.Comparison.IsConfigured);
    }

    private sealed class AnsweringChecker(Func<string, ExternalBatchResult> answer) : IExternalChecker
    {
        public string Name => "CytoGPS";

        public string Edition => "2016";

        public List<string> Inputs { get; } = [];

        public ExternalAvailability CheckAvailability() => ExternalAvailability.Configured;

        public Task<ExternalBatchResult> CheckAsync(IReadOnlyList<string> inputs, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            Inputs.AddRange(inputs);
            return Task.FromResult(answer(inputs[0]));
        }
    }
}
