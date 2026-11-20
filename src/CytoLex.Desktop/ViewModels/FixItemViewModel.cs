using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// Applies a fix: the new input and a description for the status line (T-040). The main window sets the input, which
/// re-validates.
/// </summary>
/// <param name="newText">The whole corrected input.</param>
/// <param name="description">What was applied, e.g. <c>Insert a comma</c>.</param>
public delegate void ApplyFixHandler(string newText, string description);

/// <summary>
/// One fix of a diagnostic (T-040): a level-1 fix as a before/after diff with an Apply button, or an explanation as text.
/// </summary>
public sealed partial class FixItemViewModel
{
    private readonly ApplyFixHandler? _apply;

    public FixItemViewModel(string input, Fix fix, ApplyFixHandler? apply = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(fix);

        Fix = fix;
        _apply = apply;
        var context = fix.ForContext is { } c ? $" (if {DiagnosticItemViewModel.Label(c)})" : string.Empty;
        Label = fix.Level switch
        {
            FixLevel.Deterministic when fix.IsEdit => $"Fix{context}: {fix.Description}",
            FixLevel.Explanation => $"Explanation{context}: {fix.Description}",
            _ => $"Suggestion{context}: {fix.Description}",
        };

        IsEdit = fix.IsEdit && fix.Preview is not null;
        Preview = IsEdit ? fix.Preview : null;
        DiffParts = IsEdit ? [.. FixDiff.Build(input, fix).Select(p => new DiffPartViewModel(p))] : [];
    }

    /// <summary>The fix shown.</summary>
    public Fix Fix { get; }

    /// <summary><c>Fix (if acquired): …</c>, <c>Explanation: …</c> or <c>Suggestion: …</c>.</summary>
    public string Label { get; }

    /// <summary>Whether the fix edits the text (a level-1 fix with a preview): shows a diff and an Apply button.</summary>
    public bool IsEdit { get; }

    /// <summary>The corrected input; <c>null</c> for an explanation.</summary>
    public string? Preview { get; }

    /// <summary>The before/after diff (empty for an explanation).</summary>
    public ImmutableArray<DiffPartViewModel> DiffParts { get; }

    /// <summary>Whether Apply is offered: an edit, and a window to apply it in.</summary>
    public bool CanApply => IsEdit && _apply is not null;

    /// <summary>Sets the input to <see cref="Preview"/>, which re-validates.</summary>
    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        if (CanApply)
        {
            _apply!(Preview!, Fix.Description);
        }
    }
}
