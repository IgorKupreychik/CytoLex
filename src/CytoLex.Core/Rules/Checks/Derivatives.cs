using CytoLex.Core.Semantics;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// Derivative and recombinant chromosomes (§4.4, §4.5, §9.2.1, §9.2.3):
/// <list type="bullet">
/// <item><c>DER.ABERRATION_ORDER</c>: within <c>der(n)</c> the aberrations follow their breakpoints on chromosome n from pter to
/// qter. Each aberration is placed by its breakpoint on n nearest pter; aberrations without a band on n, and bands that
/// cannot be ordered, are skipped.</item>
/// <item><c>DER.ABERRATIONS_NO_COMMA</c> / <c>REC.FORMAT</c>: a <c>der(n)</c> or <c>rec(n)</c> followed by a comma and an aberration
/// of n. Certain when the derivative has no aberration of its own (<c>rec(2),dup(2p),…</c>); otherwise uncertain, since a
/// separate abnormality of the other homologue is possible.</item>
/// <item><c>REC.NOT_ACQUIRED</c>: any <c>rec</c>; the KB context (neoplastic) makes it conditional.</item>
/// <item><c>DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME</c>: several aberrations written together without <c>der</c>/<c>rec</c> in front
/// (<c>add(5)(p15.3)add(5)(q23)</c>).</item>
/// <item><c>DER.DICENTRIC_FORMAT</c>: <c>dic(a;b)</c> followed by further aberrations; a dicentric derivative is <c>der(a;b)</c>.</item>
/// </list>
/// </summary>
internal static class Derivatives
{
    public const string AberrationOrderRule = "DER.ABERRATION_ORDER";
    public const string NoCommaRule = "DER.ABERRATIONS_NO_COMMA";
    public const string RecFormatRule = "REC.FORMAT";
    public const string RecNotAcquiredRule = "REC.NOT_ACQUIRED";
    public const string MultipleAberrationsRule = "DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME";
    public const string DicentricFormatRule = "DER.DICENTRIC_FORMAT";

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var a in model.DescendantsAndSelf().OfType<Abnormality>().Where(a => !a.IsPartial && !a.Parts.IsEmpty))
        {
            var head = a.Parts[0];
            if (head.Head == "rec")
            {
                yield return new StructuralFinding(
                    RecNotAcquiredRule,
                    head.Span,
                    "rec describes a meiotic recombinant; an acquired rearranged chromosome is written der.");
            }

            if (a.Parts.Length > 1 && head.Head == "dic")
            {
                yield return new StructuralFinding(
                    DicentricFormatRule,
                    head.Span,
                    $"A dicentric chromosome with further aberrations is a derivative: write der{head.ChromosomeList()} followed by all its aberrations.");
            }
            else if (a.Parts.Length > 1 && head.Head is not ("der" or "rec" or "ider"))
            {
                var own = head.Chromosomes.FirstOrDefault()?.Name ?? "n";
                yield return new StructuralFinding(
                    MultipleAberrationsRule,
                    a.Parts[1].Span,
                    $"A chromosome with more than one aberration is a derivative: write der({own}) before '{a.Span.Of(input)}'.");
            }

            if (head.Head == "der" && AberrationOrder(a, input) is { } order)
            {
                yield return order;
            }
        }

        foreach (var line in model.CellLines)
        {
            for (var i = 0; i + 1 < line.Items.Length; i++)
            {
                if (SplitOff(line.Items[i], line.Items[i + 1], input) is { } split)
                {
                    yield return split;
                }
            }
        }
    }

    private static StructuralFinding? AberrationOrder(Abnormality a, string input)
    {
        if (a.Parts[0].Chromosomes is not [{ IsUnknown: false } own])
        {
            return null;
        }

        (AbnormalityPart Part, BandPosition Position)? previous = null;
        foreach (var part in a.Parts.Skip(1))
        {
            var positions = part.Groups
                .Where(g => g.Chromosome?.Name == own.Name)
                .SelectMany(g => g.Breakpoints)
                .Where(b => b.Chromosome?.Name == own.Name)
                .Select(BandPosition.Of)
                .OfType<BandPosition>()
                .ToList();
            if (positions.Count == 0)
            {
                continue;
            }

            var nearest = positions.Aggregate((x, y) => y.CompareAlong(x) < 0 ? y : x);
            if (previous is { } p && nearest.CompareAlong(p.Position) < 0)
            {
                return new StructuralFinding(
                    AberrationOrderRule,
                    part.Span,
                    $"'{part.Span.Of(input)}' has a breakpoint on chromosome {own.Name} nearer pter than '{p.Part.Span.Of(input)}' and is listed before it.",
                    [p.Part.Span]);
            }

            previous = (part, nearest);
        }

        return null;
    }

    /// <summary>A <c>der(n)</c>/<c>rec(n)</c> followed by a comma and an unsigned aberration of n.</summary>
    private static StructuralFinding? SplitOff(KaryotypeItem item, KaryotypeItem next, string input)
    {
        if (item is not Abnormality { IsPartial: false, Kind: "der" or "rec", Parts: [var head, ..] } derivative
            || head.Chromosomes is not [{ IsUnknown: false } own]
            || next is not Abnormality { IsPartial: false, Sign: GainLoss.None, Kind: not ("der" or "rec" or "ider"), Parts: [var first, ..] } aberration
            || !first.Chromosomes.Any(c => c.Name == own.Name)
            || (head.Groups.Length > 0 && derivative.Parts.Length == 1))
        {
            return null;
        }

        var bare = derivative.Parts.Length == 1;
        var symbol = head.Head!;
        return new StructuralFinding(
            symbol == "rec" ? RecFormatRule : NoCommaRule,
            aberration.Span,
            $"The aberrations of {symbol}({own.Name}) follow it directly, without a comma: '{derivative.Span.Of(input)}{aberration.Span.Of(input)}'.",
            [derivative.Span],
            bare ? Certainty.Certain : Certainty.Uncertain);
    }
}
