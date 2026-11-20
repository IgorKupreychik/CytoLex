using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>Diagnostic severity, ordered so that a larger value is more severe (docs/design/core.md §8.1).</summary>
public enum Severity
{
    /// <summary>A note; never affects validity.</summary>
    Info,

    /// <summary>Likely wrong or book-ambiguous; does not make the string invalid.</summary>
    Warning,

    /// <summary>Not valid ISCN in the edition.</summary>
    Error,
}

/// <summary>Whether a check could decide with certainty (§8.2 step 3); uncertain findings are capped at warning.</summary>
public enum Certainty
{
    /// <summary>The finding is certain.</summary>
    Certain,

    /// <summary>The check cannot decide for sure (counts with dic, ploidy ranges, <c>?</c>/<c>~</c>, …).</summary>
    Uncertain,
}

/// <summary>Which pipeline stage found a diagnostic (§2).</summary>
public enum DiagnosticOrigin
{
    /// <summary>A syntax error recovered by the parser (§8.4).</summary>
    Parser,

    /// <summary>Found while interpreting the tree (T-065).</summary>
    Binder,

    /// <summary>Reported by a rule check bound to a KB rule (§7.2).</summary>
    Rule,

    /// <summary>From the engine itself, e.g. contradicting context markers (<see cref="Syntax.CoreCodes"/>).</summary>
    Core,
}

/// <summary>
/// Per-context severities of a diagnostic that depends on an unknown sample context (§8.3).
/// A context missing from <see cref="PerContext"/>, or mapped to <c>null</c>, has no finding.
/// </summary>
public sealed record ContextCondition(ImmutableDictionary<SampleContext, Severity?> PerContext)
{
    /// <summary>The severity in <paramref name="context"/>; <c>null</c> = no finding there.</summary>
    public Severity? In(SampleContext context) => PerContext.GetValueOrDefault(context);
}

/// <summary>
/// One finding in one edition (docs/design/core.md §8.1). Severity, citations and review state come from the KB rule
/// (§8.2); the check or parser only gives the facts (span, detail, certainty).
/// </summary>
/// <param name="RuleId">The KB rule ID, or a core code (<see cref="Syntax.CoreCodes"/>).</param>
/// <param name="Severity">The effective severity; for a conditional diagnostic the worst over the contexts.</param>
/// <param name="Span">The primary span in the whole input; zero-length = an insertion point.</param>
/// <param name="SecondarySpans">Further spans involved (e.g. the other context markers).</param>
/// <param name="Message">The KB rule title (or the core code's description).</param>
/// <param name="Detail">What the parser or check found, if it said more than the title.</param>
/// <param name="Citations">The KB citations; empty for core codes.</param>
/// <param name="Origin">Which stage found it.</param>
/// <param name="BookAmbiguous">The KB rule has <c>needs_review</c>; the severity is capped at warning (T-022).</param>
/// <param name="ReviewNote">The KB review note of such a rule.</param>
/// <param name="Certainty">Whether the check was certain.</param>
/// <param name="Condition">Set when the finding depends on the unknown sample context (§8.3).</param>
/// <param name="CannotVerify">An info that the string alone cannot show whether the rule is met (§8.2 step 5).</param>
/// <param name="Fixes">Proposed fixes (T-035): level-1 edits (one per alternative or context, plus "all occurrences" when one rule reports several faults), else one level-3 explanation; empty only for core codes.</param>
public sealed record Diagnostic(
    string RuleId,
    Severity Severity,
    TextSpan Span,
    ImmutableArray<TextSpan> SecondarySpans,
    string Message,
    string? Detail,
    ImmutableArray<Citation> Citations,
    DiagnosticOrigin Origin,
    bool BookAmbiguous,
    string? ReviewNote,
    Certainty Certainty,
    ContextCondition? Condition,
    bool CannotVerify,
    ImmutableArray<Fix> Fixes)
{
    /// <summary>Whether the finding depends on the unknown sample context.</summary>
    public bool IsConditional => Condition is not null;
}
