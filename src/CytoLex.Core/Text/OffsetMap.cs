using System.Collections.Immutable;

namespace CytoLex.Core.Text;

/// <summary>
/// One piece of a text derived from an original by edits (<see cref="OffsetMap"/>): either a run copied unchanged from
/// the original, or text written by an edit in place of an original span.
/// </summary>
/// <param name="Length">The piece's length in the derived text.</param>
/// <param name="Original">The original text it comes from: the copied run, or the span the edit replaced (empty for an insertion).</param>
/// <param name="IsCopy">Copied unchanged (<paramref name="Length"/> = <c>Original.Length</c>); <c>false</c> = written by an edit.</param>
public sealed record OffsetPiece(int Length, TextSpan Original, bool IsCopy);

/// <summary>
/// Maps offsets of a text derived from an original by edits back to the original (T-096: autocorrection keeps the
/// original, and spans of the corrected text are shown on it). The derived text is a sequence of pieces; edits of
/// several rounds compose (<see cref="Apply"/> on the map of the previous round). A position inside text an edit wrote
/// maps to the bounds of the span that edit replaced.
/// </summary>
/// <param name="OriginalLength">The length of the original text.</param>
/// <param name="Pieces">The derived text, piece by piece, in order; no empty pieces.</param>
public sealed record OffsetMap(int OriginalLength, ImmutableArray<OffsetPiece> Pieces)
{
    /// <summary>The length of the derived text.</summary>
    public int Length => Pieces.Sum(p => p.Length);

    /// <summary>The map of an unchanged text of <paramref name="length"/> characters.</summary>
    public static OffsetMap Identity(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        return new OffsetMap(length, length == 0 ? [] : [new OffsetPiece(length, new TextSpan(0, length), true)]);
    }

    /// <summary>
    /// The map of the text after <paramref name="edits"/> (against the derived text this map describes) are applied,
    /// still pointing into the original.
    /// </summary>
    /// <exception cref="ArgumentException">An edit lies outside the derived text, or two edits overlap.</exception>
    public OffsetMap Apply(IEnumerable<(TextSpan Span, string NewText)> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var ordered = edits.OrderBy(e => e.Span.Start).ThenBy(e => e.Span.Length).ToList();
        var length = Length;
        var pieces = ImmutableArray.CreateBuilder<OffsetPiece>();
        var position = 0;
        foreach (var (span, newText) in ordered)
        {
            if (span.Start < position || span.End > length)
            {
                throw new ArgumentException($"Edit {span} overlaps an earlier edit or lies outside the text (length {length}).", nameof(edits));
            }

            AddSlice(pieces, position, span.Start);
            if (newText.Length > 0)
            {
                pieces.Add(new OffsetPiece(newText.Length, ToOriginal(span), false));
            }

            position = span.End;
        }

        AddSlice(pieces, position, length);
        return new OffsetMap(OriginalLength, pieces.ToImmutable());
    }

    /// <summary>The original span <paramref name="span"/> of the derived text comes from (an empty span stays empty).</summary>
    public TextSpan ToOriginal(TextSpan span)
    {
        var start = StartToOriginal(span.Start);
        return span.Length == 0 ? new TextSpan(start, 0) : TextSpan.FromBounds(start, Math.Max(start, EndToOriginal(span.End)));
    }

    /// <summary>The original offset of the derived offset <paramref name="offset"/> (as the start of a span).</summary>
    public int ToOriginal(int offset) => StartToOriginal(offset);

    private int StartToOriginal(int offset)
    {
        var pieceStart = 0;
        foreach (var p in Pieces)
        {
            if (offset < pieceStart + p.Length)
            {
                return p.IsCopy ? p.Original.Start + (offset - pieceStart) : p.Original.Start;
            }

            pieceStart += p.Length;
        }

        return OriginalLength;
    }

    private int EndToOriginal(int offset)
    {
        var pieceStart = 0;
        foreach (var p in Pieces)
        {
            if (offset > pieceStart && offset <= pieceStart + p.Length)
            {
                return p.IsCopy ? p.Original.Start + (offset - pieceStart) : p.Original.End;
            }

            pieceStart += p.Length;
        }

        return OriginalLength;
    }

    /// <summary>Adds the pieces covering [<paramref name="from"/>, <paramref name="to"/>) of the derived text.</summary>
    private void AddSlice(ImmutableArray<OffsetPiece>.Builder target, int from, int to)
    {
        var pieceStart = 0;
        foreach (var p in Pieces)
        {
            var pieceEnd = pieceStart + p.Length;
            var a = Math.Max(from, pieceStart);
            var b = Math.Min(to, pieceEnd);
            if (a < b)
            {
                target.Add(p.IsCopy
                    ? new OffsetPiece(b - a, new TextSpan(p.Original.Start + (a - pieceStart), b - a), true)
                    : p with { Length = b - a });
            }

            pieceStart = pieceEnd;
        }
    }
}
