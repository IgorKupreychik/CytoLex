using CommunityToolkit.Mvvm.Input;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// One note of the "Corrected" line (T-097): an applied autocorrection with its rule (the link opens the rule browser),
/// the change and the citation.
/// </summary>
public sealed partial class CorrectionItemViewModel
{
    private readonly Action<string>? _openRule;

    public CorrectionItemViewModel(AppliedCorrection correction, Action<string>? openRule)
    {
        ArgumentNullException.ThrowIfNull(correction);
        Correction = correction;
        _openRule = openRule;
        RuleId = correction.RuleId;
        Change = HighlightBuilder.ChangeOf(correction);
        Description = correction.Description;
        Citation = correction.Citations.IsDefaultOrEmpty ? null : string.Join("; ", correction.Citations.Select(c => c.Rendered));
    }

    /// <summary>The correction shown.</summary>
    public AppliedCorrection Correction { get; }

    /// <summary>The KB rule whose fix it is.</summary>
    public string RuleId { get; }

    /// <summary>The change: <c>"xy" → "XY"</c>, <c>removed " "</c>.</summary>
    public string Change { get; }

    /// <summary>The fix's wording (KB hint).</summary>
    public string Description { get; }

    /// <summary>The rendered citations; <c>null</c> without any.</summary>
    public string? Citation { get; }

    /// <summary>Whether <see cref="Citation"/> is set.</summary>
    public bool HasCitation => Citation is not null;

    /// <summary>Opens <see cref="RuleId"/> in the rule browser.</summary>
    [RelayCommand]
    private void OpenRule() => _openRule?.Invoke(RuleId);
}
