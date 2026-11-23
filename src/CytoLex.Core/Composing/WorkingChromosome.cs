using CytoLex.Core.Semantics;

namespace CytoLex.Core.Composing;

/// <summary>
/// A chromosome being rearranged (T-066): oriented pieces, each joined to the previous one either by an original
/// adjacency (two pieces split at a band, merged again by <see cref="ToComposed"/>) or by a break and reunion
/// (<c>::</c>). A band at which the chromosome is split ends one piece and starts the next (<c>pter→q21</c>,
/// <c>q21→qter</c>), as in the detailed system.
/// </summary>
/// <remarks>
/// Positions are compared by <see cref="BandPosition"/>; bands of which one contains the other (<c>q21</c>, <c>q21.1</c>)
/// count as the same place.
/// </remarks>
internal sealed class WorkingChromosome
{
    /// <summary>The pieces; <c>JunctionBefore</c>: joined to the previous piece by a break and reunion (else original adjacency).</summary>
    private readonly List<(ComposedPiece Piece, bool JunctionBefore)> _entries;

    private WorkingChromosome(IEnumerable<(ComposedPiece, bool)> entries) => _entries = [.. entries];

    public JoinKind? Leading { get; set; }

    public JoinKind? Trailing { get; set; }

    public int Count => _entries.Count;

    /// <summary>A normal chromosome, <c>pter→qter</c>.</summary>
    public static WorkingChromosome Normal(ChromosomeId c) => Of([Range(BandPoint.ArmEnd(c, "p"), BandPoint.ArmEnd(c, "q"))]);

    /// <summary>Pieces joined by breaks and reunions.</summary>
    public static WorkingChromosome Of(IEnumerable<ComposedPiece> pieces) => new(pieces.Select(p => (p, true)));

    /// <summary>Pieces with the given links (<c>true</c> = junction), one fewer than the pieces.</summary>
    public static WorkingChromosome Linked(IReadOnlyList<ComposedPiece> pieces, IReadOnlyList<bool> links) =>
        new(pieces.Select((p, i) => (p, i == 0 || links[i - 1])));

    /// <summary>A slice read the other way: pieces in reverse order, each reversed, links reversed.</summary>
    public static (List<ComposedPiece> Pieces, List<bool> Links) Reversed(IReadOnlyList<ComposedPiece> pieces, IReadOnlyList<bool> links) =>
        ([.. pieces.Reverse().Select(p => p.Reversed())], [.. links.Reverse()]);

    public static ComposedPiece Range(BandPoint from, BandPoint to) => new([from, to]);

    public static ComposedPiece Single(PointKind kind) => new([new BandPoint(null, kind)]);

    /// <summary>The unknown chromosome with its centromere, <c>?→cen→?</c> (der(?), §9.2.3).</summary>
    public static ComposedPiece UnknownCentric() =>
        new([new BandPoint(null, PointKind.Unknown), new BandPoint(ChromosomeId.Unknown, PointKind.Cen), new BandPoint(null, PointKind.Unknown)]);

    /// <summary>Whether <paramref name="point"/> lies in one of the pieces.</summary>
    public bool Contains(BandPoint point) => Find(point) is not null;

    /// <summary>Whether a piece is the unknown chromosome with its centromere.</summary>
    public bool HasUnknownCentric => _entries.Any(e => e.Piece.Points.Length == 3 && e.Piece.Points[1].Kind == PointKind.Cen && e.Piece.From.Kind == PointKind.Unknown);

    /// <summary>
    /// Splits the chromosome at <paramref name="point"/> and returns the boundary there (index of the piece after it) and
    /// whether the part distal to the band (toward its arm end) lies to the right; <c>null</c> when the band isn't found.
    /// </summary>
    public (int Boundary, bool DistalRight)? Split(BandPoint point)
    {
        if (Find(point) is not { } found)
        {
            return null;
        }

        var (index, piece, ascending) = found;
        var distalRight = ascending == (point.Position!.Value.Arm > 0);
        if (Cmp(point, piece.From) == 0)
        {
            return (index, distalRight);
        }

        if (Cmp(point, piece.To) == 0)
        {
            return (index + 1, distalRight);
        }

        var junction = _entries[index].JunctionBefore;
        _entries[index] = (Range(piece.From, point), junction);
        _entries.Insert(index + 1, (Range(point, piece.To), false));
        return (index + 1, distalRight);
    }

    /// <summary>Removes everything right (or left) of <paramref name="boundary"/>.</summary>
    public void RemoveSide(int boundary, bool right)
    {
        if (right)
        {
            _entries.RemoveRange(boundary, _entries.Count - boundary);
            Trailing = null;
        }
        else
        {
            _entries.RemoveRange(0, boundary);
            Leading = null;
        }
    }

    /// <summary>Removes the pieces between two boundaries and joins the rest by a break and reunion.</summary>
    public void RemoveBetween(int from, int to)
    {
        (from, to) = (Math.Min(from, to), Math.Max(from, to));
        _entries.RemoveRange(from, to - from);
        MarkJunction(from);
    }

    /// <summary>Inserts <paramref name="pieces"/>, joined to each other and to both sides by breaks and reunions.</summary>
    public void Insert(int boundary, IReadOnlyList<ComposedPiece> pieces) =>
        InsertLinked(boundary, pieces, [.. Enumerable.Repeat(true, Math.Max(0, pieces.Count - 1))]);

    /// <summary>Inserts pieces with the given inner links (<c>true</c> = junction); the outer links as stated.</summary>
    public void InsertLinked(int boundary, IReadOnlyList<ComposedPiece> pieces, IReadOnlyList<bool> innerLinks, bool junctionBefore = true, bool junctionAfter = true)
    {
        _entries.InsertRange(boundary, pieces.Select((p, i) => (p, i == 0 ? junctionBefore : innerLinks[i - 1])));
        var after = boundary + pieces.Count;
        if (after < _entries.Count)
        {
            _entries[after] = (_entries[after].Piece, junctionAfter);
        }
    }

    /// <summary>Reverses the pieces between two boundaries (an inversion); the links at both ends become junctions.</summary>
    public void Reverse(int from, int to)
    {
        (from, to) = (Math.Min(from, to), Math.Max(from, to));
        var old = _entries[from..to];
        for (var j = 0; j < old.Count; j++)
        {
            var source = old[old.Count - 1 - j];
            var junction = j == 0 || old[old.Count - j].JunctionBefore;
            _entries[from + j] = (source.Piece.Reversed(), junction);
        }

        MarkJunction(from);
        MarkJunction(to);
    }

    /// <summary>The pieces between two boundaries, in reading order.</summary>
    public List<ComposedPiece> Slice(int from, int to) => [.. _entries[Math.Min(from, to)..Math.Max(from, to)].Select(e => e.Piece)];

    /// <summary>The links inside <see cref="Slice"/> (<c>true</c> = junction), one fewer than its pieces.</summary>
    public List<bool> SliceLinks(int from, int to) =>
        Math.Min(from, to) + 1 >= Math.Max(from, to) ? [] : [.. _entries[(Math.Min(from, to) + 1)..Math.Max(from, to)].Select(e => e.JunctionBefore)];

    /// <summary>
    /// Finds the reunion between <paramref name="a"/> and <paramref name="b"/> (in either order): the boundary whose
    /// neighbouring points are these two bands; <c>null</c> when there is none.
    /// </summary>
    public int? JunctionBetween(BandPoint a, BandPoint b)
    {
        for (var i = 1; i < _entries.Count; i++)
        {
            var (left, right) = (_entries[i - 1].Piece.To, _entries[i].Piece.From);
            if (_entries[i].JunctionBefore && ((Same(left, a) && Same(right, b)) || (Same(left, b) && Same(right, a))))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>The composition with adjacent pieces merged (<c>pter→q22</c> + <c>q22→q25</c> → <c>pter→q25</c>).</summary>
    public ComposedChromosome ToComposed()
    {
        var pieces = new List<ComposedPiece>();
        var joins = new List<JoinKind>();
        foreach (var (piece, junction) in _entries)
        {
            if (pieces.Count > 0 && !junction && pieces[^1].Points.Length == 2 && piece.Points.Length == 2)
            {
                pieces[^1] = Range(pieces[^1].From, piece.To);
                continue;
            }

            if (pieces.Count > 0)
            {
                joins.Add(JoinKind.Junction);
            }

            pieces.Add(piece);
        }

        return new ComposedChromosome(Leading, [.. pieces], [.. joins], Trailing);
    }

    /// <summary>Compares two points of one chromosome from pter to qter; bands that contain each other are the same place.</summary>
    internal static int Cmp(BandPoint a, BandPoint b) =>
        a.Position is { } pa && b.Position is { } pb ? pa.CompareAlong(pb) ?? 0 : 0;

    private static bool Same(BandPoint a, BandPoint b) =>
        a.Chromosome == b.Chromosome && a.Position is not null && b.Position is not null && Cmp(a, b) == 0;

    /// <summary>Sets the link at a boundary: a break and reunion (<c>true</c>) or the original adjacency.</summary>
    public void SetJunction(int boundary, bool junction)
    {
        if (boundary > 0 && boundary < _entries.Count)
        {
            _entries[boundary] = (_entries[boundary].Piece, junction);
        }
    }

    private void MarkJunction(int boundary) => SetJunction(boundary, true);

    /// <summary>The first range piece of the point's chromosome that contains it, with its orientation.</summary>
    private (int Index, ComposedPiece Piece, bool Ascending)? Find(BandPoint point)
    {
        if (point.Position is null || point.Chromosome is not { } c)
        {
            return null;
        }

        for (var i = 0; i < _entries.Count; i++)
        {
            var piece = _entries[i].Piece;
            if (piece.Points.Length != 2 || piece.From.Chromosome != c || piece.To.Chromosome != c || piece.From.Position is null || piece.To.Position is null)
            {
                continue;
            }

            var ascending = Cmp(piece.From, piece.To) <= 0;
            var (low, high) = ascending ? (piece.From, piece.To) : (piece.To, piece.From);
            if (Cmp(point, low) >= 0 && Cmp(point, high) <= 0)
            {
                return (i, piece, ascending);
            }
        }

        return null;
    }
}
