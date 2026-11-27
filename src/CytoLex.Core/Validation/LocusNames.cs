using System.Text.RegularExpressions;

namespace CytoLex.Core.Validation;

/// <summary>
/// What a probe name says about its chromosome (T-101): paints (<c>wcp11</c>, <c>pcp1q</c>, §13.2 ¶1) and D-numbers
/// (<c>D21Z1</c>, <c>D4S96</c>, <c>D4F26</c>: DNA, chromosome, S/Z/F class, number; decided with the user to place
/// all three classes on their chromosome). Any other name has no position in the string.
/// </summary>
internal static partial class LocusNames
{
    /// <summary>The chromosome a paint or D-number names and what follows it in a paint (<c>pcp1q</c> → 1, <c>q</c>); <c>null</c> otherwise.</summary>
    public static (string Chromosome, string After)? Chromosome(string name)
    {
        if (Paint().Match(name) is { Success: true } paint && IsChromosome(paint.Groups["chr"].Value))
        {
            return (paint.Groups["chr"].Value, paint.Groups["rest"].Value);
        }

        return DNumber().Match(name) is { Success: true } d && IsChromosome(d.Groups["chr"].Value) ? (d.Groups["chr"].Value, string.Empty) : null;
    }

    /// <summary>A band written as a locus without its chromosome (<c>q34</c>, <c>p36.3</c>; subtelomeric results).</summary>
    public static bool IsBand(string name) => Band().IsMatch(name);

    /// <summary>A human chromosome name (1–22, X, Y).</summary>
    public static bool IsChromosome(string name) =>
        name is "X" or "Y" || (int.TryParse(name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 22 && name[0] != '0');

    [GeneratedRegex(@"^[wp]cp(?<chr>[1-9][0-9]?|X|Y)(?<rest>[pq][0-9.]*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Paint();

    [GeneratedRegex(@"^D(?<chr>[1-9][0-9]?|X|Y)[ZSF][0-9]+[A-Z]?$", RegexOptions.CultureInvariant)]
    private static partial Regex DNumber();

    [GeneratedRegex(@"^[pq][1-9][0-9]?(\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Band();
}
