using System.Globalization;

namespace CytoLex.Core.Translation;

/// <summary>Text helpers shared by the translation renderers: numbers, letter case and English list joining.</summary>
internal static class Phrasing
{
    public static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

    public static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];

    /// <summary>English list: <c>A</c>, <c>A and B</c>, <c>A, B and C</c>.</summary>
    public static string And(List<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };

    /// <summary>English list with a serial comma: <c>A and B</c> (or <c>A, and B</c> when an item has its own "and"), <c>A, B, and C</c>.</summary>
    public static string SerialAnd(List<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        2 => items.Any(i => i.Contains(" and ", StringComparison.Ordinal)) ? items[0] + ", and " + items[1] : items[0] + " and " + items[1],
        _ => string.Join(", ", items.Take(items.Count - 1)) + ", and " + items[^1],
    };
}
