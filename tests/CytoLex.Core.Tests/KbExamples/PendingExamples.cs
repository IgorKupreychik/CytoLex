namespace CytoLex.Core.Tests.KbExamples;

/// <summary>
/// KB examples the core can't handle yet, each with the task that closes the gap (T-014, decided with the user: the KB
/// keeps the book's forms; the tests skip them with the reason instead of failing). A pending example that now passes
/// fails its test, so an entry is removed as soon as its task lands. Empty since T-032.
/// </summary>
internal static class PendingExamples
{
    public static readonly IReadOnlyDictionary<string, string> Table =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>For a pending example: fails when it now passes, otherwise skips with its reason. No-op for others.</summary>
    public static void SkipOrFailIfFixed(string text, bool passes)
    {
        if (!Table.TryGetValue(text, out var reason))
        {
            return;
        }

        Assert.False(passes, $"'{text}' now passes: remove it from {nameof(PendingExamples)}.");
        Assert.Skip($"pending - {reason}");
    }
}
