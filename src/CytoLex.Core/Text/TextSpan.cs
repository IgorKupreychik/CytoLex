namespace CytoLex.Core.Text;

/// <summary>
/// A range of UTF-16 code units in the exact input string (docs/design/core.md §1, §4).
/// </summary>
/// <param name="Start">Offset of the first character.</param>
/// <param name="Length">Number of characters; 0 for an empty span (e.g. an insertion point).</param>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>Offset just past the last character.</summary>
    public int End => Start + Length;

    /// <summary>The span from <paramref name="start"/> (inclusive) to <paramref name="end"/> (exclusive).</summary>
    public static TextSpan FromBounds(int start, int end) => new(start, end - start);

    /// <summary>The text this span covers in <paramref name="text"/>.</summary>
    public string Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Substring(Start, Length);
    }
}
