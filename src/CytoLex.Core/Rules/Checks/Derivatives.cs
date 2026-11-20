using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
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
                // dic(5;7)(q22;p13)t(3;7)(…) → der(5;7)t(5;7)(q22;p13)t(3;7)(…): the dicentric becomes the derivative's first aberration.
                var symbol = head.Symbols[^1].Span;
                var list = TextSpan.FromBounds(symbol.End, head.HeadSpan().End);
                yield return new StructuralFinding(
                    DicentricFormatRule,
                    head.Span,
                    $"A dicentric chromosome with further aberrations is a derivative: write der{head.ChromosomeList()} followed by all its aberrations.",
                    Fix: head.Symbols.Length == 1 && head.HasChromosomeList ? FixProposal.Replace(symbol, "der" + list.Of(input) + "t") : null);
            }
            else if (a.Parts.Length > 1 && head.Head is not ("der" or "rec" or "ider"))
            {
                var own = head.Chromosomes.FirstOrDefault()?.Name ?? "n";
                yield return new StructuralFinding(
                    MultipleAberrationsRule,
                    a.Parts[1].Span,
                    $"A chromosome with more than one aberration is a derivative: write der({own}) before '{a.Span.Of(input)}'.",
                    Fix: head.Chromosomes is [{ IsUnknown: false } c] && a.Parts.All(p => p.Chromosomes.Any(x => x.Name == c.Name))
                        ? FixProposal.Insert(head.Span.Start, $"der({c.Span.Of(input)})")
                        : null);
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
                if (SplitOff(line, i, input) is { } split)
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
                    [p.Part.Span],
                    Fix: new FixProposal([new TextEdit(p.Part.Span, part.Span.Of(input)), new TextEdit(part.Span, p.Part.Span.Of(input))]));
            }

            previous = (part, nearest);
        }

        return null;
    }

    /// <summary>
    /// A <c>der(n)</c>/<c>rec(n)</c> followed by a comma and an unsigned aberration of n. The fix joins the derivative and
    /// the run of such aberrations after it (<c>rec(2),dup(2p),inv(2)(p21q31)</c> → <c>rec(2)dup(2p)inv(2)(p21q31)</c>).
    /// </summary>
    private static StructuralFinding? SplitOff(CellLine line, int index, string input)
    {
        var (item, next) = (line.Items[index], line.Items[index + 1]);
        if (item is not Abnormality { IsPartial: false, Kind: "der" or "rec", Parts: [var head, ..] } derivative
            || head.Chromosomes is not [{ IsUnknown: false } own]
            || !IsAberrationOf(next, own.Name)
            || (head.Groups.Length > 0 && derivative.Parts.Length == 1))
        {
            return null;
        }

        var aberration = (Abnormality)next;
        var commas = new List<TextEdit>();
        for (var j = index + 1; j < line.Items.Length && IsAberrationOf(line.Items[j], own.Name); j++)
        {
            commas.Add(new TextEdit(TextSpan.FromBounds(line.Items[j - 1].Span.End, line.Items[j].Span.Start), string.Empty));
        }

        var bare = derivative.Parts.Length == 1;
        var symbol = head.Head!;
        return new StructuralFinding(
            symbol == "rec" ? RecFormatRule : NoCommaRule,
            aberration.Span,
            $"The aberrations of {symbol}({own.Name}) follow it directly, without a comma: '{derivative.Span.Of(input)}{aberration.Span.Of(input)}'.",
            [derivative.Span],
            bare ? Certainty.Certain : Certainty.Uncertain,
            new FixProposal([.. commas]));
    }

    private static bool IsAberrationOf(KaryotypeItem item, string chromosome) =>
        item is Abnormality { IsPartial: false, Sign: GainLoss.None, Kind: not ("der" or "rec" or "ider"), Parts: [var first, ..] }
        && first.Chromosomes.Any(c => c.Name == chromosome);
}
