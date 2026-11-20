using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using CytoLex.Core.Context;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// "Fix all" of one edition (T-040): one round of <see cref="Fixer.ApplyAll"/>, shown as a diff before it is applied.
/// Fixes that overlap an applied one, and findings the round brings to light, come back after the re-validation.
/// </summary>
public sealed partial class FixAllViewModel
{
    private readonly ApplyFixHandler _apply;

    private FixAllViewModel(string input, FixAllResult round, SampleContext? context, ApplyFixHandler apply)
    {
        _apply = apply;
        Text = round.Text;
        Count = round.Applied.Length;
        SkippedCount = round.Skipped.Length;
        Header = $"Fix all ({Count})";
        DiffParts = [.. FixDiff.Build(input, round.Applied.SelectMany(f => f.Edits)).Select(p => new DiffPartViewModel(p))];
        var notes = new List<string> { $"Applies {Count} fixes in one step, then re-validates." };
        if (SkippedCount > 0)
        {
            notes.Add($"{SkippedCount} overlapping fix(es) are left for the next round.");
        }

        notes.Add(context is null
            ? "Context-dependent fixes are left out while the context is unknown."
            : $"Context-dependent fixes are applied for the {DiagnosticItemViewModel.Label(context.Value)} context.");
        Tip = string.Join(" ", notes);
    }

    /// <summary>The fix-all round of <paramref name="result"/>, or <c>null</c> when it would apply fewer than two fixes (one fix has its own Apply).</summary>
    public static FixAllViewModel? From(string input, EditionResult result, SampleContext? context, ApplyFixHandler? apply)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(result);
        if (apply is null || result.Status == EditionStatus.NotImplemented)
        {
            return null;
        }

        var round = Fixer.ApplyAll(input, result, context);
        return round.Applied.Length < 2 ? null : new FixAllViewModel(input, round, context, apply);
    }

    /// <summary>The input after the round.</summary>
    public string Text { get; }

    /// <summary>The number of fixes the round applies.</summary>
    public int Count { get; }

    /// <summary>The number of fixes left for the next round because they overlap an applied one.</summary>
    public int SkippedCount { get; }

    /// <summary>The button text, e.g. <c>Fix all (3)</c>.</summary>
    public string Header { get; }

    /// <summary>What the round does (tooltip).</summary>
    public string Tip { get; }

    /// <summary>The before/after diff of the round.</summary>
    public ImmutableArray<DiffPartViewModel> DiffParts { get; }

    /// <summary>Sets the input to <see cref="Text"/>, which re-validates.</summary>
    [RelayCommand]
    private void Apply() => _apply(Text, $"{Count} fixes (fix all)");
}
