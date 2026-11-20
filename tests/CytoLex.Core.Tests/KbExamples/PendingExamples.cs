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
            ["45~48,XX,del(3)(p12)[2],–5[4],+8[2],+11[3][cp7]"] =
                "T-032: cell counts after an abnormality ([2]) are not parsed (§11.1.5 E2)",
            ["45~48,XX,del(3)(p12)[1],–5[4],+8[2][cp6]"] =
                "T-032: cell counts after an abnormality ([1]) are not parsed",
            ["46,XY,t(9;22)(q34;q11.2)[3]/92,idem,×2[5]"] =
                "T-032: idem,×2 is CORE.UNPARSED, not a CLONE.POLYPLOID_SUBCLONE finding with a fix",
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
