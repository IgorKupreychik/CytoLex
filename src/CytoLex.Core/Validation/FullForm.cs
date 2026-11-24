using System.Globalization;
using CytoLex.Core.Rules.Checks;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>
/// Writes a cell line with an <c>idem</c>/<c>sl</c>/<c>sdl</c> reference out in full (T-070): the count (and ploidy) as
/// written, the sex chromosomes of its <see cref="ExpandedLine"/>, its inherited and own changes in §6 order
/// (KARYO.ABNORMALITY_ORDER), <c>…</c> when changes are omitted, and the cell count as written. More copies than written
/// go after a rearranged chromosome as <c>×n</c> (KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY), a whole-chromosome change is
/// repeated (<c>+8,+8</c>) and a counted marker gets its count multiplied (<c>+6mar</c>).
/// </summary>
internal static class FullForm
{
    /// <summary>The full form of <paramref name="line"/>; <c>null</c> without a resolved reference, for a partial line or without a count.</summary>
    public static string? Of(string input, ExpandedLine line)
    {
        var cellLine = line.Line;
        if (cellLine.BasedOn is null || line.IsPartial || cellLine.Count is not { IsMissing: false } count)
        {
            return null;
        }

        var head = count.Span.Of(input) + (cellLine.Ploidy?.Span.Of(input) ?? string.Empty);
        var parts = new List<string> { head };
        if (!line.Sex.IsEmpty)
        {
            parts.Add(string.Concat(line.Sex) + (line.SexMark?.Span.Of(input) ?? string.Empty));
        }

        parts.AddRange(ItemOrder.Sort(line.Changes, c => c.Item).Select(c => Piece(input, c)));
        if (line.HasOmitted)
        {
            parts.Add("…");
        }

        return string.Join(",", parts) + (cellLine.CellCount?.Span.Of(input) ?? string.Empty);
    }

    /// <summary>How <paramref name="change"/> is written with its copies.</summary>
    public static string Piece(string input, ExpandedChange change)
    {
        var item = change.Item;
        var text = item.Span.Of(input);
        var written = item is SuffixedItem { Copies.Count: { } k } && k > 0 ? k : 1;
        if (change.Copies == written)
        {
            return text;
        }

        switch (item)
        {
            case Abnormality { LeadingCount: { IsRange: false, IsMissing: false, From: { } lead } leading, Copies: null }:
                return Splice(input, item.Span, leading.Span, (lead * change.Copies).ToString(CultureInfo.InvariantCulture));
            case Abnormality or ChromosomeRegion:
                var suffixed = (SuffixedItem)item;
                if (suffixed.Copies is { } copies)
                {
                    return Splice(input, item.Span, copies.Span, change.Copies == 1 ? string.Empty : Times(change.Copies));
                }

                var at = new[] { suffixed.Constitutional?.Span.Start, suffixed.Inheritance?.Span.Start, item.Span.End }.OfType<int>().Min();
                return Splice(input, item.Span, new TextSpan(at, 0), Times(change.Copies));
            default:
                return string.Join(",", Enumerable.Repeat(text, change.Copies));
        }
    }

    private static string Times(int n) => "×" + n.ToString(CultureInfo.InvariantCulture);

    /// <summary>The text of <paramref name="outer"/> with <paramref name="inner"/> replaced by <paramref name="replacement"/>.</summary>
    private static string Splice(string input, TextSpan outer, TextSpan inner, string replacement) =>
        input[outer.Start..inner.Start] + replacement + input[inner.End..outer.End];
}
