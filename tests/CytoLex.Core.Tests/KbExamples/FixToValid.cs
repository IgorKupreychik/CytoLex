using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CytoLex.Core.Validation;
using CoreContext = CytoLex.Core.Context.SampleContext;
using CoreSeverity = CytoLex.Core.Validation.Severity;

namespace CytoLex.Core.Tests.KbExamples;

/// <summary>How a string ends after repeated "fix all" (T-040).</summary>
internal enum FixToValidKind
{
    /// <summary>Valid before any fix (the finding is a warning or info, or the string is valid in another context).</summary>
    ValidAsIs,

    /// <summary>Valid after one or more rounds of level-1 fixes.</summary>
    Fixed,

    /// <summary>Not valid, and every error left has only an explanation (no certain edit); counted, not failed.</summary>
    StopsAtExplanation,

    /// <summary>A failure: still fixable after the last round, or a fix brought in an error rule the input did not have.</summary>
    Failed,

    /// <summary>Not checked: no implemented segment, or the rule does not fire (not implemented).</summary>
    Skipped,
}

/// <summary>The outcome of <see cref="FixToValid.Run(KbExampleCase)"/>.</summary>
/// <param name="Kind">How the string ended.</param>
/// <param name="Text">The string after the fixes.</param>
/// <param name="Rounds">The fix-all rounds applied.</param>
/// <param name="Remaining">The error rule IDs left (sorted, distinct).</param>
/// <param name="Message">The failure or skip reason.</param>
internal sealed record FixToValidOutcome(FixToValidKind Kind, string Text, int Rounds, ImmutableArray<string> Remaining, string? Message);

/// <summary>
/// The T-040 acceptance: applying the fixes of an invalid string yields a valid one. A whole string is validated, then
/// <see cref="Fixer.ApplyAll"/> is applied and the result re-validated, up to <see cref="MaxRounds"/> rounds, until it is
/// valid or no level-1 fix applies. It must end valid, unless every error left is explanation-only and was already
/// reported on the input (a fix must not bring in a new error rule). A book fragment is checked only for its own rule:
/// after its level-1 fixes the rule no longer fires. The context is the rule's single context when it has one (the
/// context the example is invalid in), else the string's own.
/// </summary>
internal static class FixToValid
{
    public const int MaxRounds = 5;

    /// <summary>The repo-relative path of the report.</summary>
    public const string RelativePath = "build/review/fix-coverage.md";

    public static FixToValidOutcome Run(KbExampleCase c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var hint = c.Rule.SampleContexts.Count == 1
            ? c.Rule.SampleContexts[0] == SampleContext.Constitutional ? CoreContext.Constitutional : CoreContext.Acquired
            : (CoreContext?)null;
        return Run(c.Rule.Edition, c.Text, hint, c.Rule.Id, c.IsFragment);
    }

    /// <summary>Repeated fix all on <paramref name="input"/>.</summary>
    /// <param name="edition">The edition to validate in.</param>
    /// <param name="input">The string.</param>
    /// <param name="hint">The context hint; <c>null</c> = none (fixes for the inferred context, if any).</param>
    /// <param name="rule">The KB rule the string is an invalid example of: it must fire, else the case is skipped (not implemented); <c>null</c> for other strings.</param>
    /// <param name="isFragment">A book fragment: only <paramref name="rule"/>'s fixes are applied, and it must stop firing.</param>
    public static FixToValidOutcome Run(string edition, string input, CoreContext? hint, string? rule = null, bool isFragment = false)
    {
        var options = new ValidationOptions { Editions = EditionSelection.One(edition), ContextHint = hint, IsFragment = isFragment };
        var report = Validator.Default.Validate(input, options);
        var result = report.Results.Single();
        if (!result.Segments.IsEmpty && result.Segments.All(s => s.Status == EditionStatus.NotImplemented))
        {
            return Skip(input, string.Join("; ", result.Segments.Select(s => s.Reason).Distinct(StringComparer.Ordinal)));
        }

        if (rule is not null && !result.Diagnostics.Any(d => d.RuleId == rule))
        {
            return Skip(input, $"rule {rule} does not fire");
        }

        return isFragment && rule is not null ? RunFragment(input, options, result, rule) : RunWhole(input, options, report);
    }

    private static FixToValidOutcome RunWhole(string input, ValidationOptions options, ValidationReport report)
    {
        var result = report.Results.Single();
        var original = Errors(result);
        if (result.Validity.IsValid)
        {
            return new(FixToValidKind.ValidAsIs, input, 0, [], null);
        }

        var text = input;
        for (var round = 1; round <= MaxRounds + 1; round++)
        {
            var all = Fixer.ApplyAll(text, result, options.ContextHint ?? report.Context.Effective);
            if (all.Applied.IsEmpty)
            {
                var remaining = Errors(result);
                var brought = remaining.Except(original, StringComparer.Ordinal).ToList();
                return brought.Count == 0
                    ? new(FixToValidKind.StopsAtExplanation, text, round - 1, remaining, null)
                    : new(FixToValidKind.Failed, text, round - 1, remaining, $"the fixes brought in {string.Join(", ", brought)}");
            }

            if (round > MaxRounds)
            {
                return new(FixToValidKind.Failed, text, MaxRounds, Errors(result), $"still fixable after {MaxRounds} rounds");
            }

            text = all.Text;
            report = Validator.Default.Validate(text, options);
            result = report.Results.Single();
            if (result.Validity.IsValid)
            {
                return new(FixToValidKind.Fixed, text, round, [], null);
            }
        }

        throw new InvalidOperationException("unreachable");
    }

    private static FixToValidOutcome RunFragment(string input, ValidationOptions options, EditionResult result, string rule)
    {
        var text = input;
        for (var round = 1; round <= MaxRounds; round++)
        {
            var fix = result.Diagnostics.Where(d => d.RuleId == rule).SelectMany(d => d.Fixes)
                .FirstOrDefault(f => f.IsEdit && (f.ForContext is null || f.ForContext == options.ContextHint));
            if (fix is null)
            {
                return new(FixToValidKind.StopsAtExplanation, text, round - 1, [rule], null);
            }

            text = Fixer.Apply(text, fix);
            result = Validator.Default.Validate(text, options).Results.Single();
            if (!result.Diagnostics.Any(d => d.RuleId == rule))
            {
                return new(FixToValidKind.Fixed, text, round, [], null);
            }
        }

        return new(FixToValidKind.Failed, text, MaxRounds, [rule], $"{rule} still fires after {MaxRounds} fixes");
    }

    private static FixToValidOutcome Skip(string input, string reason) => new(FixToValidKind.Skipped, input, 0, [], reason);

    // Error rule IDs the result is invalid by: firm errors, or conditional ones that are errors in the context they apply to.
    private static ImmutableArray<string> Errors(EditionResult result) =>
    [
        .. result.Diagnostics
            .Where(d => d.Severity == CoreSeverity.Error || (d.Condition is { } c && Enum.GetValues<CoreContext>().Any(x => c.In(x) == CoreSeverity.Error)))
            .Select(d => d.RuleId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>Runs every invalid KB example and writes <see cref="RelativePath"/>; returns the full path.</summary>
    public static string WriteReport(KbCatalog catalog)
    {
        var rows = catalog.Cases.Where(c => c.Kind == ExampleKind.Invalid).Select(c => (Case: c, Outcome: Run(c))).ToList();
        var path = Path.Combine(RuleCoverageReport.RepoRoot(), RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Render(rows), new UTF8Encoding(false));
        return path;
    }

    internal static string Render(IReadOnlyList<(KbExampleCase Case, FixToValidOutcome Outcome)> rows)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("# Fix coverage (T-040)\n\n");
        sb.Append("Generated by the CytoLex.Core.Tests run (`FixToValidTests`). Every invalid KB example, fixed with up to ")
            .Append(MaxRounds.ToString(inv))
            .Append(" rounds of \"fix all\" (fragments: their own rule's fixes only).\n\n");
        sb.Append("| Outcome | Examples |\n|---|---|\n");
        foreach (var kind in Enum.GetValues<FixToValidKind>())
        {
            sb.Append(inv, $"| {Name(kind)} | {rows.Count(r => r.Outcome.Kind == kind)} |\n");
        }

        foreach (var kind in new[] { FixToValidKind.Failed, FixToValidKind.StopsAtExplanation, FixToValidKind.Fixed })
        {
            var group = rows.Where(r => r.Outcome.Kind == kind).ToList();
            if (group.Count == 0)
            {
                continue;
            }

            sb.Append(inv, $"\n## {Name(kind)} ({group.Count})\n\n");
            if (kind == FixToValidKind.StopsAtExplanation)
            {
                sb.Append("Errors left with an explanation only, by rule:\n\n");
                foreach (var byRule in group.SelectMany(r => r.Outcome.Remaining).GroupBy(id => id, StringComparer.Ordinal)
                             .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
                {
                    sb.Append(inv, $"- `{byRule.Key}`: {byRule.Count()}\n");
                }

                sb.Append('\n');
            }

            foreach (var (c, o) in group.OrderBy(r => r.Case.Key, StringComparer.Ordinal))
            {
                var line = kind switch
                {
                    FixToValidKind.Fixed => $"- `{c.Key}`: `{c.Text}` → `{o.Text}` ({o.Rounds} round(s))",
                    FixToValidKind.StopsAtExplanation => $"- `{c.Key}`: `{c.Text}` → `{o.Text}`; left: {string.Join(", ", o.Remaining)}",
                    _ => $"- `{c.Key}`: `{c.Text}` → `{o.Text}`: {o.Message}",
                };
                sb.Append(line).Append('\n');
            }
        }

        return sb.ToString();
    }

    private static string Name(FixToValidKind kind) => kind switch
    {
        FixToValidKind.ValidAsIs => "Valid as is (warning/info rule or other context)",
        FixToValidKind.Fixed => "Fixed to valid",
        FixToValidKind.StopsAtExplanation => "Stops at an explanation",
        FixToValidKind.Failed => "Failed",
        _ => "Skipped (not implemented)",
    };
}
