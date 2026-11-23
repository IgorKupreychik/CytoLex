using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Validation;
using CoreContext = CytoLex.Core.Context.SampleContext;
using CoreSeverity = CytoLex.Core.Validation.Severity;

namespace CytoLex.Core.Tests.Corpus;

/// <summary>
/// What a corpus entry must produce in one edition (T-062):
/// <list type="bullet">
/// <item><b>Every entry</b> (parse-only): no <c>CORE.UNPARSED</c> unless its <c>fires</c> name it. Skipped when none of
/// its segments is implemented in the pilot (rsa, seq, breakage, meiotic).</item>
/// <item><b>Enforced labels</b> (<see cref="CorpusData.IsEnforced"/>), run with the entry's context hint:
/// <c>valid</c> = valid with no errors; <c>invalid</c> = not valid and every rule in <c>fires</c> reported (others may
/// be reported too). A label is checked only when every segment is implemented; otherwise the entry stays parse-only.</item>
/// </list>
/// </summary>
internal static class CorpusExpectations
{
    public const string Unparsed = "CORE.UNPARSED";

    /// <summary>Validates <paramref name="text"/> in one edition with an optional context hint.</summary>
    public static EditionResult Run(string edition, string text, SampleContext? hint) =>
        Validator.Default.Validate(text, new ValidationOptions
        {
            Editions = EditionSelection.One(edition),
            ContextHint = hint switch
            {
                SampleContext.Constitutional => CoreContext.Constitutional,
                SampleContext.Acquired => CoreContext.Acquired,
                _ => null,
            },
        }).Results.Single();

    /// <summary>Not-implemented reason when no segment of the result is implemented; otherwise <c>null</c>.</summary>
    public static string? NotImplemented(EditionResult result) =>
        !result.Segments.IsEmpty && result.Segments.All(s => s.Status == EditionStatus.NotImplemented)
            ? string.Join("; ", result.Segments.Select(s => s.Reason).Distinct(StringComparer.Ordinal))
            : null;

    public static ExampleOutcome Evaluate(CorpusEntry entry, string edition, IReadOnlySet<string> kbRules)
    {
        var expected = entry.Expected.GetValueOrDefault(edition);
        var hint = expected?.Context;
        var result = Run(edition, entry.Iscn, hint);
        if (NotImplemented(result) is { } notImplemented)
        {
            return ExampleOutcome.Skip(notImplemented);
        }

        var failures = new List<string>();
        var fires = expected?.Fires ?? [];
        if (!fires.Contains(Unparsed) && result.Diagnostics.Any(d => d.RuleId == Unparsed))
        {
            failures.Add($"{Unparsed}: {Describe(result)}.");
        }

        if (expected is null || !CorpusData.IsEnforced(entry, edition, kbRules, out _))
        {
            return new ExampleOutcome(null, failures);
        }

        var partial = result.Segments.Where(s => s.Status == EditionStatus.NotImplemented).Select(s => s.Segment.Type).ToList();
        if (partial.Count > 0)
        {
            return new ExampleOutcome(null, failures); // label can't be checked on a partly implemented string
        }

        switch (expected.Verdict)
        {
            case CorpusVerdict.Valid:
                if (!result.Validity.IsValid || result.Diagnostics.Any(d => d.Severity == CoreSeverity.Error))
                {
                    failures.Add($"expected valid{Hint(hint)}; got: {Describe(result)}.");
                }

                break;
            case CorpusVerdict.Invalid:
                var missing = fires.Where(f => !result.Diagnostics.Any(d => d.RuleId == f)).ToList();
                var unimplemented = missing.Where(f => !f.StartsWith(CorpusData.CorePrefix, StringComparison.Ordinal)
                    && !RuleImplementationScan.Default.IsImplemented(edition, f)).ToList();
                if (unimplemented.Count > 0)
                {
                    // Like the KB-example harness: a rule without a check (and not reported by the parser) can't fire yet.
                    return ExampleOutcome.Skip($"rule {string.Join(", ", unimplemented)} not implemented");
                }

                if (missing.Count > 0)
                {
                    failures.Add($"expected {string.Join(", ", missing)} to fire{Hint(hint)}; got: {Describe(result)}.");
                }
                else if (result.Validity.IsValid && !BelowErrorByPolicy(edition, fires, result))
                {
                    failures.Add($"expected invalid{Hint(hint)}, but it is valid; got: {Describe(result)}.");
                }

                break;
        }

        return new ExampleOutcome(null, failures);
    }

    /// <summary>
    /// Whether every reported <paramref name="fires"/> rule is below error by the severity policy (T-022; core.md §8.2):
    /// a KB severity below error, a <c>needs_review</c> rule or an uncertain finding (capped at warning). Then an invalid
    /// entry may still be valid.
    /// </summary>
    private static bool BelowErrorByPolicy(string edition, IEnumerable<string> fires, EditionResult result)
    {
        var rules = KbCatalog.Default.Rules.Where(r => r.Edition == edition).ToDictionary(r => r.Id, StringComparer.Ordinal);
        return result.Diagnostics.Where(d => fires.Contains(d.RuleId)).All(d =>
            rules.TryGetValue(d.RuleId, out var rule)
            && (rule.Severity < KbExamples.Severity.Error || rule.NeedsReview || d.Certainty == Certainty.Uncertain));
    }

    public static string Describe(EditionResult result) =>
        result.Diagnostics.IsEmpty
            ? "no diagnostics"
            : string.Join(", ", result.Diagnostics.Select(d => $"{d.RuleId} {d.Severity}{(d.IsConditional ? " conditional" : "")}"));

    private static string Hint(SampleContext? hint) => hint is null ? "" : $" (hint {hint})";
}
