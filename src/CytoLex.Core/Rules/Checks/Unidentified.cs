using System.Globalization;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// Markers, unidentified rings, double minutes and copy numbers (§9.2.12, §9.2.15, §9.3; T-068):
/// <list type="bullet">
/// <item><c>MAR.PLUS_SIGN</c> / <c>RING.UNIDENTIFIED</c>: an unidentified <c>mar</c> or <c>r</c> is written with a plus sign
/// (<c>47,XX,mar</c>, <c>50,XX,…,r</c>). A minus sign (a marker lost in clonal evolution) is left alone.</item>
/// <item><c>DMIN.FORMAT</c>: <c>dmin</c> takes no sign (<c>+1dmin</c>); <c>dmin14</c> is the parser's.</item>
/// <item><c>MAR.NUMBERED</c>: the same numbered marker listed as separate items (<c>+mar1,+mar1</c> → <c>+mar1×2</c>), or with a
/// leading count (<c>+3mar1</c>).</item>
/// <item><c>KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY</c>: a leading count on anything but an unidentified <c>mar</c>/<c>r</c> or
/// <c>dmin</c> (<c>2del(6)(q13q23)</c>), and more copies of an unsigned rearrangement of one autosome than there are normal
/// homologues to replace (<c>47,XX,del(6)(q13q23)×3</c>; <see cref="ChromosomeCount.ExcessCopies"/>).</item>
/// <item>The order cases that <see cref="ItemOrder"/> gives these rules: a ring after a marker or a ring of known origin
/// out of number order (<c>RING.UNIDENTIFIED</c>), a marker or ring after double minutes (<c>DMIN.FORMAT</c>).</item>
/// </list>
/// </summary>
internal static class Unidentified
{
    public const string MarPlusSignRule = "MAR.PLUS_SIGN";
    public const string RingRule = "RING.UNIDENTIFIED";
    public const string DminRule = "DMIN.FORMAT";
    public const string MarNumberedRule = "MAR.NUMBERED";
    public const string MultiplySignRule = "KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY";

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var line in model.CellLines)
        {
            var level = ChromosomeCount.ReferenceLevel(line);
            var items = line.Items.OfType<Abnormality>().Where(a => !a.IsPartial && !a.Parts.IsEmpty).ToList();
            foreach (var a in items)
            {
                var unidentified = ChromosomeCount.IsUnidentified(a);
                if (unidentified && a.Sign == GainLoss.None && a.Kind is "mar" or "r")
                {
                    var plus = FixProposal.Insert(a.Span.Start, "+");
                    yield return a.Kind == "mar"
                        ? new StructuralFinding(MarPlusSignRule, a.Span, $"A marker chromosome is always written with a plus sign: +{a.Span.Of(input)}.", Fix: plus)
                        : new StructuralFinding(RingRule, a.Span, $"A ring of unknown origin is written with a plus sign: +{a.Span.Of(input)}.", Fix: plus);
                }

                if (a.Kind == "dmin" && a.Sign != GainLoss.None && a.Syntax is not null)
                {
                    var sign = a.Syntax.PresentTokens().First();
                    yield return new StructuralFinding(
                        DminRule,
                        TextSpan.FromBounds(sign.Span.Start, a.Parts[0].Span.End),
                        "Double minutes take no plus or minus sign and are not counted; write the number per cell before dmin.",
                        Fix: FixProposal.Delete(sign.Span));
                }

                if (a.LeadingCount is { } leading && LeadingCountRule(a, unidentified) is { } rule)
                {
                    // Move the count behind the abnormality as ×n: +3mar1 → +mar1×3, 2del(6)(q13q23) → del(6)(q13q23)×2.
                    var moveCount = a.Copies is null && !leading.IsRange
                        ? new FixProposal([
                            new TextEdit(leading.Span, string.Empty),
                            new TextEdit(new TextSpan(a.Parts[^1].Span.End, 0), FixHelpers.Multiply(input) + leading.Span.Of(input)),
                        ])
                        : null;
                    yield return rule == MarNumberedRule
                        ? new StructuralFinding(rule, leading.Span, $"Copies of a numbered marker take ×n after the symbol: write mar{a.Parts[0].SymbolNumber}×{leading.Span.Of(input)}.", Fix: moveCount)
                        : new StructuralFinding(rule, leading.Span, $"The number of copies of a rearranged chromosome is written ×{leading.Span.Of(input)} after the whole abnormality, not before it.", Fix: moveCount);
                }

                if (level is { } k && ChromosomeCount.ExcessCopies(a, k) > 0)
                {
                    var homologues = k;
                    yield return new StructuralFinding(
                        MultiplySignRule,
                        a.Copies!.Span,
                        string.Create(CultureInfo.InvariantCulture, $"Only {homologues} copies can replace the normal chromosomes {a.Parts[0].Chromosomes[0].Name} (×{homologues}); the further copies are supernumerary and written separately with a plus sign."),
                        Fix: SplitExcessCopies(a, homologues, ChromosomeCount.ExcessCopies(a, k), input));
                }
            }

            foreach (var repeat in RepeatedNumberedMarkers(items))
            {
                yield return new StructuralFinding(
                    MarNumberedRule,
                    repeat.Item.Span,
                    string.Create(CultureInfo.InvariantCulture, $"mar{repeat.Item.Parts[0].SymbolNumber} is listed {repeat.Total} times: write its copies once as mar{repeat.Item.Parts[0].SymbolNumber}×{repeat.Total}."),
                    [repeat.First.Span],
                    Fix: MergeRepeats(line, repeat, items, input));
            }
        }

        foreach (var v in ItemOrder.Violations(model, input).Where(v => v.Rule is OrderRule.Ring or OrderRule.DoubleMinute))
        {
            var (rule, detail) = v.Rule == OrderRule.Ring
                ? (RingRule, v.Item is Abnormality ring && ChromosomeCount.IsUnidentified(ring)
                    ? $"A ring of unknown origin comes before any marker chromosome: move '{v.Item.Span.Of(input)}' before '{v.Before.Span.Of(input)}'."
                    : $"A ring of known origin is placed in chromosome number order: move '{v.Item.Span.Of(input)}' before '{v.Before.Span.Of(input)}'.")
                : (DminRule, $"Double minutes are placed after any marker chromosome: move '{v.Item.Span.Of(input)}' before '{v.Before.Span.Of(input)}'.");
            yield return new StructuralFinding(rule, v.Item.Span, detail, [v.Before.Span], Fix: v.Fix);
        }
    }

    /// <summary><c>+mar1,+mar1,+mar1</c> → <c>+mar1×3</c>: the first item takes the total, the repeats are removed.</summary>
    private static FixProposal MergeRepeats(CellLine line, Repeat repeat, List<Abnormality> items, string input)
    {
        var all = items.Where(a => a.Kind == "mar" && a.Parts.Length == 1 && a.LeadingCount is null && a.Copies is null
            && a.Sign == repeat.First.Sign && a.Parts[0].SymbolNumber == repeat.First.Parts[0].SymbolNumber).ToList();
        var total = string.Create(CultureInfo.InvariantCulture, $"{FixHelpers.Multiply(input)}{repeat.Total}");
        return new FixProposal([
            new TextEdit(new TextSpan(repeat.First.Span.End, 0), total),
            .. FixHelpers.Remove(line, all.Skip(1)).Edits,
        ]);
    }

    /// <summary><c>del(6)(q13q23)×3</c> with two homologues → <c>del(6)(q13q23)×2,+del(6)(q13q23)</c>.</summary>
    private static FixProposal? SplitExcessCopies(Abnormality a, int homologues, int excess, string input)
    {
        if (a.Copies is not { } copies || a.Sign != GainLoss.None || excess <= 0)
        {
            return null;
        }

        var x = FixHelpers.Multiply(input);
        var body = TextSpan.FromBounds(a.Span.Start, a.Parts[^1].Span.End).Of(input);
        var kept = homologues == 1 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{x}{homologues}");
        var extra = excess == 1 ? $",+{body}" : string.Create(CultureInfo.InvariantCulture, $",+{body}{x}{excess}");
        return FixProposal.Replace(copies.Span, kept + extra);
    }

    /// <summary>The rule a leading count breaks, or <c>null</c> where it is allowed (<c>+3mar</c>, <c>5r</c>, <c>~14dmin</c>).</summary>
    private static string? LeadingCountRule(Abnormality a, bool unidentified) => a.Kind switch
    {
        "dmin" => null,
        "mar" when unidentified && a.Parts[0].SymbolNumber is not null => MarNumberedRule,
        "mar" or "r" when unidentified => null,
        _ => MultiplySignRule,
    };

    private readonly record struct Repeat(Abnormality Item, Abnormality First, int Total);

    /// <summary>The second and later items of a numbered marker listed more than once with the same sign (<c>+mar1,+mar1</c>).</summary>
    private static IEnumerable<Repeat> RepeatedNumberedMarkers(List<Abnormality> items) =>
        items
            .Where(a => a.Kind == "mar" && a.Parts.Length == 1 && a.Parts[0].SymbolNumber is not null && a.LeadingCount is null && a.Copies is null)
            .GroupBy(a => (a.Sign, a.Parts[0].SymbolNumber))
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Skip(1).Select(a => new Repeat(a, g.First(), g.Count())));
}
