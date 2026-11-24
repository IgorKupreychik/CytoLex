using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.External.CytoGps;

namespace CytoLex.Desktop.ViewModels;

/// <summary>What a settings field picks with "Browse…".</summary>
public enum SettingPicker
{
    /// <summary>No picker (typed only).</summary>
    None,

    /// <summary>A folder.</summary>
    Folder,

    /// <summary>A <c>.jar</c> file.</summary>
    JarFile,
}

/// <summary>
/// One text setting of the settings view (T-089): the value in <c>config/local.yaml</c>, the text being edited, the
/// environment override (shown read-only: editing the file would have no effect) and its check result.
/// </summary>
public partial class SettingFieldViewModel : ObservableObject
{
    private readonly Action _changed;

    /// <summary>Creates the field.</summary>
    /// <param name="key">The <c>config/local.yaml</c> key.</param>
    /// <param name="label">The label shown.</param>
    /// <param name="picker">What "Browse…" picks.</param>
    /// <param name="hint">A short description of the expected value.</param>
    /// <param name="changed">Called after the text changed.</param>
    public SettingFieldViewModel(string key, string label, SettingPicker picker, string hint, Action changed)
    {
        Key = key;
        Label = label;
        Picker = picker;
        Hint = hint;
        EnvironmentVariable = CytoGpsConfigFile.EnvironmentVariable(key);
        _changed = changed;
    }

    /// <summary>The <c>config/local.yaml</c> key, e.g. <c>java_home</c>.</summary>
    public string Key { get; }

    /// <summary>The label shown.</summary>
    public string Label { get; }

    /// <summary>What "Browse…" picks.</summary>
    public SettingPicker Picker { get; }

    /// <summary>Whether the field has a "Browse…" button.</summary>
    public bool HasPicker => Picker != SettingPicker.None;

    /// <summary>A short description of the expected value.</summary>
    public string Hint { get; }

    /// <summary>The environment variable that overrides the key.</summary>
    public string EnvironmentVariable { get; }

    /// <summary>The value in the file (<c>null</c>: not set).</summary>
    public string? FileValue { get; private set; }

    /// <summary>The environment variable's value when it overrides the key, else <c>null</c>.</summary>
    public string? OverrideValue { get; private set; }

    /// <summary>Whether an environment variable overrides the key (the field is read-only).</summary>
    public bool IsOverridden => OverrideValue is not null;

    /// <summary>The override note, e.g. <c>Set by the environment variable CYTOLEX_CYTOGPS_JAVA_HOME</c>.</summary>
    public string? OverrideText => IsOverridden
        ? $"Set by the environment variable {EnvironmentVariable}; the file value ({FileValue ?? "not set"}) has no effect while it is set."
        : null;

    /// <summary>Whether the view lets the user edit the file at all.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadOnly), nameof(CanBrowse))]
    public partial bool IsEditable { get; set; }

    /// <summary>Whether the text box is read-only (overridden, or the view can't save).</summary>
    public bool IsReadOnly => IsOverridden || !IsEditable;

    /// <summary>Whether "Browse…" is enabled.</summary>
    public bool CanBrowse => HasPicker && !IsReadOnly;

    /// <summary>The text shown: the edited value, or the environment value when overridden.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    partial void OnTextChanged(string value) => _changed();

    /// <summary>Whether the check passed (<c>null</c>: not checked).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProblem), nameof(HasStatus))]
    public partial bool? IsOk { get; set; }

    /// <summary>Whether the check found a problem.</summary>
    public bool IsProblem => IsOk == false;

    /// <summary>The check's message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusText { get; set; }

    /// <summary>Whether there is a check message.</summary>
    public bool HasStatus => StatusText is not null;

    /// <summary>The value that is in effect: the environment's, else the edited text (<c>null</c> when empty).</summary>
    public string? EffectiveValue => OverrideValue ?? Normalize(Text);

    /// <summary>The value the file should hold: the file value when overridden (not editable), else the edited text.</summary>
    public string? SavedValue => IsOverridden ? FileValue : Normalize(Text);

    /// <summary>Whether the edited value differs from the file.</summary>
    public bool IsDirty => !string.Equals(SavedValue, Normalize(FileValue), StringComparison.Ordinal);

    /// <summary>Sets the file and environment values; the text shows the value in effect.</summary>
    public void Load(string? fileValue, string? overrideValue)
    {
        FileValue = Normalize(fileValue);
        OverrideValue = Normalize(overrideValue);
        OnPropertyChanged(nameof(FileValue));
        OnPropertyChanged(nameof(OverrideValue));
        OnPropertyChanged(nameof(IsOverridden));
        OnPropertyChanged(nameof(OverrideText));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CanBrowse));
        Text = OverrideValue ?? FileValue ?? string.Empty;
    }

    /// <summary>Shows a check result.</summary>
    public void SetStatus(bool? ok, string? message)
    {
        IsOk = ok;
        StatusText = message;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
