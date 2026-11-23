namespace CytoLex.Core.Tests.KbExamples;

/// <summary>
/// KB examples the core can't handle yet, each with the task that closes the gap (T-014, decided with the user: the KB
/// keeps the book's forms; the tests skip them with the reason instead of failing). A pending example that now passes
/// fails its test, so an entry is removed as soon as its task lands.
/// </summary>
internal static class PendingExamples
{
    public static readonly IReadOnlyDictionary<string, string> Table =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Inferred invalid example (T-015): without the period the karyotype parser leaves "ish …" unparsed.
            ["46,XY ish 22q11.2(D22S75×2)"] = "T-033: read a missing period before ish under ISH.COMBINED_WITH_KARYOTYPE (now CORE.UNPARSED)",
        };

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
