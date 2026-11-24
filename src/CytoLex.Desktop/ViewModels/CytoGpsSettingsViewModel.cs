using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CytoLex.External;
using CytoLex.External.CytoGps;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The CytoGPS part of the Settings tab (T-089): edits the <c>cytogps</c> section of <c>config/local.yaml</c>
/// (<see cref="CytoGpsConfigFile"/>), checks each path with the adapter's own checks (<see cref="CytoGpsSetupCheck"/>: files
/// only, nothing is started while typing), runs one book string through CytoGPS on "Test" with the values as typed, and
/// after a save hands the new setup to the CytoGPS tab and the batch comparison (<c>applied</c>). Keys overridden by an
/// environment variable are read-only. Repo checkouts only (decision 2026-11-24): without a repo root the view is read-only.
/// CytoLex validation never depends on this view.
/// </summary>
public partial class CytoGpsSettingsViewModel : ObservableObject
{
    /// <summary>The string "Test" sends: an ISCN 2016 book example (ch. 8, trisomy 21).</summary>
    public const string TestString = "47,XY,+21";

    /// <summary>The data-policy note of the disk cache (decision T-048).</summary>
    public const string DiskCacheNote =
        "Results are also kept in build/cache/cytogps/results/. Use it with synthetic or ISCN-book strings only, never real patient karyotypes (T-048).";

    private readonly Func<string, string?> _environment;
    private readonly Func<CytoGpsConfig, CytoGpsSetupReport> _check;
    private readonly Func<CytoGpsConfig, IExternalChecker> _createChecker;
    private readonly Action _applied;
    private bool _loading;
    private bool? _fileDiskCache;

    /// <summary>Creates the view and reads the file.</summary>
    /// <param name="repoRoot">The CytoLex repo root (<c>null</c>: not a repo checkout, read-only).</param>
    /// <param name="environment">Reads an environment variable (default: the process environment).</param>
    /// <param name="check">The per-field check (default: <see cref="CytoGpsSetupCheck.Check(CytoGpsConfig)"/>).</param>
    /// <param name="createChecker">Creates the checker "Test" runs (default: a <see cref="CytoGpsChecker"/>).</param>
    /// <param name="applied">Called after a save (the app swaps in the new checker).</param>
    public CytoGpsSettingsViewModel(
        string? repoRoot,
        Func<string, string?>? environment = null,
        Func<CytoGpsConfig, CytoGpsSetupReport>? check = null,
        Func<CytoGpsConfig, IExternalChecker>? createChecker = null,
        Action? applied = null)
    {
        RepoRoot = repoRoot;
        _environment = environment ?? Environment.GetEnvironmentVariable;
        _check = check ?? CytoGpsSetupCheck.Check;
        _createChecker = createChecker ?? (c => new CytoGpsChecker(c));
        _applied = applied ?? (() => { });
        SourceDir = new(CytoGpsConfigFile.SourceDirKey, "CytoGPS source", SettingPicker.Folder,
            "The unpacked github.com/i2-wustl/CytoGPS (src/, target/, resource/).", Changed);
        JavaHome = new(CytoGpsConfigFile.JavaHomeKey, "JDK", SettingPicker.Folder,
            "A JDK folder with bin/java and bin/javac.", Changed);
        JavaxJsonJar = new(CytoGpsConfigFile.JavaxJsonJarKey, "javax.json jar", SettingPicker.JarFile,
            "org.glassfish:javax.json:1.1.4 from Maven Central (SHA-1 checked).", Changed);
        TimeoutSeconds = new(CytoGpsConfigFile.TimeoutSecondsKey, "Time limit (s)", SettingPicker.None,
            $"Limit for one CytoGPS run; empty = {CytoGpsConfig.DefaultTimeout.TotalSeconds:0} s.", Changed);
        Fields = [SourceDir, JavaHome, JavaxJsonJar];
        Load();
    }

    /// <summary>The repo root whose <c>config/local.yaml</c> is edited, or <c>null</c>.</summary>
    public string? RepoRoot { get; }

    /// <summary>The file edited, for display.</summary>
    public string FileText => RepoRoot is null ? "config/local.yaml (no repo checkout)" : CytoGpsConfigFile.PathOf(RepoRoot);

    /// <summary><c>source_dir</c>.</summary>
    public SettingFieldViewModel SourceDir { get; }

    /// <summary><c>java_home</c>.</summary>
    public SettingFieldViewModel JavaHome { get; }

    /// <summary><c>javax_json_jar</c>.</summary>
    public SettingFieldViewModel JavaxJsonJar { get; }

    /// <summary><c>timeout_seconds</c>.</summary>
    public SettingFieldViewModel TimeoutSeconds { get; }

    /// <summary>The path fields, in order.</summary>
    public IReadOnlyList<SettingFieldViewModel> Fields { get; }

    /// <summary><c>disk_cache</c>.</summary>
    [ObservableProperty]
    public partial bool DiskCache { get; set; }

    partial void OnDiskCacheChanged(bool value) => Changed();

    /// <summary>The environment variable overriding <c>disk_cache</c> and its effect, or <c>null</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiskCacheOverridden), nameof(CanEditDiskCache))]
    public partial string? DiskCacheOverrideText { get; private set; }

    /// <summary>Whether <c>disk_cache</c> is set by the environment.</summary>
    public bool IsDiskCacheOverridden => DiskCacheOverrideText is not null;

    /// <summary>Whether the disk-cache checkbox can be changed.</summary>
    public bool CanEditDiskCache => IsEditable && !IsDiskCacheOverridden;

    /// <summary>Whether the file can be edited (repo checkout, file readable).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditDiskCache), nameof(IsNotEditable))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RevertCommand), nameof(TestCommand))]
    public partial bool IsEditable { get; private set; }

    /// <summary>Whether the view is read-only.</summary>
    public bool IsNotEditable => !IsEditable;

    /// <summary>Why the view is read-only, or <c>null</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadOnlyReason))]
    public partial string? ReadOnlyReason { get; private set; }

    /// <summary>Whether <see cref="ReadOnlyReason"/> is set.</summary>
    public bool HasReadOnlyReason => ReadOnlyReason is not null;

    /// <summary>Whether the form differs from the file.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RevertCommand))]
    public partial bool IsDirty { get; private set; }

    /// <summary>Whether the setup as typed passes every path check.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SetupText))]
    public partial bool IsSetupOk { get; private set; }

    /// <summary>The overall check line.</summary>
    public string SetupText => IsSetupOk
        ? "Every path checks out. \"Test\" runs CytoGPS once (the first run compiles it)."
        : "CytoGPS is not set up yet: see the fields marked ✗.";

    /// <summary>The last save or load message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusText { get; private set; }

    /// <summary>Whether <see cref="StatusText"/> is set.</summary>
    public bool HasStatus => StatusText is not null;

    /// <summary>Whether the last save failed.</summary>
    [ObservableProperty]
    public partial bool IsStatusError { get; private set; }

    /// <summary>The test run's progress (e.g. compiling).</summary>
    [ObservableProperty]
    public partial string? TestProgress { get; private set; }

    /// <summary>The test result: verdict and status, or the adapter's message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTestResult))]
    public partial string? TestResult { get; private set; }

    /// <summary>Whether <see cref="TestResult"/> is set.</summary>
    public bool HasTestResult => TestResult is not null;

    /// <summary>Whether CytoGPS gave a verdict in the test (<c>null</c>: no test or not finished).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTestFailed))]
    public partial bool? IsTestOk { get; private set; }

    /// <summary>Whether the test ran and gave no verdict.</summary>
    public bool IsTestFailed => IsTestOk == false;

    /// <summary>Whether the time limit is empty or a positive whole number.</summary>
    public bool IsTimeoutValid => ParseTimeout(TimeoutSeconds.EffectiveValue) is not null;

    /// <summary>The setup as typed (with the environment overrides); <c>null</c> without a repo root.</summary>
    public CytoGpsConfig? FormConfig => RepoRoot is null
        ? null
        : new CytoGpsConfig(
            RepoRoot,
            SourceDir.EffectiveValue,
            JavaHome.EffectiveValue,
            JavaxJsonJar.EffectiveValue,
            EffectiveDiskCache,
            ParseTimeout(TimeoutSeconds.EffectiveValue) ?? CytoGpsConfig.DefaultTimeout);

    private bool EffectiveDiskCache => _environment(CytoGpsConfigFile.EnvironmentVariable(CytoGpsConfigFile.DiskCacheKey)) is { } env
        && !string.IsNullOrWhiteSpace(env)
            ? bool.TryParse(env.Trim(), out var b) && b
            : DiskCache;

    /// <summary>Puts a picked path into <paramref name="field"/> (forward slashes, as in the template).</summary>
    public void SetPickedPath(SettingFieldViewModel field, string path)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(path);
        if (IsEditable && !field.IsReadOnly)
        {
            field.Text = path.Replace('\\', '/');
        }
    }

    /// <summary>Writes the changed keys into <c>config/local.yaml</c> and applies the new setup.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (RepoRoot is null)
        {
            return;
        }

        var changes = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in Fields.Append(TimeoutSeconds).Where(f => f.IsDirty))
        {
            changes[field.Key] = field.SavedValue;
        }

        if (!IsDiskCacheOverridden && DiskCache != (_fileDiskCache ?? false))
        {
            changes[CytoGpsConfigFile.DiskCacheKey] = DiskCache ? "true" : "false";
        }

        try
        {
            var path = CytoGpsConfigFile.Save(RepoRoot, changes);
            Load();
            (StatusText, IsStatusError) = ($"Saved to {path}. The CytoGPS tab and the batch comparison use the new setup.", false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            (StatusText, IsStatusError) = ($"Not saved: {ex.Message}", true);
            return;
        }

        _applied();
    }

    private bool CanSave() => IsEditable && IsDirty && IsTimeoutValid;

    /// <summary>Drops the edits: reads the file again.</summary>
    [RelayCommand(CanExecute = nameof(CanRevert))]
    private void Revert()
    {
        Load();
        (StatusText, IsStatusError) = (null, false);
    }

    private bool CanRevert() => IsEditable && IsDirty;

    /// <summary>Runs <see cref="TestString"/> through CytoGPS with the setup as typed (not saved); cancel with <c>TestCancelCommand</c>.</summary>
    [RelayCommand(CanExecute = nameof(CanTest), IncludeCancelCommand = true)]
    private async Task TestAsync(CancellationToken cancellationToken)
    {
        if (FormConfig is not { } config)
        {
            return;
        }

        (TestResult, IsTestOk, TestProgress) = (null, null, "Starting CytoGPS…");
        var progress = new Progress<string>(m =>
        {
            if (TestCommand.IsRunning)
            {
                TestProgress = m;
            }
        });
        try
        {
            // Off the UI thread: the adapter hashes and may compile the sources before its first await.
            var checker = _createChecker(config);
            var result = await Task.Run(() => checker.CheckAsync([TestString], progress, cancellationToken), cancellationToken)
                .ConfigureAwait(true);
            ShowTest(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            (TestResult, IsTestOk) = ("Test cancelled.", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            (TestResult, IsTestOk) = ($"Test failed: {ex.Message}", false);
        }
        finally
        {
            TestProgress = null;
        }
    }

    private bool CanTest() => RepoRoot is not null && IsTimeoutValid;

    private void ShowTest(ExternalBatchResult result)
    {
        var item = result.Items.IsDefaultOrEmpty ? null : result.Items[0];
        if (result.Status == ExternalBatchStatus.Completed && item?.Verdict is ExternalVerdict.Valid or ExternalVerdict.Invalid)
        {
            var verdict = item.Verdict == ExternalVerdict.Valid ? "valid" : "not valid";
            (TestResult, IsTestOk) = ($"CytoGPS works: {TestString} is {verdict} (CytoGPS status: {item.ToolStatus ?? "none"}).", true);
            return;
        }

        (TestResult, IsTestOk) = (result.Message ?? item?.ToolStatus ?? "CytoGPS returned no result.", false);
    }

    private void Load()
    {
        _loading = true;
        try
        {
            var values = System.Collections.Immutable.ImmutableDictionary<string, string>.Empty;
            string? reason = null;
            if (RepoRoot is null)
            {
                reason = "Settings can be edited only when CytoLex runs from a repo checkout (config/local.yaml). The values in effect come from the environment.";
            }
            else
            {
                try
                {
                    values = CytoGpsConfigFile.ReadValues(RepoRoot);
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    reason = $"config/local.yaml cannot be read: {ex.Message} Fix it by hand, then Revert.";
                }
            }

            (IsEditable, ReadOnlyReason) = (reason is null, reason);
            foreach (var field in Fields.Append(TimeoutSeconds))
            {
                field.IsEditable = IsEditable;
                field.Load(values.GetValueOrDefault(field.Key), _environment(field.EnvironmentVariable));
            }

            _fileDiskCache = values.GetValueOrDefault(CytoGpsConfigFile.DiskCacheKey) is { } dc && bool.TryParse(dc.Trim(), out var b) ? b : null;
            var envVariable = CytoGpsConfigFile.EnvironmentVariable(CytoGpsConfigFile.DiskCacheKey);
            DiskCacheOverrideText = _environment(envVariable) is { } env && !string.IsNullOrWhiteSpace(env)
                ? $"Set by the environment variable {envVariable} ({env.Trim()})."
                : null;
            DiskCache = IsDiskCacheOverridden ? EffectiveDiskCache : _fileDiskCache ?? false;
        }
        finally
        {
            _loading = false;
        }

        Changed();
    }

    /// <summary>A field changed: re-check (files only) and update the dirty flag.</summary>
    private void Changed()
    {
        if (_loading)
        {
            return;
        }

        IsDirty = Fields.Append(TimeoutSeconds).Any(f => f.IsDirty)
            || (!IsDiskCacheOverridden && DiskCache != (_fileDiskCache ?? false));
        Recheck();
        OnPropertyChanged(nameof(IsTimeoutValid));
        SaveCommand.NotifyCanExecuteChanged();
        TestCommand.NotifyCanExecuteChanged();
    }

    private void Recheck()
    {
        var config = FormConfig ?? new CytoGpsConfig(
            AppContext.BaseDirectory, SourceDir.EffectiveValue, JavaHome.EffectiveValue, JavaxJsonJar.EffectiveValue, false, CytoGpsConfig.DefaultTimeout);
        CytoGpsSetupReport report;
        try
        {
            report = _check(config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            foreach (var field in Fields)
            {
                field.SetStatus(false, ex.Message);
            }

            IsSetupOk = false;
            return;
        }

        foreach (var field in Fields)
        {
            var check = report.Fields.FirstOrDefault(f => f.Key == field.Key);
            field.SetStatus(check?.IsOk, check?.Message);
        }

        IsSetupOk = report.IsConfigured;
        TimeoutSeconds.SetStatus(
            IsTimeoutValid ? null : false,
            IsTimeoutValid ? null : "Must be a whole number of seconds greater than 0 (empty = default).");
    }

    /// <summary>The time limit in seconds: the default when empty, <c>null</c> when not a positive whole number.</summary>
    private static TimeSpan? ParseTimeout(string? text) =>
        text is null ? CytoGpsConfig.DefaultTimeout
        : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var secs) && secs > 0 ? TimeSpan.FromSeconds(secs)
        : null;
}
