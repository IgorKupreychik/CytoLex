using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// One card of the Associations view (T-110): condition, strength, context, source citation, review state and what in the
/// string matched. The rule's defining criteria are not shown (kept for T-112; no diagnosis wording, T-104).
/// </summary>
public sealed class AssociationItemViewModel
{
    /// <summary>The card of <paramref name="match"/>; evidence spans mapped to the editor by <paramref name="toEditor"/>.</summary>
    public AssociationItemViewModel(AssociationMatch match, Func<TextSpan, TextSpan>? toEditor = null)
    {
        ArgumentNullException.ThrowIfNull(match);
        Match = match;
        Evidence = [.. match.Evidence.Select(e => new AssociationEvidenceViewModel(e, toEditor))];
    }

    /// <summary>The match.</summary>
    public AssociationMatch Match { get; }

    /// <summary>The rule ID (<c>ASSOC.T9_22_CML</c>).</summary>
    public string RuleId => Match.RuleId;

    /// <summary>The condition's name as its source gives it.</summary>
    public string Condition => Match.Condition.Name;

    /// <summary>The condition's codes, e.g. <c>Orphanet ORPHA:870 · ICD-10 Q90</c>; empty when none is filled in.</summary>
    public string ConditionIds => string.Join(" · ", Match.Condition.Ids.Select(i => $"{SystemLabel(i.System)} {i.Code}"));

    /// <summary>Whether <see cref="ConditionIds"/> is shown.</summary>
    public bool HasConditionIds => !Match.Condition.Ids.IsDefaultOrEmpty;

    /// <summary>The strength: <c>characteristic</c>, <c>recurrent</c> or <c>reported</c>.</summary>
    public string StrengthLabel => Match.Strength switch
    {
        AssociationStrength.Characteristic => "characteristic",
        AssociationStrength.Recurrent => "recurrent",
        _ => "reported",
    };

    /// <summary>What the strength means.</summary>
    public string StrengthTip => Match.Strength switch
    {
        AssociationStrength.Characteristic => "Characteristic: the hallmark abnormality of the condition in the source",
        AssociationStrength.Recurrent => "Recurrent: found again and again in the condition, but not its hallmark",
        _ => "Reported: described in the condition, less common",
    };

    /// <summary>Where the rule holds: <c>constitutional</c>, <c>neoplastic</c> or <c>constitutional or neoplastic</c>.</summary>
    public string ContextLabel => Match.Context switch
    {
        AssociationContext.Constitutional => "constitutional",
        AssociationContext.Neoplastic => "neoplastic",
        _ => "constitutional or neoplastic",
    };

    /// <summary>
    /// How the rule's context applies to this string: <c>null</c> when the string's context is known; otherwise the
    /// readings it matches in, e.g. <c>Context of the string unknown: matches if the sample is acquired.</c>
    /// </summary>
    public string? ContextNote => Match.IsContextKnown || Match.Contexts.IsDefaultOrEmpty
        ? null
        : "Context of the string unknown: matches if the sample is "
            + string.Join(" or ", Match.Contexts.Select(DiagnosticItemViewModel.Label)) + ".";

    /// <summary>Whether <see cref="ContextNote"/> is shown.</summary>
    public bool HasContextNote => ContextNote is not null;

    /// <summary>The source and its entry: <c>WHO-HAEM5: …</c>.</summary>
    public string SourceText => $"{Match.Source}: {Match.Citation}";

    /// <summary>No person has checked the rule against its source yet (badge, as the probe table's rows in T-103).</summary>
    public bool IsUnreviewed => Match.IsUnreviewed;

    /// <summary>The badge's tooltip, naming the rule's source.</summary>
    public string UnreviewedTip => $"Not yet checked against its source ({Match.Source}) by a person: {RuleId}";

    /// <summary>What in the string matched, one row per satisfied leaf.</summary>
    public ImmutableArray<AssociationEvidenceViewModel> Evidence { get; }

    private static string SystemLabel(string system) => system switch
    {
        "orphanet" => "Orphanet",
        "icd10" => "ICD-10",
        "icdo3" => "ICD-O-3",
        "mondo" => "MONDO",
        _ => system,
    };
}
