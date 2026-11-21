using System.Collections.Immutable;

namespace CytoLex.External;

/// <summary>
/// A checker outside CytoLex that judges ISCN strings, for comparison with our results (T-049; docs/decisions.md
/// "External comparison"). Implementations run locally only (decision T-048).
/// </summary>
public interface IExternalChecker
{
    /// <summary>The checker's name, e.g. <c>CytoGPS</c>.</summary>
    string Name { get; }

    /// <summary>The single ISCN edition the checker follows (comparisons use this edition only), e.g. <c>2016</c>.</summary>
    string Edition { get; }

    /// <summary>Whether the checker is set up on this machine; never starts a process.</summary>
    ExternalAvailability CheckAvailability();

    /// <summary>
    /// Checks a batch of strings. Never throws for setup or tool problems: a checker that is not set up returns
    /// <see cref="ExternalBatchStatus.NotConfigured"/>, a tool failure <see cref="ExternalBatchStatus.Failed"/>;
    /// either way there is one item per input, in input order.
    /// </summary>
    Task<ExternalBatchResult> CheckAsync(
        IReadOnlyList<string> inputs,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Whether an external checker can run, and if not, why.</summary>
/// <param name="IsConfigured">Whether every setting and file the checker needs is present.</param>
/// <param name="Reason">Why it is not configured (names the missing key, path or file); <c>null</c> when configured.</param>
public sealed record ExternalAvailability(bool IsConfigured, string? Reason)
{
    public static ExternalAvailability Configured { get; } = new(true, null);

    public static ExternalAvailability NotConfigured(string reason) => new(false, reason);
}

/// <summary>The outcome of a whole batch.</summary>
public enum ExternalBatchStatus
{
    /// <summary>The checker ran (items may still have failed one by one).</summary>
    Completed,

    /// <summary>The checker is not set up; nothing ran.</summary>
    NotConfigured,

    /// <summary>The checker was set up but failed (build, process, timeout, unreadable output).</summary>
    Failed,
}

/// <summary>The verdict of the external checker for one string, normalized.</summary>
public enum ExternalVerdict
{
    /// <summary>The checker accepts the string (for CytoGPS also after its own grammar repair).</summary>
    Valid,

    /// <summary>The checker rejects the string.</summary>
    Invalid,

    /// <summary>The checker is not set up; no verdict.</summary>
    NotConfigured,

    /// <summary>No verdict because the checker or this string failed (see <see cref="ExternalItemResult.ToolStatus"/>).</summary>
    Failed,
}

/// <summary>The kind of a message of the external checker.</summary>
public enum ExternalMessageKind
{
    /// <summary>A lexer or parser message.</summary>
    Grammar,

    /// <summary>A semantic validation message.</summary>
    Validation,
}

/// <summary>A message of the external checker, as it wrote it (CytoGPS gives no rule IDs or positions).</summary>
public sealed record ExternalMessage(ExternalMessageKind Kind, string Text);

/// <summary>
/// One clone of an accepted string with its Loss–Gain–Fusion counts per band (input to T-050/T-051).
/// </summary>
/// <param name="CellCount">The clone's cell count, if given.</param>
/// <param name="Relationship">The checker's clone relationship label, if any.</param>
/// <param name="UncertainEvents">Events the checker could not place, as written.</param>
/// <param name="DetailedSystems">The checker's detailed-system rendering of derivative chromosomes, as written.</param>
/// <param name="Loss">Loss counts, aligned with <see cref="ExternalBatchResult.Bands"/>.</param>
/// <param name="Gain">Gain counts, aligned with <see cref="ExternalBatchResult.Bands"/>.</param>
/// <param name="Fusion">Fusion counts, aligned with <see cref="ExternalBatchResult.Bands"/>.</param>
public sealed record ExternalClone(
    int? CellCount,
    string? Relationship,
    ImmutableArray<string> UncertainEvents,
    ImmutableArray<string> DetailedSystems,
    ImmutableArray<int> Loss,
    ImmutableArray<int> Gain,
    ImmutableArray<int> Fusion);

/// <summary>The normalized result of one string.</summary>
/// <param name="Input">The string as given.</param>
/// <param name="SentInput">The string as sent to the checker (e.g. printed symbols converted to their typed forms).</param>
/// <param name="Verdict">The normalized verdict.</param>
/// <param name="ToolStatus">The checker's own status (CytoGPS: one of its five statuses), or why the item failed.</param>
/// <param name="RevisedInput">The checker's repaired string, if it repaired one.</param>
/// <param name="Messages">The checker's messages.</param>
/// <param name="Clones">Clones with LGF counts (accepted strings only).</param>
public sealed record ExternalItemResult(
    string Input,
    string SentInput,
    ExternalVerdict Verdict,
    string ToolStatus,
    string? RevisedInput,
    ImmutableArray<ExternalMessage> Messages,
    ImmutableArray<ExternalClone> Clones);

/// <summary>The result of a batch.</summary>
/// <param name="Checker">The checker's name.</param>
/// <param name="Edition">The ISCN edition the checker follows.</param>
/// <param name="Status">Whether the batch ran.</param>
/// <param name="Message">Why the batch did not run or failed; <c>null</c> when completed.</param>
/// <param name="Bands">The checker's band list that LGF counts align with (empty unless it ran).</param>
/// <param name="Items">One result per input, in input order.</param>
public sealed record ExternalBatchResult(
    string Checker,
    string Edition,
    ExternalBatchStatus Status,
    string? Message,
    ImmutableArray<string> Bands,
    ImmutableArray<ExternalItemResult> Items);
