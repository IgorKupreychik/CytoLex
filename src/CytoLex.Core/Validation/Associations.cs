using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>Which kind of <c>match</c> leaf a piece of evidence satisfies (T-109).</summary>
public enum AssociationLeafKind
{
    /// <summary>An abnormality as written (<see cref="ExactLeaf"/>).</summary>
    Exact,

    /// <summary>Two bands joined by a rearrangement or an in situ <c>con</c> signal (<see cref="FusionLeaf"/>).</summary>
    Fusion,

    /// <summary>A gain or loss overlapping a region (<see cref="CopyLeaf"/>).</summary>
    Copy,
}

/// <summary>What in the string satisfies one leaf of an association rule (T-109).</summary>
/// <param name="Leaf">The leaf kind.</param>
/// <param name="LeafText">The leaf as text (<c>fusion 9q34+22q11.2</c>).</param>
/// <param name="Abnormality">The entry (an index into <see cref="EditionResult.Abnormalities"/>).</param>
/// <param name="Locus">The locus (an index into <see cref="EditionResult.Loci"/>) for a copy or in situ fusion match; <c>null</c> otherwise.</param>
/// <param name="Span">What matched, in the text the result's spans refer to: the abnormality, the <c>con</c> group, or the locus.</param>
/// <param name="Text">That text as written.</param>
/// <param name="Overlap">For a copy match: the share of the rule's region the locus covers (0–1].</param>
/// <param name="Reading">
/// For a copy match read from a copy number or signal count rather than a gain/loss symbol: how it was read
/// (<c>×1 read as loss</c>; autosomes against 2, decided at T-109). <c>null</c> otherwise.
/// </param>
public sealed record AssociationEvidence(
    AssociationLeafKind Leaf,
    string LeafText,
    int Abnormality,
    int? Locus,
    TextSpan Span,
    string Text,
    double? Overlap = null,
    string? Reading = null);

/// <summary>
/// An association rule whose pattern the string matches (T-109): a condition associated in the literature with
/// what the string describes. Not a diagnosis or an interpretation of the result (T-104); never in the diagnostics or
/// the translation.
/// </summary>
/// <param name="RuleId">The rule ID (<c>ASSOC.T9_22_CML</c>).</param>
/// <param name="Condition">The condition.</param>
/// <param name="Context">Where the association holds, per the rule.</param>
/// <param name="Strength">How closely the condition is tied to the abnormality.</param>
/// <param name="Source">The source (<c>WHO-HAEM5</c>, <c>ClinGen</c>, …).</param>
/// <param name="Citation">The source entry or release.</param>
/// <param name="Defining">Classifications where the abnormality is a defining criterion (for T-112); empty when none.</param>
/// <param name="IsUnreviewed">No person has checked the rule against its source yet.</param>
/// <param name="Contexts">
/// The sample contexts in which the rule matches: the string's effective context, or each context it holds in when the
/// context is unknown (like the translation's readings).
/// </param>
/// <param name="IsContextKnown">Whether the string's context is known (hint or markers); otherwise <paramref name="Contexts"/> are readings.</param>
/// <param name="Evidence">What matched, one item per satisfied leaf and abnormality or locus, in input order.</param>
public sealed record AssociationMatch(
    string RuleId,
    AssociationCondition Condition,
    AssociationContext Context,
    AssociationStrength Strength,
    string Source,
    string Citation,
    ImmutableArray<DefiningCriterion> Defining,
    bool IsUnreviewed,
    ImmutableArray<SampleContext> Contexts,
    bool IsContextKnown,
    ImmutableArray<AssociationEvidence> Evidence);
