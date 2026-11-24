using System.Collections.Immutable;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Batch;

/// <summary>How many strings of a batch broke a rule (errors and warnings only).</summary>
/// <param name="RuleId">The KB rule ID or core code.</param>
/// <param name="Strings">The number of strings with at least one such diagnostic.</param>
public sealed record RuleCount(string RuleId, int Strings);

/// <summary>The totals of one edition over a batch (T-038).</summary>
/// <param name="Edition">The edition.</param>
/// <param name="Total">Strings in the batch.</param>
/// <param name="Valid">Validated and valid as written.</param>
/// <param name="NotValid">Validated and not valid.</param>
/// <param name="NotImplemented">Not validated: the edition is not implemented.</param>
/// <param name="Incomplete">Validated, but a segment or construct was not (valid "as far as implemented").</param>
/// <param name="TopRules">The rules with errors or warnings in the most strings, most first.</param>
/// <param name="ValidAfterAutoCorrection">Validated and valid only after autocorrection (T-096; 0 without it).</param>
public sealed record EditionSummary(
    string Edition,
    int Total,
    int Valid,
    int NotValid,
    int NotImplemented,
    int Incomplete,
    ImmutableArray<RuleCount> TopRules,
    int ValidAfterAutoCorrection);

/// <summary>Totals per edition of a <see cref="BatchResult"/>.</summary>
public static class BatchSummary
{
    /// <summary>One summary per edition of <paramref name="result"/>, in its order.</summary>
    public static ImmutableArray<EditionSummary> Of(BatchResult result, int topRules = 10)
    {
        ArgumentNullException.ThrowIfNull(result);
        return [.. result.Editions.Select(e => Of(result, e, topRules))];
    }

    private static EditionSummary Of(BatchResult result, string edition, int topRules)
    {
        var editionResults = result.Items
            .Select(i => i.Report.Results.FirstOrDefault(r => r.Edition == edition))
            .OfType<EditionResult>()
            .ToList();
        var implemented = editionResults.Where(r => r.Status == EditionStatus.Implemented).ToList();
        var rules = implemented
            .SelectMany(r => r.Diagnostics.Where(d => d.Severity >= Severity.Warning).Select(d => d.RuleId).Distinct(StringComparer.Ordinal))
            .GroupBy(id => id, StringComparer.Ordinal)
            .Select(g => new RuleCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Strings)
            .ThenBy(c => c.RuleId, StringComparer.Ordinal)
            .Take(topRules);
        return new EditionSummary(
            edition,
            result.Items.Length,
            implemented.Count(r => r.Verdict == EditionVerdict.Valid),
            implemented.Count(r => !r.Validity.IsValid),
            editionResults.Count - implemented.Count,
            implemented.Count(r => !r.Validity.IsComplete),
            [.. rules],
            implemented.Count(r => r.Verdict == EditionVerdict.ValidAfterAutoCorrection));
    }
}
