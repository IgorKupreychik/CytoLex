using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

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
                    $"'{joined.Of(input)}': the symbol r is written after the triplet with a space ('{joined.Of(input)[..^1]} r').",
                    Fix: FixProposal.Insert(joined.End - 1, " "));
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
        var lowestIndex = Array.IndexOf(ranks, lowest);
        var lowestName = part.Chromosomes[lowestIndex].Name;
        var list = part.ChromosomeList();
        var rotate = Permute(part, Rotation(ranks.Length, lowestIndex), reverseGroups: false, input);
        switch (part.Head)
        {
            case "t" when ranks[0] != lowest:
                return ranks.Length == 2
                    ? Finding(ChromosomeOrderRule, part, $"In t{list} the sex chromosome or the lowest-numbered chromosome ({lowestName}) is listed first, with its breakpoints.", rotate)
                    : Finding(MultiChromosomeOrderRule, part, $"In t{list} the sex chromosome or the lowest-numbered chromosome ({lowestName}) is listed first; the others follow in the order of the exchange.", rotate);

            case "dic" when !part.Has("psu") && ranks[0] != lowest:
            case "der" or "rob" when ranks.Length == 2 && ranks[0] != lowest:
                return Finding(ChromosomeOrderRule, part, $"In {part.Head}{list} the sex chromosome or the lowest-numbered chromosome ({lowestName}) is listed first, with its breakpoints.", rotate);

            case "ins" when ranks.Length == 2 && !part.IsDetailed && part.Groups is [var first, var second]
                && first.Breakpoints.Length == 2 && second.Breakpoints.Length == 1:
                return Finding(
                    InsertionReceptorFirstRule,
                    part,
                    $"In ins{list} the receiving chromosome ({part.Chromosomes[1].Name}, one breakpoint) is listed first and the donor ({part.Chromosomes[0].Name}) last.",
                    Permute(part, [1, 0], reverseGroups: false, input));

            case "tas" or "trc" when part.Symbols.Length == 1 && Ends(part, ranks) is { } ends && ranks[0] != ends.Min():
                {
                    var end = part.Chromosomes[Array.IndexOf(ranks, ends.Min())].Name;

                    // The chain read from its other end: chromosomes reversed, each group's breakpoints too.
                    return Finding(
                        part.Head == "tas" ? TasFormatRule : TrcFormatRule,
                        part,
                        $"In {part.Head}{list} the end chromosome of the chain that is a sex chromosome or has the lowest number ({end}) is listed first; the others follow in the order they are attached.",
                        ranks[^1] == ends.Min() ? Permute(part, [.. Enumerable.Range(0, ranks.Length).Reverse()], reverseGroups: true, input) : null);
                }

            case "r" when part.Has("dic", "trc") && ranks[0] != lowest:
                return Finding(RingDicTrcOrderRule, part, $"In {string.Join(' ', part.Symbols.Select(s => s.Symbol))}{list} the sex chromosome or the lowest-numbered chromosome ({lowestName}) is listed first.", rotate);

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
                ? Finding(RingMonocentricAsDerRule, r.Part, $"A ring of several chromosomes with one centromere is a derivative: der(n)r{list}, n the chromosome with the centromere.", AsDerivative(r.Part, input))
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
                $"In '{r.Item.Parts[0].Span.Of(input)}{r.Part.Span.Of(input)}' the chromosome that provides the centromere ({own.Name}) is listed first in the ring.",
                Permute(r.Part, Rotation(ranks.Length, Array.IndexOf(ranks, ownRank)), reverseGroups: false, input))
            : null;
    }

    /// <summary>
    /// <c>r(1;3)(p36.1q23;q21q27)</c> → <c>der(1)r(1;3)(p36.1q23;q21q27)</c>: the centromere is in the one segment whose
    /// breakpoints lie on both arms; that chromosome names the derivative and leads the ring. <c>null</c> when that is not
    /// exactly one segment.
    /// </summary>
    private static FixProposal? AsDerivative(AbnormalityPart part, string input)
    {
        if (part.IsDetailed || part.Groups.Length != part.Chromosomes.Length)
        {
            return null;
        }

        var centric = Enumerable.Range(0, part.Groups.Length)
            .Where(i => part.Groups[i].Breakpoints is [{ Arm: "p" }, { Arm: "q" }] or [{ Arm: "q" }, { Arm: "p" }])
            .ToList();
        if (centric is not [var c])
        {
            return null;
        }

        var reorder = c == 0 ? null : Permute(part, Rotation(part.Chromosomes.Length, c), reverseGroups: false, input);
        return new FixProposal([
            new TextEdit(new TextSpan(part.Span.Start, 0), $"der({part.Chromosomes[c].Span.Of(input)})"),
            .. reorder?.Edits ?? [],
        ]);
    }

    /// <summary>The order that starts at <paramref name="first"/> and keeps the cyclic sequence (a rotation).</summary>
    private static int[] Rotation(int length, int first) => [.. Enumerable.Range(0, length).Select(i => (first + i) % length)];

    /// <summary>
    /// Rewrites a part's chromosome list and breakpoint groups in a new order (T-035): <c>t(16;12)(p11.1;q13)</c> →
    /// <c>t(12;16)(q13;p11.1)</c>. With <paramref name="reverseGroups"/> each group's breakpoints are reversed as well (a chain
    /// read from its other end). The chromosomes and groups keep their written text. <c>null</c> when the groups don't
    /// match the chromosomes one to one, or a group to reverse holds more than its breakpoints.
    /// </summary>
    internal static FixProposal? Permute(AbnormalityPart part, int[] order, bool reverseGroups, string input)
    {
        var chromosomes = part.Chromosomes;
        if (chromosomes.Length != order.Length || chromosomes.Length < 2 || order.SequenceEqual(Enumerable.Range(0, order.Length)))
        {
            return null;
        }

        var listSpan = TextSpan.FromBounds(chromosomes[0].Span.Start, chromosomes[^1].Span.End);
        var edits = new List<TextEdit> { new(listSpan, string.Join(";", order.Select(i => chromosomes[i].Span.Of(input)))) };
        if (!part.Groups.IsEmpty)
        {
            if (part.Groups.Length != chromosomes.Length || part.IsDetailed)
            {
                return null;
            }

            var texts = new List<string>();
            foreach (var g in order.Select(i => part.Groups[i]))
            {
                if (!reverseGroups || g.Breakpoints.Length < 2)
                {
                    texts.Add(g.Span.Of(input));
                    continue;
                }

                var joined = string.Concat(g.Breakpoints.Select(b => b.Span.Of(input)));
                if (joined != g.Span.Of(input))
                {
                    return null;
                }

                texts.Add(string.Concat(g.Breakpoints.Reverse().Select(b => b.Span.Of(input))));
            }

            edits.Add(new TextEdit(TextSpan.FromBounds(part.Groups[0].Span.Start, part.Groups[^1].Span.End), string.Join(";", texts)));
        }

        return new FixProposal([.. edits]);
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

    private static StructuralFinding Finding(string rule, AbnormalityPart part, string detail, FixProposal? fix = null) =>
        new(rule, part.HeadSpan(), detail, Fix: fix);
}
