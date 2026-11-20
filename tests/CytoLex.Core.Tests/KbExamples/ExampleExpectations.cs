using System.Globalization;

namespace CytoLex.Core.Tests.KbExamples;

/// <summary>Result of checking one KB example case: either skipped (with the reason) or a list of failures.</summary>
internal sealed record ExampleOutcome(string? SkipReason, IReadOnlyList<string> Failures)
{
    public static ExampleOutcome Skip(string reason) => new(reason, []);

    public bool Passed => SkipReason is null && Failures.Count == 0;
}

/// <summary>
/// What a KB example must produce (T-025; T-021/T-022 notes; docs/design/core.md §8–§9):
/// <list type="bullet">
/// <item><b>Valid</b> (whole string): valid without a hint, valid in the rule's contexts, no errors with a matching hint; the rule never fires
/// (except a "can't be verified from the string" info, T-056).</item>
/// <item><b>Valid</b> (fragment): only "the rule never fires".</item>
/// <item><b>Invalid</b>: without a hint the rule fires (firm or conditional, never silence); with a matching hint it fires firmly;
/// severity = KB severity, at most warning for <c>needs_review</c> rules and uncertain findings. A rule without a <c>[KbRule]</c>
/// check is still run (the parser reports surface parts of some rules): if it fires without a hint the case is checked as
/// usual, otherwise it is skipped as not implemented (decided in T-028). An invalid example whose own markers set a
/// context outside the rule's contexts (<c>rec</c> → constitutional for the neoplastic-only <c>REC.NOT_ACQUIRED</c>) is
/// checked only with the matching hint: without one the string is consistent with itself (decided in T-031).</item>
/// <item><b>Corrected</b>: the corrected form passes as a valid example; with a fixer, when the KB fix is level 1 and the
/// rule is implemented or reported by the parser, a fix of the rule's diagnostic previews it exactly (T-035). Level-3
/// rules have no single fix, so their corrected forms are checked for validity only.</item>
/// <item>No <c>CORE.UNPARSED</c> on any example.</item>
/// </list>
/// </summary>
internal static class ExampleExpectations
{
    public const string Unparsed = "CORE.UNPARSED";
    public const string NoValidator = "no validator yet (T-028)";

    public static ExampleOutcome Evaluate(KbExampleCase c, IExampleValidator? validator, bool ruleImplemented)
    {
        if (validator is null)
        {
            return ExampleOutcome.Skip(NoValidator);
        }

        if (c.Kind == ExampleKind.Invalid && !ruleImplemented
            && !validator.Run(c.Rule.Edition, c.Text, null, c.IsFragment).Diagnostics.Any(d => d.RuleId == c.Rule.Id))
        {
            return ExampleOutcome.Skip($"rule {c.Rule.Id} not implemented");
        }

        var failures = new List<string>();
        switch (c.Kind)
        {
            case ExampleKind.Valid:
                CheckValid(c, c.Text, validator, failures);
                break;
            case ExampleKind.Invalid:
                CheckInvalid(c, validator, failures);
                break;
            case ExampleKind.Corrected:
                CheckValid(c, c.Corrected!, validator, failures);
                if (validator.SupportsFixes && c.Rule.FixLevel == 1)
                {
                    CheckFixPreview(c, validator, ruleImplemented, failures);
                }

                break;
        }

        return new ExampleOutcome(null, failures);
    }

    /// <summary>The runs to make: without a hint, then once per context the rule is limited to.</summary>
    private static IEnumerable<SampleContext?> Hints(KbRuleInfo rule) =>
        [null, .. rule.SampleContexts.Select(c => (SampleContext?)c)];

    private static ExampleRun Run(KbExampleCase c, IExampleValidator v, string text, SampleContext? hint, List<string> failures)
    {
        var run = v.Run(c.Rule.Edition, text, hint, c.IsFragment);
        foreach (var d in run.Diagnostics.Where(d => d.RuleId == Unparsed))
        {
            failures.Add($"{Label(text, hint)}: {Unparsed} (parser could not attribute text to a KB rule; core.md §8.4).");
        }

        return run;
    }

    private static void CheckValid(KbExampleCase c, string text, IExampleValidator v, List<string> failures)
    {
        foreach (var hint in Hints(c.Rule))
        {
            var run = Run(c, v, text, hint, failures);
            // A "can't be verified from the string" info is expected on valid uses of the notation (T-056).
            foreach (var d in run.Diagnostics.Where(d => d.RuleId == c.Rule.Id && !(d.CannotVerify && d.Severity == Severity.Info)))
            {
                failures.Add($"{Label(text, hint)}: its own rule fired ({Describe(d)}).");
            }

            if (c.IsFragment)
            {
                continue;
            }

            if (hint is null)
            {
                if (!run.IsValid)
                {
                    failures.Add($"{Label(text, hint)}: not valid; errors: {Errors(run)}.");
                }

                foreach (var ctx in c.Rule.SampleContexts.Where(ctx => !run.ValidContexts.Contains(ctx)))
                {
                    failures.Add($"{Label(text, hint)}: not valid as {ctx}, the rule's context.");
                }
            }
            else if (run.Diagnostics.Any(d => d.Severity == Severity.Error))
            {
                failures.Add($"{Label(text, hint)}: errors: {Errors(run)}.");
            }
        }
    }

    private static void CheckInvalid(KbExampleCase c, IExampleValidator v, List<string> failures)
    {
        foreach (var hint in Hints(c.Rule))
        {
            var run = Run(c, v, c.Text, hint, failures);
            var own = run.Diagnostics.Where(d => d.RuleId == c.Rule.Id).ToList();
            if (own.Count == 0 && hint is null && run.InferredContext is { } inferred
                && c.Rule.SampleContexts.Count > 0 && !c.Rule.SampleContexts.Contains(inferred))
            {
                continue;
            }

            if (own.Count == 0)
            {
                var others = run.Diagnostics.Count == 0 ? "none" : string.Join(", ", run.Diagnostics.Select(Describe));
                failures.Add($"{Label(c.Text, hint)}: rule {c.Rule.Id} did not fire (diagnostics: {others}).");
                continue;
            }

            if (hint is not null && own.All(d => d.Conditional))
            {
                failures.Add($"{Label(c.Text, hint)}: rule {c.Rule.Id} fired only conditionally despite the matching context hint.");
            }

            foreach (var d in own)
            {
                var expected = ExpectedSeverity(c.Rule, d);
                if (d.Severity != expected)
                {
                    failures.Add($"{Label(c.Text, hint)}: severity {d.Severity}, expected {expected} (KB {c.Rule.Severity}{(c.Rule.NeedsReview ? ", needs_review" : "")}{(d.Uncertain ? ", uncertain" : "")}).");
                }
            }
        }
    }

    private static void CheckFixPreview(KbExampleCase c, IExampleValidator v, bool ruleImplemented, List<string> failures)
    {
        var own = Hints(c.Rule)
            .SelectMany(hint => Run(c, v, c.Text, hint, failures).Diagnostics)
            .Where(d => d.RuleId == c.Rule.Id)
            .ToList();
        if (!ruleImplemented && own.Count == 0)
        {
            return; // neither a check nor the parser reports the rule yet
        }

        var previews = own
            .SelectMany(d => d.Previews)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!previews.Contains(c.Corrected!, StringComparer.Ordinal))
        {
            var got = previews.Count == 0 ? "no fix" : string.Join(" | ", previews);
            failures.Add($"'{c.Text}': no fix of {c.Rule.Id} previews '{c.Corrected}' (got: {got}).");
        }
    }

    /// <summary>KB severity, capped at warning for <c>needs_review</c> rules and uncertain findings (core.md §8.2 steps 2–3).</summary>
    public static Severity ExpectedSeverity(KbRuleInfo rule, ExampleDiagnostic d) =>
        (rule.NeedsReview || d.Uncertain) && rule.Severity > Severity.Warning ? Severity.Warning : rule.Severity;

    private static string Label(string text, SampleContext? hint) =>
        hint is null ? $"'{text}' (no hint)" : string.Create(CultureInfo.InvariantCulture, $"'{text}' (hint {hint})");

    private static string Describe(ExampleDiagnostic d) =>
        $"{d.RuleId} {d.Severity}{(d.Conditional ? " conditional" : "")}";

    private static string Errors(ExampleRun run)
    {
        var errors = run.Diagnostics.Where(d => d.Severity == Severity.Error).Select(Describe).ToList();
        return errors.Count == 0 ? "(none listed)" : string.Join(", ", errors);
    }
}
