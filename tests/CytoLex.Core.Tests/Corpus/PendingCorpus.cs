namespace CytoLex.Core.Tests.Corpus;

/// <summary>
/// Corpus entries and mutants the core gets wrong, each with the task that closes the gap (T-062, like
/// <see cref="KbExamples.PendingExamples"/>). Keys are entry IDs (<c>K-001</c>) or mutant keys (<c>K-001~swap-chromosomes</c>).
/// A pending case that now passes fails its test, so remove the entry as soon as its task lands.
/// </summary>
internal static class PendingCorpus
{
    public static readonly IReadOnlyDictionary<string, string> Table =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>For a pending case: fails when it now passes, otherwise skips with its reason. No-op for others.</summary>
    public static void SkipOrFailIfFixed(string key, bool passes)
    {
        if (!Table.TryGetValue(key, out var reason))
        {
            return;
        }

        Assert.False(passes, $"'{key}' now passes: remove it from {nameof(PendingCorpus)}.");
        Assert.Skip($"pending - {reason}");
    }
}
