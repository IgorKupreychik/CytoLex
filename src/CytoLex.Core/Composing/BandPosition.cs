using System.Collections.Immutable;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Composing;

/// <summary>
/// The position of a band along its chromosome, from pter to qter, read from the band designation alone (§2.3.2: regions
/// and bands are numbered outward from the centromere, so <c>p23</c> is distal to <c>p13</c> and <c>q21.3</c> to <c>q21.1</c>).
/// Band coordinates are not needed. Bands of which one contains the other (<c>q21</c>, <c>q21.1</c>) cannot be ordered.
/// </summary>
internal readonly record struct BandPosition(int Arm, ImmutableArray<int> Digits)
{
    /// <summary>−1 = p arm, 0 = centromere, +1 = q arm.</summary>
    private const int _p = -1;

    /// <summary>The position of a certain band or landmark (<c>pter</c>, <c>cen</c>, <c>qter</c>); <c>null</c> otherwise.</summary>
    public static BandPosition? Of(Breakpoint bp)
    {
        if (bp.IsQuestioned || bp.RangeEnd is not null || !bp.Alternatives.IsEmpty)
        {
            return null;
        }

        return Of(bp.Landmark, bp.Arm, bp.BandNumber);
    }

    /// <summary>
    /// The position of a landmark (<c>pter</c>, <c>qter</c>, <c>cen</c>) or of a band given by its arm and digits;
    /// <c>null</c> for other landmarks and malformed bands.
    /// </summary>
    public static BandPosition? Of(string? landmark, string? arm, string? bandNumber)
    {
        switch (landmark)
        {
            case "pter":
                return new BandPosition(_p, [int.MaxValue]);
            case "qter":
                return new BandPosition(1, [int.MaxValue]);
            case "cen":
                return new BandPosition(0, []);
            case not null:
                return null;
        }

        if (arm is not ("p" or "q") || bandNumber is not { Length: > 0 } number || !number.All(c => char.IsAsciiDigit(c) || c == '.'))
        {
            return null;
        }

        return new BandPosition(arm == "p" ? _p : 1, [.. number.Where(char.IsAsciiDigit).Select(c => c - '0')]);
    }

    /// <summary>The arm: <c>p</c>, <c>q</c>, or <c>null</c> at the centromere.</summary>
    public string? ArmName => Arm switch
    {
        _p => "p",
        1 => "q",
        _ => null,
    };

    /// <summary>
    /// Compares the distance from the centromere on the same arm: &lt; 0 when this band is proximal to
    /// <paramref name="other"/>; <c>null</c> when they lie on different arms or one contains the other.
    /// </summary>
    public int? CompareDistance(BandPosition other)
    {
        if (Arm != other.Arm || Arm == 0)
        {
            return null;
        }

        for (var i = 0; i < Math.Min(Digits.Length, other.Digits.Length); i++)
        {
            if (Digits[i] != other.Digits[i])
            {
                return Digits[i].CompareTo(other.Digits[i]);
            }
        }

        return null;
    }

    /// <summary>Compares positions from pter to qter: &lt; 0 when this band lies nearer pter; <c>null</c> when they cannot be ordered.</summary>
    public int? CompareAlong(BandPosition other)
    {
        if (Arm != other.Arm)
        {
            return Arm.CompareTo(other.Arm);
        }

        return CompareDistance(other) is { } d ? (Arm == _p ? -d : d) : (Arm == 0 ? 0 : null);
    }
}
