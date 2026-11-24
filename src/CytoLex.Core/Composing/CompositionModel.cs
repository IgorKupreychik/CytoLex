using System.Collections.Immutable;
using System.Text;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Composing;

/// <summary>A chromosome of a band composition: <c>1</c>–<c>22</c>, <c>X</c>, <c>Y</c> or <c>?</c>, maybe underlined (homologue distinction).</summary>
/// <param name="Name">The identifier without underline marks, or <c>?</c>.</param>
/// <param name="IsUnderlined">Written underlined (<c>9̲</c>).</param>
public readonly record struct ChromosomeId(string Name, bool IsUnderlined = false)
{
    /// <summary>The unknown chromosome <c>?</c>.</summary>
    public static ChromosomeId Unknown { get; } = new("?");

    /// <summary><c>?</c>.</summary>
    public bool IsUnknown => Name == "?";

    /// <summary>The identifier of a bound chromosome.</summary>
    public static ChromosomeId Of(ChromosomeRef c) => new(c.Name, c.IsUnderlined);

    /// <summary>As printed: <c>9</c>, <c>9̲</c> (U+0332 after each character), <c>?</c>.</summary>
    public override string ToString() => IsUnderlined ? string.Concat(Name.Select(c => c + "̲")) : Name;
}

/// <summary>What a <see cref="BandPoint"/> is.</summary>
public enum PointKind
{
    /// <summary>A band or region: <c>q13</c>, <c>p10</c>.</summary>
    Band,

    /// <summary><c>pter</c>: the end of the short arm.</summary>
    Pter,

    /// <summary><c>qter</c>: the end of the long arm.</summary>
    Qter,

    /// <summary><c>cen</c>: the centromere.</summary>
    Cen,

    /// <summary><c>hsr</c>: a homogeneously staining region (inserted material).</summary>
    Hsr,

    /// <summary><c>?</c>: material of unknown origin.</summary>
    Unknown,
}

/// <summary>
/// One point of a band composition (§4.3.2): a band, an arm end, the centromere, <c>hsr</c> or <c>?</c>. Two points are
/// equal when they name the same place, however the input wrote them (<c>q13</c> in a single-chromosome composition
/// equals <c>5q13</c>).
/// </summary>
/// <param name="Chromosome">The chromosome; <c>null</c> for <c>hsr</c> and <c>?</c> material.</param>
/// <param name="Kind">What the point is.</param>
/// <param name="Arm"><c>p</c> or <c>q</c> for a band.</param>
/// <param name="Band">Region, band and sub-band digits of a band (<c>13.1</c>).</param>
public sealed record BandPoint(ChromosomeId? Chromosome, PointKind Kind, string? Arm = null, string? Band = null)
{
    /// <summary>The place along the chromosome (<see cref="BandPosition"/>); <c>null</c> for <c>hsr</c> and <c>?</c>.</summary>
    internal BandPosition? Position => Kind switch
    {
        PointKind.Band => BandPosition.Of(null, Arm, Band),
        PointKind.Pter => BandPosition.Of("pter", null, null),
        PointKind.Qter => BandPosition.Of("qter", null, null),
        PointKind.Cen => BandPosition.Of("cen", null, null),
        _ => null,
    };

    /// <summary>An arm end (<c>pter</c>, <c>qter</c>).</summary>
    public bool IsTerminal => Kind is PointKind.Pter or PointKind.Qter;

    /// <summary>A band of chromosome <paramref name="c"/>.</summary>
    public static BandPoint OfBand(ChromosomeId c, string arm, string band) => new(c, PointKind.Band, arm, band);

    /// <summary>The arm end of <paramref name="c"/> on <paramref name="arm"/> (<c>p</c> → pter).</summary>
    public static BandPoint ArmEnd(ChromosomeId c, string arm) => new(c, arm == "p" ? PointKind.Pter : PointKind.Qter);

    /// <summary>A point of a bound breakpoint; <c>null</c> when it is uncertain (<c>q2?3</c>, <c>~</c>, <c>or</c>) or not a place (<c>neo</c>, <c>ter</c>).</summary>
    public static BandPoint? Of(Breakpoint b)
    {
        if (b.RangeEnd is not null || !b.Alternatives.IsEmpty)
        {
            return null;
        }

        var c = b.Chromosome is { } chromosome ? ChromosomeId.Of(chromosome) : (ChromosomeId?)null;
        return b.Landmark switch
        {
            "pter" => new BandPoint(c, PointKind.Pter),
            "qter" => new BandPoint(c, PointKind.Qter),
            "cen" => new BandPoint(c, PointKind.Cen),
            "hsr" => new BandPoint(null, PointKind.Hsr),
            "?" => new BandPoint(null, PointKind.Unknown),
            null when !b.IsQuestioned && b.Arm is ("p" or "q") && b.BandNumber is { } band => new BandPoint(c, PointKind.Band, b.Arm, band),
            _ => null,
        };
    }

    /// <summary>As written in a band composition, with or without the chromosome number (§4.3.2.2).</summary>
    public string Format(bool withChromosome)
    {
        var prefix = withChromosome && Chromosome is { IsUnknown: false } c ? c.ToString() : string.Empty;
        return Kind switch
        {
            PointKind.Band => prefix + Arm + Band,
            PointKind.Pter => prefix + "pter",
            PointKind.Qter => prefix + "qter",
            PointKind.Cen => prefix + "cen",
            PointKind.Hsr => "hsr",
            _ => "?",
        };
    }

    public override string ToString() => Format(true);
}

/// <summary>One piece of a band composition: points joined by <c>→</c> in the written orientation, or a single point (<c>hsr</c>, <c>?</c>).</summary>
public sealed record ComposedPiece(ImmutableArray<BandPoint> Points)
{
    public BandPoint From => Points[0];

    public BandPoint To => Points[^1];

    /// <summary>The piece read the other way (<c>q21→p13</c> for <c>p13→q21</c>).</summary>
    public ComposedPiece Reversed() => new([.. Points.Reverse()]);

    public bool Equals(ComposedPiece? other) => other is not null && Points.SequenceEqual(other.Points);

    public override int GetHashCode() => Points.Aggregate(0, (h, p) => HashCode.Combine(h, p));

    public override string ToString() => string.Join("→", Points);
}

/// <summary>
/// The band composition of one abnormal chromosome (§4.3.2): pieces joined by <c>::</c> (break and reunion), with an
/// optional <c>:</c> (break) or <c>::</c> (ring) before the first and after the last piece. Value equality: same pieces,
/// joins and ends.
/// </summary>
public sealed record ComposedChromosome(JoinKind? LeadingJoin, ImmutableArray<ComposedPiece> Pieces, ImmutableArray<JoinKind> Joins, JoinKind? TrailingJoin)
{
    /// <summary>A ring: <c>::…::</c>.</summary>
    public bool IsRing => LeadingJoin == JoinKind.Junction && TrailingJoin == JoinKind.Junction;

    /// <summary>The composition of a band composition as written (detailed system); <c>null</c> when a point is uncertain.</summary>
    public static ComposedChromosome? FromWritten(DerivativeStructure s)
    {
        var pieces = ImmutableArray.CreateBuilder<ComposedPiece>();
        foreach (var piece in s.Pieces)
        {
            var points = piece.Points.Select(BandPoint.Of).ToList();
            if (points.Any(p => p is null))
            {
                return null;
            }

            pieces.Add(new ComposedPiece([.. points.OfType<BandPoint>()]));
        }

        return new ComposedChromosome(s.LeadingJoin, pieces.ToImmutable(), s.Joins, s.TrailingJoin);
    }

    /// <summary>Read from the other end: pieces and their points reversed, ends swapped.</summary>
    public ComposedChromosome Reversed() =>
        new(TrailingJoin, [.. Pieces.Reverse().Select(p => p.Reversed())], [.. Joins.Reverse()], LeadingJoin);

    /// <summary>The composition as written in the detailed system: <c>3qter→3q13.1::1p22→1qter</c>.</summary>
    public string Format(bool withChromosomes)
    {
        var sb = new StringBuilder();
        Join(sb, LeadingJoin);
        for (var i = 0; i < Pieces.Length; i++)
        {
            if (i > 0)
            {
                Join(sb, Joins[i - 1]);
            }

            sb.Append(string.Join('→', Pieces[i].Points.Select(p => p.Format(withChromosomes))));
        }

        Join(sb, TrailingJoin);
        return sb.ToString();
    }

    public bool Equals(ComposedChromosome? other) =>
        other is not null
        && LeadingJoin == other.LeadingJoin
        && TrailingJoin == other.TrailingJoin
        && Pieces.SequenceEqual(other.Pieces)
        && Joins.SequenceEqual(other.Joins);

    public override int GetHashCode() => HashCode.Combine(LeadingJoin, TrailingJoin, Pieces.Length, Pieces.IsEmpty ? null : Pieces[0]);

    public override string ToString() => Format(true);

    private static void Join(StringBuilder sb, JoinKind? join)
    {
        if (join is { } j)
        {
            sb.Append(j == JoinKind.Break ? ":" : "::");
        }
    }
}

/// <summary>Where an <see cref="AbnormalityComposition"/> comes from.</summary>
public enum CompositionSource
{
    /// <summary>Computed from the short system (T-066).</summary>
    Computed,

    /// <summary>Written in the detailed system.</summary>
    Written,
}

/// <summary>Why an abnormality has no <see cref="AbnormalityComposition"/> (T-086).</summary>
public enum CompositionGapKind
{
    /// <summary>The symbol has no band composition (<c>mar</c>, <c>dmin</c>, <c>fra</c>, <c>idem</c>, …).</summary>
    NoComposition,

    /// <summary>A partial abnormality (<c>der(1)t(1;3)</c> without the rest, a book fragment).</summary>
    Partial,

    /// <summary>A breakpoint is uncertain (<c>q2?3</c>, <c>~</c>, <c>or</c>) or missing, or a chromosome is not given.</summary>
    Uncertain,

    /// <summary>The type or shape has no defined composition yet (insertion recombinants, T-087).</summary>
    Unsupported,

    /// <summary>
    /// An aberration of a derivative names a band the chromosome being built no longer has after the earlier
    /// aberrations: the short system can't describe it (<c>DER.COMPLEX_USE_DETAILED</c>, §9.2.3 ¶15).
    /// </summary>
    Unplaceable,
}

/// <summary>Why an abnormality has no composition, with the part (index into <c>Abnormality.Parts</c>) that could not be placed.</summary>
/// <param name="Kind">The reason.</param>
/// <param name="Part">For <see cref="CompositionGapKind.Unplaceable"/>: the aberration that names the missing band.</param>
public sealed record CompositionGap(CompositionGapKind Kind, int? Part = null);

/// <summary>
/// The band composition of an abnormality (T-066): one <see cref="ComposedChromosome"/> per abnormal chromosome it
/// describes (<c>t(2;5)</c> → der(2) and der(5), in the order of the chromosome list; <c>ins(5;2)</c> → two; others one).
/// </summary>
/// <param name="Products">The abnormal chromosomes.</param>
/// <param name="Source">Computed from the short system or read from the detailed system.</param>
/// <param name="Detailed">The abnormality in the detailed system: <c>der(9)(9pter→9q34::22q11.2→22qter)</c>.</param>
/// <param name="Kb">The KB rules that define the composition (only those the edition has).</param>
public sealed record AbnormalityComposition(ImmutableArray<ComposedChromosome> Products, CompositionSource Source, string Detailed, ImmutableArray<KbRef> Kb)
{
    public bool Equals(AbnormalityComposition? other) =>
        other is not null && Source == other.Source && Detailed == other.Detailed && Products.SequenceEqual(other.Products) && Kb.SequenceEqual(other.Kb);

    public override int GetHashCode() => HashCode.Combine(Source, Detailed);
}
