using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// The order of the chromosomes in a rearrangement's chromosome list (§4.1 ¶3, §4.3.1.2, §9.2.15, §9.2.16, §9.2.18).
/// Rank: X &lt; Y &lt; 1 … 22. Parts with an unknown (<c>?</c>) or invalid chromosome are skipped. Each fault goes to one rule:
/// <list type="bullet">
/// <item><c>t</c>, <c>dic</c> (not <c>psu dic</c>, §9.2.4 ¶3), <c>der(a;b)</c>, <c>rob</c> of two chromosomes: the lowest first
/// (<c>STRUCT.CHROMOSOME_ORDER</c>).</item>
/// <item><c>t</c> of three or more: the lowest first; the others follow the exchange, so a rotation keeps the meaning and
/// only the first is checked (<c>STRUCT.MULTI_CHROMOSOME_ORDER</c>).</item>
/// <item><c>ins(a;b)</c>: the receiving chromosome (one breakpoint) first, the donor (two) last
/// (<c>STRUCT.INSERTION_RECEPTOR_FIRST</c>).</item>
/// <item><c>tas</c>/<c>trc</c>: the lower of the two end chromosomes of the chain first; an end chromosome has one breakpoint, and
/// in a two-chromosome <c>tas</c> both are ends (<c>TAS.FORMAT</c>, <c>TRC.FORMAT</c>).</item>
/// <item><c>dic r</c>/<c>trc r</c>: the lowest first, and the triplet and <c>r</c> written apart (<c>RING.DIC_TRC_ORDER</c>).</item>
/// <item>A monocentric ring of several chromosomes is a derivative, <c>der(n)r(n;…)</c> (<c>RING.MONOCENTRIC_AS_DER</c>). Inside
/// <c>der(n)</c> the ring may also list the lowest-numbered chromosome first, as the book's own §4.4 example
/// <c>der(8)r(1;8;17)</c> does; only an order that is neither is reported.</item>
/// </list>
/// </summary>
internal static class ChromosomeOrder
{
    public const string ChromosomeOrderRule = "STRUCT.CHROMOSOME_ORDER";
    public const string MultiChromosomeOrderRule = "STRUCT.MULTI_CHROMOSOME_ORDER";
    public const string InsertionReceptorFirstRule = "STRUCT.INSERTION_RECEPTOR_FIRST";
    public const string TasFormatRule = "TAS.FORMAT";
    public const string TrcFormatRule = "TRC.FORMAT";
    public const string RingDicTrcOrderRule = "RING.DIC_TRC_ORDER";
    public const string RingMonocentricAsDerRule = "RING.MONOCENTRIC_AS_DER";

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var r in Structure.Parts(model))
        {
            if (Check(r, input) is { } finding)
            {
                yield return finding;
            }

            if (Adjacent(r.Part) is { } joined)
            {
                yield return new StructuralFinding(
                    RingDicTrcOrderRule,
                    joined,
                    $"'{joined.Of(input)}': the symbol r is written after the triplet with a space ('{joined.Of(input)[..^1]} r').");
            }
        }
    }

    private static StructuralFinding? Check(Structure.PartRef r, string input)
    {
        var part = r.Part;
        if (Structure.Ranks(part) is not { Length: >= 2 } ranks)
        {
            return null;
        }

        var lowest = ranks.Min();
        var lowestName = part.Chromosomes[Array.IndexOf(ranks, lowest)].Name;
        var list = part.ChromosomeList();
        switch (part.Head)
        {
            case "t" when ranks[0] != lowest:
                return ranks.Length == 2
                    ? Finding(ChromosomeOrderRule, part, $"In t{list} the sex chromosome or the lowest-numbered chromosome ({lowestName}) is listed first, with its breakpoints.")
                    : Finding(MultiChromosomeOrderRule, part, $"In t{list} the sex chromosome or the lowest-numbered chromosome ({lowestName}) is listed first; the others follow in the order of the exchange.");

            case "dic" when !part.Has("psu") && ranks[0] != lowest:
            case "der" or "rob" when ranks.Length == 2 && ranks[0] != lowest:
                return Finding(ChromosomeOrderRule, part, $"In {part.Head}{list} the sex chromosome or the lowest-numbered chromosome ({lowestName}) is listed first, with its breakpoints.");

            case "ins" when ranks.Length == 2 && !part.IsDetailed && part.Groups is [var first, var second]
                && first.Breakpoints.Length == 2 && second.Breakpoints.Length == 1:
                return Finding(
                    InsertionReceptorFirstRule,
                    part,
                    $"In ins{list} the receiving chromosome ({part.Chromosomes[1].Name}, one breakpoint) is listed first and the donor ({part.Chromosomes[0].Name}) last.");

            case "tas" or "trc" when part.Symbols.Length == 1 && Ends(part, ranks) is { } ends && ranks[0] != ends.Min():
                {
                    var end = part.Chromosomes[Array.IndexOf(ranks, ends.Min())].Name;
                    return Finding(
                        part.Head == "tas" ? TasFormatRule : TrcFormatRule,
                        part,
                        $"In {part.Head}{list} the end chromosome of the chain that is a sex chromosome or has the lowest number ({end}) is listed first; the others follow in the order they are attached.");
                }

            case "r" when part.Has("dic", "trc") && ranks[0] != lowest:
                return Finding(RingDicTrcOrderRule, part, $"In {string.Join(' ', part.Symbols.Select(s => s.Symbol))}{list} the sex chromosome or the lowest-numbered chromosome ({lowestName}) is listed first.");

            case "r" when !part.Has("dic", "trc"):
                return MonocentricRing(r, ranks, lowest, input);

            default:
                return null;
        }
    }

    private static StructuralFinding? MonocentricRing(Structure.PartRef r, int[] ranks, int lowest, string input)
    {
        var list = r.Part.ChromosomeList();
        if (!r.InDerivative)
        {
            return r.Index == 0 && r.Item.Parts.Length == 1
                ? Finding(RingMonocentricAsDerRule, r.Part, $"A ring of several chromosomes with one centromere is a derivative: der(n)r{list}, n the chromosome with the centromere.")
                : null;
        }

        if (r.Item.Kind != "der" || r.Item.Parts[0].Chromosomes is not [var own] || Structure.Rank(own) is not { } ownRank)
        {
            return null;
        }

        return ranks[0] != ownRank && ranks[0] != lowest && ranks.Contains(ownRank)
            ? Finding(
                RingMonocentricAsDerRule,
                r.Part,
                $"In '{r.Item.Parts[0].Span.Of(input)}{r.Part.Span.Of(input)}' the chromosome that provides the centromere ({own.Name}) is listed first in the ring.")
            : null;
    }

    /// <summary>The ranks of the two end chromosomes of a tas/trc chain; <c>null</c> when they cannot be told.</summary>
    private static int[]? Ends(AbnormalityPart part, int[] ranks)
    {
        if (part.Head == "tas" && ranks.Length == 2)
        {
            return ranks;
        }

        if (part.IsDetailed || part.Groups.Length != ranks.Length)
        {
            return null;
        }

        var ends = ranks.Where((_, i) => part.Groups[i].Breakpoints.Length == 1).ToArray();
        return ends.Length == 2 ? ends : null;
    }

    /// <summary><c>dicr</c>/<c>trcr</c>: the triplet and the ring symbol written without a space.</summary>
    private static TextSpan? Adjacent(AbnormalityPart part)
    {
        for (var i = 0; i + 1 < part.Symbols.Length; i++)
        {
            var (first, second) = (part.Symbols[i], part.Symbols[i + 1]);
            if (first.Symbol is "dic" or "trc" && second.Symbol == "r" && first.Span.End == second.Span.Start)
            {
                return TextSpan.FromBounds(first.Span.Start, second.Span.End);
            }
        }

        return null;
    }

    private static StructuralFinding Finding(string rule, AbnormalityPart part, string detail) =>
        new(rule, part.HeadSpan(), detail);
}
