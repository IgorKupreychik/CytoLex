using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Kb;

/// <summary>A book citation of a rule, as stored in the runtime bundle (kb/README.md).</summary>
/// <param name="Section">The ISCN section, e.g. <c>9.2.17.1</c>.</param>
/// <param name="Page">The book page.</param>
/// <param name="Anchor">The paragraph (<c>¶1</c>), example (<c>E3</c>) or float label (<c>Table 2</c>); <c>null</c> if none.</param>
/// <param name="Rendered">The citation as shown to users: <c>ISCN 2016 §9.2.17.1, p.75, ¶1</c>.</param>
public sealed record Citation(string Section, int Page, string? Anchor, string Rendered);

/// <summary>The KB fix hint of a rule (level, kind, wording); <c>DiagnosticFactory</c> builds the fixes from it (T-035).</summary>
/// <param name="Level">1 = deterministic edit, 2 = "did you mean", 3 = explanation only.</param>
/// <param name="Kind">The KB <c>fix.kind</c> (<c>replace</c>, <c>insert</c>, <c>delete</c>, <c>reorder</c>), if any.</param>
/// <param name="Hint">The KB hint text, if any.</param>
/// <param name="Auto">KB <c>fix.auto</c>: autocorrection may apply the rule's level-1 fixes automatically (T-096).</param>
public sealed record RuleFixHint(int Level, string? Kind, string? Hint, bool Auto = false);

/// <summary>
/// The runtime metadata of one KB rule in one edition (docs/design/core.md §6): everything the engine needs to turn a
/// finding into a diagnostic (severity, review state, context, citations). Quotes and examples are not in the bundle.
/// </summary>
/// <param name="Id">The stable rule ID, e.g. <c>KARYO.COUNT_SEX_ORDER</c>.</param>
/// <param name="Edition">The ISCN edition, e.g. <c>2016</c>.</param>
/// <param name="Section">The ISCN section that states the rule.</param>
/// <param name="Title">The short title; used as the diagnostic message.</param>
/// <param name="Severity">The KB severity (before the T-022 caps).</param>
/// <param name="Statement">The paraphrased rule statement.</param>
/// <param name="AppliesTo">The KB <c>applies_to</c> names (<c>general</c>, <c>karyotype</c>, <c>arr</c>, …).</param>
/// <param name="Contexts">The sample contexts the rule is limited to; empty = every context. KB <c>neoplastic</c> is <see cref="SampleContext.Acquired"/>.</param>
/// <param name="Fix">The KB fix hint, if any.</param>
/// <param name="NeedsReview">KB <c>review.needs_review</c>: the book is ambiguous; findings are capped at warning (T-022).</param>
/// <param name="ReviewNote">KB <c>review.note</c>, if any.</param>
/// <param name="Citations">The rendered citations.</param>
public sealed record KbRule(
    string Id,
    string Edition,
    string Section,
    string Title,
    Severity Severity,
    string Statement,
    ImmutableArray<string> AppliesTo,
    ImmutableArray<SampleContext> Contexts,
    RuleFixHint? Fix,
    bool NeedsReview,
    string? ReviewNote,
    ImmutableArray<Citation> Citations)
{
    /// <summary>The <c>applies_to</c> name that makes a rule run on every implemented notation type.</summary>
    public const string General = "general";

    /// <summary>Whether the rule runs on segments of <paramref name="type"/> (<c>general</c> = every type).</summary>
    public bool AppliesToType(NotationType type) =>
        AppliesTo.Contains(General) || AppliesTo.Contains(type.ToKbName());

    /// <summary>Whether the rule is limited to some sample contexts (KB <c>context</c>).</summary>
    public bool IsContextLimited => !Contexts.IsDefaultOrEmpty;
}
