using System.Collections.Immutable;
using CytoLex.Core.Batch;
using CytoLex.Core.Validation;

namespace CytoLex.External.Comparison;

/// <summary>Where a string falls in the comparison of CytoLex with an external checker (T-051).</summary>
public enum Agreement
{
    /// <summary>Both accept it.</summary>
    BothValid,

    /// <summary>Both reject it.</summary>
    BothInvalid,

    /// <summary>CytoLex rejects it, the checker accepts it.</summary>
    OnlyCytoLexInvalid,

    /// <summary>The checker rejects it, CytoLex accepts it.</summary>
    OnlyCheckerInvalid,

    /// <summary>Outside the checker's grammar (<see cref="IExternalCoverage"/>); kept out of the matrix.</summary>
    NotSupported,

    /// <summary>No verdict from one side (checker not configured or failed, edition not implemented); kept out of the matrix.</summary>
    NotCompared,
}

/// <summary>The 2×2 agreement matrix plus the strings kept out of it.</summary>
public sealed record AgreementMatrix(
    int BothValid,
    int BothInvalid,
    int OnlyCytoLexInvalid,
    int OnlyCheckerInvalid,
    int NotSupported,
    int NotCompared)
{
    /// <summary>The strings in the 2×2 matrix.</summary>
    public int Compared => BothValid + BothInvalid + OnlyCytoLexInvalid + OnlyCheckerInvalid;

    /// <summary>The strings both judge alike.</summary>
    public int Agreeing => BothValid + BothInvalid;

    /// <summary>The strings they judge differently.</summary>
    public int Disagreeing => OnlyCytoLexInvalid + OnlyCheckerInvalid;

    /// <summary>All strings.</summary>
    public int Total => Compared + NotSupported + NotCompared;

    internal static AgreementMatrix Of(IEnumerable<ComparisonItem> items)
    {
        var counts = items.GroupBy(i => i.Agreement).ToDictionary(g => g.Key, g => g.Count());
        int N(Agreement a) => counts.GetValueOrDefault(a);
        return new(N(Agreement.BothValid), N(Agreement.BothInvalid), N(Agreement.OnlyCytoLexInvalid),
            N(Agreement.OnlyCheckerInvalid), N(Agreement.NotSupported), N(Agreement.NotCompared));
    }
}

/// <summary>The comparison of one string.</summary>
/// <param name="Item">The batch item.</param>
/// <param name="CytoLexValid">CytoLex's verdict in the checker's edition; <c>null</c> when the edition was not validated.</param>
/// <param name="CytoLexComplete">Whether CytoLex validated every segment and construct.</param>
/// <param name="CytoLexErrors">The rule IDs of CytoLex's errors, distinct, in input order.</param>
/// <param name="CytoLexNotes">The rule IDs of CytoLex's warnings and infos, distinct, in input order.</param>
/// <param name="External">The checker's result.</param>
/// <param name="Agreement">Where the string falls.</param>
/// <param name="Reason">Why it is <see cref="Agreement.NotSupported"/> or <see cref="Agreement.NotCompared"/>.</param>
/// <param name="Triage">The triage entry for the string, if any.</param>
/// <param name="SuggestedLabel">
/// A label suggested for an untriaged disagreement: <see cref="TriageLabel.CheckerLimitation"/> when CytoLex alone rejects
/// it and every error rule is one the checker doesn't check (<see cref="IExternalCoverage.DoesNotCheck"/>);
/// <see cref="TriageLabel.SeverityPolicy"/> when the checker alone rejects it and CytoLex has warnings or infos.
/// </param>
public sealed record ComparisonItem(
    BatchItem Item,
    bool? CytoLexValid,
    bool CytoLexComplete,
    ImmutableArray<string> CytoLexErrors,
    ImmutableArray<string> CytoLexNotes,
    ExternalItemResult External,
    Agreement Agreement,
    string? Reason,
    TriageEntry? Triage,
    TriageLabel? SuggestedLabel)
{
    /// <summary>Whether CytoLex and the checker disagree on the string.</summary>
    public bool IsDisagreement => Agreement is Agreement.OnlyCytoLexInvalid or Agreement.OnlyCheckerInvalid;
}

/// <summary>The comparison of a batch.</summary>
/// <param name="Checker">The checker's name.</param>
/// <param name="Edition">The edition compared (the checker's).</param>
/// <param name="CheckerStatus">Whether the checker ran.</param>
/// <param name="CheckerMessage">Why it did not run or failed.</param>
/// <param name="Matrix">The agreement matrix.</param>
/// <param name="Items">One item per input, in input order.</param>
/// <param name="StaleTriage">Triage entries whose string is not a disagreement in this batch (empty unless the checker ran).</param>
public sealed record ComparisonResult(
    string Checker,
    string Edition,
    ExternalBatchStatus CheckerStatus,
    string? CheckerMessage,
    AgreementMatrix Matrix,
    ImmutableArray<ComparisonItem> Items,
    ImmutableArray<TriageEntry> StaleTriage);

/// <summary>
/// Compares CytoLex's verdicts with an external checker's on a batch (T-051): only in the checker's edition, with the
/// strings the checker can't read kept out of the matrix, and the disagreements matched with a triage file.
/// Never throws for checker problems (<see cref="IExternalChecker.CheckAsync"/> doesn't).
/// </summary>
public static class ComparisonEngine
{
    /// <summary>Runs CytoLex and the checker on <paramref name="items"/> and compares their verdicts.</summary>
    /// <exception cref="ArgumentException"><paramref name="triage"/> is for another checker or edition.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static async Task<ComparisonResult> CompareAsync(
        IReadOnlyList<BatchItem> items,
        Validator validator,
        IExternalChecker checker,
        IExternalCoverage coverage,
        TriageFile? triage = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(coverage);
        triage ??= TriageFile.Empty(checker.Name, checker.Edition);
        if (!string.Equals(triage.Checker, checker.Name, StringComparison.Ordinal)
            || !string.Equals(triage.Edition, checker.Edition, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The triage file is for {triage.Checker} {triage.Edition}, not {checker.Name} {checker.Edition}.", nameof(triage));
        }

        progress?.Report($"CytoLex and {checker.Name}: {items.Count} strings (ISCN {checker.Edition})");
        var options = new ValidationOptions { Editions = EditionSelection.One(checker.Edition) };
        var cytoLexTask = BatchRunner.RunAsync(validator, items, options, cancellationToken: cancellationToken);
        var externalTask = checker.CheckAsync([.. items.Select(i => i.Input)], progress, cancellationToken);
        var cytoLex = await cytoLexTask.ConfigureAwait(false);
        var external = await externalTask.ConfigureAwait(false);

        var compared = Classify(items, cytoLex, external, coverage, triage);
        var stale = external.Status == ExternalBatchStatus.Completed
            ? Stale(triage, compared)
            : [];
        return new ComparisonResult(checker.Name, checker.Edition, external.Status, external.Message,
            AgreementMatrix.Of(compared), compared, stale);
    }

    internal static ImmutableArray<ComparisonItem> Classify(
        IReadOnlyList<BatchItem> items,
        BatchResult cytoLex,
        ExternalBatchResult external,
        IExternalCoverage coverage,
        TriageFile triage)
    {
        if (cytoLex.Items.Length != items.Count || external.Items.Length != items.Count)
        {
            throw new InvalidOperationException("CytoLex and the checker must return one result per input.");
        }

        var result = ImmutableArray.CreateBuilder<ComparisonItem>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var edition = cytoLex.Items[i].Report.Results.Single();
            var ext = external.Items[i];
            bool? valid = edition.Status == EditionStatus.Implemented ? edition.Validity.IsValid : null;
            var errors = RuleIds(edition, error: true);
            var notes = RuleIds(edition, error: false);

            var (agreement, reason) = coverage.Unsupported(items[i].Input) is { } unsupported
                ? (Agreement.NotSupported, unsupported)
                : valid is null
                    ? (Agreement.NotCompared, edition.Reason ?? $"ISCN {edition.Edition} not validated")
                    : ext.Verdict switch
                    {
                        ExternalVerdict.Valid => (valid.Value ? Agreement.BothValid : Agreement.OnlyCytoLexInvalid, null),
                        ExternalVerdict.Invalid => (valid.Value ? Agreement.OnlyCheckerInvalid : Agreement.BothInvalid, null),
                        _ => (Agreement.NotCompared, external.Message ?? ext.ToolStatus),
                    };

            var disagreement = agreement is Agreement.OnlyCytoLexInvalid or Agreement.OnlyCheckerInvalid;
            var entry = disagreement ? triage.Find(items[i].Input) : null;
            TriageLabel? suggested = !disagreement || entry is not null ? null
                : agreement == Agreement.OnlyCytoLexInvalid && !errors.IsEmpty && errors.All(coverage.DoesNotCheck)
                    ? TriageLabel.CheckerLimitation
                    : agreement == Agreement.OnlyCheckerInvalid && !notes.IsEmpty
                        ? TriageLabel.SeverityPolicy
                        : null;
            result.Add(new ComparisonItem(items[i], valid, edition.Validity.IsComplete, errors, notes, ext, agreement, reason, entry, suggested));
        }

        return result.MoveToImmutable();
    }

    private static ImmutableArray<string> RuleIds(EditionResult edition, bool error) =>
    [
        .. edition.Diagnostics
            .Where(d => (d.Severity == Severity.Error) == error)
            .Select(d => d.RuleId)
            .Distinct(StringComparer.Ordinal),
    ];

    private static ImmutableArray<TriageEntry> Stale(TriageFile triage, ImmutableArray<ComparisonItem> items)
    {
        var disagreeing = items.Where(i => i.IsDisagreement).Select(i => i.Item.Input).ToHashSet(StringComparer.Ordinal);
        return [.. triage.Entries.Where(e => !disagreeing.Contains(e.Iscn))];
    }
}
