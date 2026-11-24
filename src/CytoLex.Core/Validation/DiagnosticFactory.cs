using System.Collections.Immutable;
using System.Globalization;
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
    public static Diagnostic? FromSyntax(IEditionKb kb, string input, SyntaxDiagnostic d, SampleContext? effective)
    {
        if (d.RuleId == CoreCodes.Unparsed)
        {
            return Core(CoreCodes.Unparsed, Severity.Error, d.Span, [], "Text not attributed to any KB rule", d.Message, DiagnosticOrigin.Parser);
        }

        var edits = d.FixEdits;
        var finding = new RuleFinding(d.Span, d.Message, [], Certainty.Certain, [], CannotVerify: false)
        {
            Fixes = edits.IsEmpty ? [] : [new FixProposal(edits)],
        };
        return FromFinding(RuleOf(kb, d.RuleId), input, finding, DiagnosticOrigin.Parser, effective);
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
    public static Diagnostic? FromFinding(KbRule rule, string input, RuleFinding f, DiagnosticOrigin origin, SampleContext? effective)
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
            Fixes(rule, input, f, condition, effective));
    }

    /// <summary>
    /// The fixes of a finding (docs/design/core.md §10, T-035): one level-1 fix per proposal when the KB rule's fix is
    /// level 1 (kind and hint from the KB, preview = the edits applied); otherwise, or when the check proposes no edit, a
    /// level-3 explanation (statement, hint and rendered citations). A proposal for a context the known effective context
    /// excludes is dropped. "Did you mean" options of the finding (T-041) come after the level-1 fixes and before the
    /// explanation, which a finding with options keeps.
    /// </summary>
    private static ImmutableArray<Fix> Fixes(KbRule rule, string input, RuleFinding f, ContextCondition? condition, SampleContext? effective)
    {
        var level1 = new List<Fix>();
        if (rule.Fix is { Level: 1 } hint && !f.Fixes.IsDefaultOrEmpty)
        {
            var kind = ParseKind(hint.Kind);
            foreach (var p in f.Fixes.Where(p => !p.Edits.IsDefaultOrEmpty))
            {
                var forContext = p.ForContext;
                if (forContext is { } fc && effective is { } known && fc != known)
                {
                    continue;
                }

                if (forContext is null && condition is not null && effective is null)
                {
                    var contexts = condition.PerContext.Where(kv => kv.Value is not null).Select(kv => kv.Key).ToList();
                    forContext = contexts.Count == 1 ? contexts[0] : null;
                }

                level1.Add(new Fix(FixLevel.Deterministic, kind, p.Edits, hint.Hint ?? rule.Title, forContext, Fixer.Apply(input, p.Edits), rule.Citations));
            }
        }

        var suggestions = f.Suggestions.IsDefaultOrEmpty
            ? []
            : f.Suggestions.Take(MaxSuggestions).Select(s => Suggestion(rule, input, s)).ToList();
        return level1.Count > 0 ? [.. level1, .. suggestions] : [.. suggestions, Explanation(rule)];
    }

    /// <summary>The most "did you mean" options one diagnostic shows (T-041).</summary>
    public const int MaxSuggestions = 3;

    /// <summary>A level-2 "did you mean" fix of <paramref name="rule"/> (T-041): one replace, with its preview.</summary>
    public static Fix Suggestion(KbRule rule, string input, SuggestionProposal s) =>
        new(FixLevel.Suggestion, FixKind.Replace, [s.Edit], s.Description, null, Fixer.Apply(input, [s.Edit]), rule.Citations);

    /// <summary>
    /// Adds "did you mean" options to a diagnostic that has none yet (T-041): before its explanation, after any level-1
    /// fix; at most <see cref="MaxSuggestions"/> in total.
    /// </summary>
    public static Diagnostic WithSuggestions(Diagnostic d, KbRule rule, string input, IEnumerable<SuggestionProposal> suggestions)
    {
        var added = suggestions.Take(MaxSuggestions).Select(s => Suggestion(rule, input, s)).ToList();
        if (added.Count == 0)
        {
            return d;
        }

        var level1 = d.Fixes.Where(x => x.Level == FixLevel.Deterministic);
        var rest = d.Fixes.Where(x => x.Level != FixLevel.Deterministic);
        return d with { Fixes = [.. level1, .. added, .. rest] };
    }

    /// <summary>
    /// Adds a "fix every occurrence" alternative (T-035): when one rule reports several faults in the string, each with
    /// its own context-free level-1 fix, and those fixes don't conflict, every one of these diagnostics also gets the fix
    /// that applies all of them (<c>mos 45,X(15)/47,XXX(10)</c>: both cell counts in square brackets). Diagnostics that
    /// share one fix (e.g. a cell line's order) are not combined.
    /// </summary>
    public static List<Diagnostic> WithRuleWideFixes(List<Diagnostic> diagnostics, string input)
    {
        var result = new List<Diagnostic>(diagnostics);
        foreach (var group in diagnostics.Where(d => d.Condition is null).GroupBy(d => d.RuleId, StringComparer.Ordinal))
        {
            var own = group
                .Select(d => d.Fixes.FirstOrDefault(f => f.IsEdit && f.ForContext is null))
                .OfType<Fix>()
                .DistinctBy(f => string.Join("|", f.Edits.Select(e => $"{e.Span.Start}:{e.Span.Length}:{e.NewText}")))
                .ToList();
            var edits = own.SelectMany(f => f.Edits).ToList();
            if (own.Count < 2 || !Fixer.AreDisjoint(edits))
            {
                continue;
            }

            var first = own[0];
            var all = first with
            {
                Edits = [.. edits],
                Description = string.Create(CultureInfo.InvariantCulture, $"{first.Description} (all {own.Count} in this string)"),
                Preview = Fixer.Apply(input, edits),
            };
            foreach (var d in group)
            {
                result[result.IndexOf(d)] = d with { Fixes = d.Fixes.Add(all) };
            }
        }

        return result;
    }

    /// <summary>A level-3 explanation: the rule statement, the KB hint, and the rendered citations.</summary>
    private static Fix Explanation(KbRule rule)
    {
        var text = rule.Statement.Trim();
        if (rule.Fix?.Hint is { Length: > 0 } hint)
        {
            text += " " + hint.Trim();
        }

        if (!rule.Citations.IsDefaultOrEmpty)
        {
            text += " (" + string.Join("; ", rule.Citations.Select(c => c.Rendered)) + ")";
        }

        return new Fix(FixLevel.Explanation, null, [], text, null, null, rule.Citations);
    }

    private static FixKind? ParseKind(string? kind) => kind switch
    {
        "replace" => FixKind.Replace,
        "insert" => FixKind.Insert,
        "delete" => FixKind.Delete,
        "reorder" => FixKind.Reorder,
        _ => null,
    };

    private static KbRule RuleOf(IEditionKb kb, string id) =>
        kb.Rule(id) ?? throw new InvalidOperationException($"Rule {id} is not in the ISCN {kb.Edition} KB (startup check missed it).");

    private static Diagnostic Core(string code, Severity severity, TextSpan span, ImmutableArray<TextSpan> secondary, string message, string? detail, DiagnosticOrigin origin) =>
        new(code, severity, span, secondary, message, detail, [], origin, false, null, Certainty.Certain, null, false, []);

    private static Severity Min(Severity a, Severity b) => a < b ? a : b;
}
