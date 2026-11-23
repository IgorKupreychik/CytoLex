using System.Collections.Immutable;
using CytoLex.Core.Text;

namespace CytoLex.Core.Segmentation;

/// <summary>
/// Splits a notation string into technique segments and detects each segment's type
/// (docs/design/core.md §3.1–§3.2). Purely lexical and edition-independent; it never fails.
/// </summary>
public static class Segmenter
{
    /// <summary>
    /// Technique keywords, longest first. Each word is a ch. 3 symbol (ISCN 2016 §3, pp.34–36:
    /// <c>ish</c>, <c>nuc</c>, <c>fib</c>, <c>rev</c>, <c>cgh</c>, <c>arr</c>, <c>rsa</c>, <c>seq</c>); a test checks them
    /// against the embedded vocab. Two-word keywords accept any (or no) spaces or tabs between the words,
    /// so <c>nucish</c> is still detected; spacing is checked by the parser's rules. Chromosomal CGH is
    /// printed <c>ish cgh</c> (§13.6, p.113), so that pair is a cgh segment, not an ish one.
    /// </summary>
    internal static readonly ImmutableArray<(ImmutableArray<string> Words, NotationType Type)> Keywords =
    [
        (["nuc", "ish"], NotationType.NucIsh),
        (["fib", "ish"], NotationType.FibIsh),
        (["rev", "ish"], NotationType.RevIsh),
        (["ish", "cgh"], NotationType.Cgh),
        (["ish"], NotationType.Ish),
        (["cgh"], NotationType.Cgh),
        (["arr"], NotationType.Arr),
        (["rsa"], NotationType.Rsa),
        (["seq"], NotationType.Seq),
    ];

    /// <summary>
    /// Splits <paramref name="input"/> at its separator periods. A period separates segments when it is
    /// outside every bracket (<c>( )</c>, <c>[ ]</c>, <c>{ }</c>) and the next non-whitespace character is a
    /// letter: <c>46,XX.ish …</c>, <c>…×1.rsa …</c>, <c>…[10/30].arr …</c>. That leaves sub-band points
    /// (<c>9q34.11</c>), decimals (<c>[0.6]</c>), accession versions (<c>NC_000008.10</c>) and HGVS prefixes
    /// (<c>g.1287…</c>, <c>g.[…]</c>) inside their segment.
    /// </summary>
    /// <returns>At least one segment; an empty input gives one empty karyotype segment.</returns>
    public static ImmutableArray<Segment> Split(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var segments = ImmutableArray.CreateBuilder<Segment>();
        var depth = 0;
        var start = 0;
        TextSpan? separator = null;

        for (var i = 0; i < input.Length; i++)
        {
            switch (input[i])
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    depth = Math.Max(0, depth - 1);
                    break;
                case '.' when depth == 0 && NextNonWhitespaceIsLetter(input, i + 1):
                    segments.Add(MakeSegment(input, segments.Count, TextSpan.FromBounds(start, i), separator));
                    separator = new TextSpan(i, 1);
                    start = i + 1;
                    break;
            }
        }

        segments.Add(MakeSegment(input, segments.Count, TextSpan.FromBounds(start, input.Length), separator));
        return segments.ToImmutable();
    }

    private static bool NextNonWhitespaceIsLetter(string input, int from)
    {
        for (var i = from; i < input.Length; i++)
        {
            if (!char.IsWhiteSpace(input[i]))
            {
                return char.IsLetter(input[i]);
            }
        }

        return false;
    }

    private static Segment MakeSegment(string input, int index, TextSpan span, TextSpan? separator)
    {
        var pos = span.Start;
        pos = SkipWhitespace(input, pos, span.End);

        // A donor-only chimera result puts // before the technique symbols (ISCN 2016 §13.3.1 E19:
        // //nuc ish(DXZ1,DYZ3)×1[400]); the keyword follows it.
        if (span.End - pos >= 2 && input[pos] == '/' && input[pos + 1] == '/')
        {
            var afterSlants = SkipWhitespace(input, pos + 2, span.End);
            if (Keywords.Any(k => MatchKeyword(input, afterSlants, span.End, k.Words) is not null))
            {
                pos = afterSlants;
            }
        }

        foreach (var (words, type) in Keywords)
        {
            if (MatchKeyword(input, pos, span.End, words) is { } end)
            {
                return new Segment(
                    index,
                    span,
                    type,
                    TextSpan.FromBounds(pos, end),
                    [.. words.Select(w => "vocab:symbols/" + w)],
                    separator);
            }
        }

        return new Segment(index, span, NotationType.Karyotype, null, [], separator);
    }

    private static int SkipWhitespace(string input, int pos, int limit)
    {
        while (pos < limit && char.IsWhiteSpace(input[pos]))
        {
            pos++;
        }

        return pos;
    }

    /// <summary>End offset of <paramref name="words"/> at <paramref name="pos"/>, or <c>null</c>. The keyword must not be followed by a letter.</summary>
    private static int? MatchKeyword(string input, int pos, int limit, ImmutableArray<string> words)
    {
        for (var w = 0; w < words.Length; w++)
        {
            if (w > 0)
            {
                while (pos < limit && input[pos] is ' ' or '\t')
                {
                    pos++;
                }
            }

            var word = words[w];
            if (limit - pos < word.Length || string.CompareOrdinal(input, pos, word, 0, word.Length) != 0)
            {
                return null;
            }

            pos += word.Length;
        }

        return pos < limit && char.IsLetter(input[pos]) ? null : pos;
    }
}
