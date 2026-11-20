using System.Collections.Immutable;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Batch;

/// <summary>
/// Validates a batch of strings in parallel (T-038). The <see cref="Validator"/> is thread-safe (docs/design/core.md §1);
/// results keep the input order.
/// </summary>
public static class BatchRunner
{
    /// <summary>
    /// Validates every item against the editions in <paramref name="options"/>. <paramref name="progress"/> gets the number
    /// of finished strings (throttled to about 200 reports, always including the last one).
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static async Task<BatchResult> RunAsync(
        Validator validator,
        IReadOnlyList<BatchItem> items,
        ValidationOptions? options = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(items);
        options ??= ValidationOptions.Default;

        var reports = new ValidationReport[items.Count];
        var done = 0;
        var step = Math.Max(1, items.Count / 200);
        var parallel = new ParallelOptions { CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(Enumerable.Range(0, items.Count), parallel, (i, _) =>
        {
            reports[i] = validator.Validate(items[i].Input, options);
            var n = Interlocked.Increment(ref done);
            if (n % step == 0 || n == items.Count)
            {
                progress?.Report(n);
            }

            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);

        return new BatchResult(EditionsOf(validator, options), [.. items.Select((item, i) => new BatchItemResult(item, reports[i]))]);
    }

    /// <summary>The synchronous form of <see cref="RunAsync"/>.</summary>
    public static BatchResult Run(Validator validator, IReadOnlyList<BatchItem> items, ValidationOptions? options = null) =>
        RunAsync(validator, items, options).GetAwaiter().GetResult();

    /// <summary>The editions a call with <paramref name="options"/> reports, in result order.</summary>
    public static ImmutableArray<string> EditionsOf(Validator validator, ValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(options);
        return options.Editions.IsAll ? validator.KnownEditions : [options.Editions.Edition ?? validator.DefaultEdition];
    }
}
