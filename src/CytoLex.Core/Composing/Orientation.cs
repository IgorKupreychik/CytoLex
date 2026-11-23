using CytoLex.Core.Semantics;

namespace CytoLex.Core.Composing;

/// <summary>
/// Which end a band composition starts from (<c>STRUCT.COMPOSITION_ORDER</c>, §4.3.2.2 ¶1–2): the end of the short arm;
/// if neither end is a short-arm end, the end attached to the proximal short arm (when one centromere shows it), else
/// the long-arm end of the lowest-numbered chromosome.
/// </summary>
/// <remarks>
/// Ties the rule leaves open are settled as the book's examples do: both ends short-arm ends → the end of the chromosome
/// with the (first written) centromere (<c>dic(13;15)</c>, <c>psu dic(15;13)</c>, der(5) of <c>t(2;5)(p12;q31)</c>);
/// the same chromosome at both ends → an arm end before a break (<c>fis(10)(q10)</c> → <c>qter→q10:</c>); an unknown
/// chromosome at an end → as built. Rings and telomeric associations keep the written order.
/// </remarks>
internal static class Orientation
{
    public const string Rule = "STRUCT.COMPOSITION_ORDER";

    /// <summary><paramref name="c"/> read from the end the rule names. <paramref name="centromere"/>: the chromosome whose centromere the derivative has.</summary>
    public static ComposedChromosome Orient(ComposedChromosome c, ChromosomeId? centromere)
    {
        if (c.Pieces.IsEmpty || c.IsRing)
        {
            return c;
        }

        var (left, right) = (c.Pieces[0].From, c.Pieces[^1].To);
        var leftShort = left.Kind == PointKind.Pter && c.LeadingJoin is null;
        var rightShort = right.Kind == PointKind.Pter && c.TrailingJoin is null;
        if (leftShort != rightShort)
        {
            return rightShort ? c.Reversed() : c;
        }

        if (leftShort)
        {
            return centromere is { } cen && right.Chromosome == cen && left.Chromosome != cen ? c.Reversed() : c;
        }

        if (ProximalShortArmSide(c) is { } side)
        {
            return side ? c.Reversed() : c;
        }

        return LowestFirst(c, left, right);
    }

    /// <summary>
    /// When exactly one piece holds a centromere and part of that chromosome's short arm: whether that short arm lies to
    /// the right (<c>true</c>) or left (<c>false</c>); <c>null</c> otherwise.
    /// </summary>
    private static bool? ProximalShortArmSide(ComposedChromosome c)
    {
        var centric = c.Pieces.Where(p => p.Points.Length == 2 && p.From.Position is { } f && p.To.Position is { } t && Math.Sign(f.Arm) != Math.Sign(t.Arm)).ToList();
        if (centric is not [var piece])
        {
            return null;
        }

        if (piece.From.Position!.Value.Arm < 0)
        {
            return false;
        }

        return piece.To.Position!.Value.Arm < 0 ? true : null;
    }

    private static ComposedChromosome LowestFirst(ComposedChromosome c, BandPoint left, BandPoint right)
    {
        if (left.Chromosome is not { IsUnknown: false } l || right.Chromosome is not { IsUnknown: false } r)
        {
            return c;
        }

        var order = Rank(l).CompareTo(Rank(r));
        if (order != 0)
        {
            return order > 0 ? c.Reversed() : c;
        }

        var leftEnd = left.IsTerminal && c.LeadingJoin is null;
        var rightEnd = right.IsTerminal && c.TrailingJoin is null;
        return rightEnd && !leftEnd ? c.Reversed() : c;
    }

    /// <summary>Sex chromosomes first, then autosomes by number.</summary>
    private static int Rank(ChromosomeId c) => c.Name switch
    {
        "X" => -2,
        "Y" => -1,
        _ => int.TryParse(c.Name, out var n) ? n : int.MaxValue,
    };
}
