using CytoLex.Core.Kb;

namespace CytoLex.Core.Associations;

/// <summary>
/// Band-level tolerance of association rules (T-109): a written band matches a rule band when it is that band or a
/// finer band inside it (<c>22q11.21</c> in <c>22q11.2</c>, <c>9q34.12</c> in <c>9q34</c>). A coarser written band does
/// not match a finer rule band.
/// </summary>
internal sealed class BandNesting(BandSet? bands)
{
    /// <summary>Whether <paramref name="written"/> is <paramref name="rule"/> or nested inside it.</summary>
    public bool Nests(string? written, string rule)
    {
        if (written is null)
        {
            return false;
        }

        if (string.Equals(written, rule, StringComparison.Ordinal))
        {
            return true;
        }

        if (bands?.Find(written) is { } band && bands.Contains(rule))
        {
            // The band data's nesting decides when it knows both names.
            for (var parent = band.Parent; parent is not null; parent = bands.Find(parent)?.Parent)
            {
                if (string.Equals(parent, rule, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        return ByName(written, rule);
    }

    /// <summary>
    /// The nesting by name alone: <c>9q34</c> → <c>9q34.1</c>, <c>9q34.12</c>; <c>22q11.2</c> → <c>22q11.21</c>. A
    /// sub-band adds digits after the dot; a band without a dot gains a dot and digits.
    /// </summary>
    internal static bool ByName(string written, string rule)
    {
        if (written.Length <= rule.Length || !written.StartsWith(rule, StringComparison.Ordinal))
        {
            return false;
        }

        var next = written[rule.Length];
        return rule.Contains('.', StringComparison.Ordinal) ? char.IsAsciiDigit(next) : next == '.';
    }
}
