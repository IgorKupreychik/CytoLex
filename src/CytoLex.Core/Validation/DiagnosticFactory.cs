using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Rules;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>
/// Turns parser and check findings into <see cref="Diagnostic"/>s with the KB-driven severity resolution of
/// docs/design/core.md §8.2–§8.3 (T-022 policy). Severity, citations and review state come from the KB rule only.
/// </summary>
internal static class DiagnosticFactory
{
    private static readonly ImmutableArray<SampleContext> _allContexts = [.. Enum.GetValues<SampleContext>()];

    /// <summary>A parser finding (§8.4); <c>null</c> when the effective context excludes the rule.</summary>
    public static Diagnostic? FromSyntax(IEditionKb kb, SyntaxDiagnostic d, SampleContext? effective)
    {
        if (d.RuleId == CoreCodes.Unparsed)
        {
            return Core(CoreCodes.Unparsed, Severity.Error, d.Span, [], "Text not attributed to any KB rule", d.Message, DiagnosticOrigin.Parser);
        }

        var finding = new RuleFinding(d.Span, d.Message, [], Certainty.Certain, [], CannotVerify: false);
        return FromFinding(RuleOf(kb, d.RuleId), finding, DiagnosticOrigin.Parser, effective);
    }

    /// <summary>The info for contradicting context markers (<see cref="CoreCodes.ContextConflict"/>).</summary>
    public static Diagnostic ContextConflict(ContextInfo context)
    {
        var markers = context.Markers;
        var acquired = markers.Where(m => m.Indicates == SampleContext.Acquired).Select(m => m.Kind).Distinct();
        var constitutional = markers.Where(m => m.Indicates == SampleContext.Constitutional).Select(m => m.Kind).Distinct();
        return Core(
            CoreCodes.ContextConflict,
            Severity.Info,
            markers[0].Span,
            [.. markers.Skip(1).Select(m => m.Span)],
            "Context markers contradict; the sample context is unknown",
            $"acquired: {string.Join(", ", acquired)}; constitutional: {string.Join(", ", constitutional)}",
            DiagnosticOrigin.Core);
    }

    /// <summary>
    /// A finding of <paramref name="rule"/>, resolved per §8.2: KB severity → <c>needs_review</c> cap → uncertainty cap →
    /// can't-verify info → context (firm, dropped, or conditional). <c>null</c> when the known context excludes it.
    /// </summary>
    public static Diagnostic? FromFinding(KbRule rule, RuleFinding f, DiagnosticOrigin origin, SampleContext? effective)
    {
        var severity = rule.Severity;
        if (rule.NeedsReview)
        {
            severity = Min(severity, Severity.Warning);
        }

        if (f.Certainty == Certainty.Uncertain)
        {
            severity = Min(severity, Severity.Warning);
        }

        if (f.CannotVerify)
        {
            severity = Severity.Info;
        }

        var contexts = f.Contexts.IsDefaultOrEmpty ? rule.Contexts : f.Contexts;
        ContextCondition? condition = null;
        if (!contexts.IsDefaultOrEmpty && contexts.Length < _allContexts.Length)
        {
            if (effective is { } known)
            {
                if (!contexts.Contains(known))
                {
                    return null;
                }
            }
            else
            {
                condition = new ContextCondition(_allContexts.ToImmutableDictionary(c => c, c => contexts.Contains(c) ? severity : (Severity?)null));
            }
        }

        return new Diagnostic(
            rule.Id,
            severity,
            f.Span,
            f.SecondarySpans.IsDefault ? [] : f.SecondarySpans,
            rule.Title,
            f.Detail,
            rule.Citations,
            origin,
            BookAmbiguous: rule.NeedsReview,
            ReviewNote: rule.NeedsReview ? rule.ReviewNote : null,
            f.Certainty,
            condition,
            f.CannotVerify,
            Fixes: []);
    }

    private static KbRule RuleOf(IEditionKb kb, string id) =>
        kb.Rule(id) ?? throw new InvalidOperationException($"Rule {id} is not in the ISCN {kb.Edition} KB (startup check missed it).");

    private static Diagnostic Core(string code, Severity severity, TextSpan span, ImmutableArray<TextSpan> secondary, string message, string? detail, DiagnosticOrigin origin) =>
        new(code, severity, span, secondary, message, detail, [], origin, false, null, Certainty.Certain, null, false, []);

    private static Severity Min(Severity a, Severity b) => a < b ? a : b;
}
