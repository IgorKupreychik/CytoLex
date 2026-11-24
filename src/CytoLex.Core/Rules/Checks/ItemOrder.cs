using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>Which order rule an out-of-order item breaks.</summary>
internal enum OrderRule
{
    /// <summary><c>KARYO.ABNORMALITY_ORDER</c>: sex chromosomes first, then autosomes by number.</summary>
    ChromosomeNumber,

    /// <summary><c>ORDER.X_BEFORE_Y</c>.</summary>
    XBeforeY,

    /// <summary><c>ORDER.NUMERICAL_BEFORE_STRUCTURAL</c>.</summary>
    NumericalBeforeStructural,

    /// <summary><c>ORDER.HOMOLOGUES_ALPHABETICAL</c>.</summary>
    HomologuesAlphabetical,

    /// <summary><c>ORDER.UNKNOWN_CENTROMERE_DER</c>.</summary>
    UnknownCentromereDer,

    /// <summary><c>ORDER.UNIDENTIFIED_LAST</c>.</summary>
    UnidentifiedLast,

    /// <summary><c>RING.UNIDENTIFIED</c>: an unidentified ring after a marker, or a ring of known origin out of number order (T-068).</summary>
    Ring,

    /// <summary><c>DMIN.FORMAT</c>: a marker or unidentified ring after double minutes (T-068).</summary>
    DoubleMinute,
}

/// <summary>An item listed after <see cref="Before"/> although it belongs before it.</summary>
/// <param name="Rule">The order rule the item breaks.</param>
/// <param name="Item">The out-of-order item.</param>
/// <param name="Before">The earlier item it belongs before.</param>
/// <param name="Fix">The cell line's items in the right order (T-035): every violation of a line shares it.</param>
internal sealed record OrderViolation(OrderRule Rule, KaryotypeItem Item, KaryotypeItem Before, FixProposal? Fix = null);

/// <summary>
/// The order of abnormalities within a cell line (§4.1 ¶2, §6). Each item gets a key:
/// <list type="number">
/// <item>category: identified &lt; <c>der(?)</c> &lt; unidentified <c>r</c> &lt; <c>mar</c> &lt; <c>dmin</c> (§6 ¶2–3);</item>
/// <item>chromosome: X &lt; Y &lt; 1 … 22 — the derivative's own chromosome for <c>der</c>/<c>rec</c>/<c>ider</c>, else the first
/// chromosome of the item;</item>
/// <item>numerical (a whole-chromosome change) before structural;</item>
/// <item>for structural items: the abbreviation, alphabetically (homologues, §6 ¶1).</item>
/// </list>
/// Items without a key (<c>or</c> readings, <c>…</c>, <c>inc</c>, <c>idem</c>, items of an unknown chromosome other than
/// <c>der(?)</c>) take no part. An item whose key is less than that of an earlier item is reported once, against the
/// earlier item with the greatest key, by the rule of the first key part that differs. Two symbol rules take over from the
/// general ones (one fault, one finding; T-068): an unidentified ring after a marker and a ring of known origin out of
/// number order go to <c>RING.UNIDENTIFIED</c>; a marker or unidentified ring after double minutes goes to <c>DMIN.FORMAT</c>.
/// </summary>
internal static class ItemOrder
{
    /// <summary>The order categories of §6 ¶2–3.</summary>
    private enum Category
    {
        Identified,
        UnknownDer,
        UnidentifiedRing,
        Marker,
        DoubleMinute,
    }

    private readonly record struct Key(Category Category, int Chromosome, int Structural, string? Symbol);

    /// <summary>Every out-of-order item of the model, in input order.</summary>
    /// <param name="model">The segment's model.</param>
    /// <param name="input">The whole input, for the fix (<c>null</c>: no fix).</param>
    public static IEnumerable<OrderViolation> Violations(KaryotypeModel model, string? input = null)
    {
        foreach (var line in model.CellLines)
        {
            FixProposal? fix = null;
            var fixBuilt = false;
            var seen = new List<(KaryotypeItem Item, Key Key)>();
            foreach (var item in line.Items)
            {
                if (item.IsPartial || KeyOf(item) is not { } key)
                {
                    continue;
                }

                (KaryotypeItem Item, Key Key)? worst = null;
                foreach (var earlier in seen)
                {
                    if (Compare(earlier.Key, key) > 0 && (worst is null || Compare(earlier.Key, worst.Value.Key) > 0))
                    {
                        worst = earlier;
                    }
                }

                if (worst is { } w)
                {
                    if (!fixBuilt && input is not null)
                    {
                        fix = SortFix(line, input);
                        fixBuilt = true;
                    }

                    yield return new OrderViolation(RuleOf(w.Key, key), item, w.Item, fix);
                }

                seen.Add((item, key));
            }
        }
    }

    /// <summary>
    /// The fix of a line's order (T-035): a stable sort of the items that have a key, written back into the positions of
    /// keyed items (items without a key, such as <c>…</c> or <c>inc</c>, stay where they are), joined by commas. The edit
    /// covers only the run from the first to the last item that moves; <c>null</c> if nothing moves.
    /// </summary>
    private static FixProposal? SortFix(CellLine line, string input)
    {
        var items = line.Items;
        if (items.Any(i => i.IsPartial))
        {
            return null;
        }

        var keyed = items.Select((item, index) => (Item: item, Index: index, Key: KeyOf(item))).Where(x => x.Key is not null).ToList();
        var sorted = keyed.Select((x, order) => (x, order))
            .OrderBy(p => p.x.Key!.Value, Comparer<Key>.Create(Compare))
            .ThenBy(p => p.order)
            .Select(p => p.x.Item)
            .ToList();
        var result = items.ToArray();
        for (var k = 0; k < keyed.Count; k++)
        {
            result[keyed[k].Index] = sorted[k];
        }

        var changed = Enumerable.Range(0, items.Length).Where(i => !ReferenceEquals(result[i], items[i])).ToList();
        if (changed.Count == 0)
        {
            return null;
        }

        var (from, to) = (changed[0], changed[^1]);
        var text = string.Join(",", result[from..(to + 1)].Select(i => i.Span.Of(input)));
        return FixProposal.Replace(TextSpan.FromBounds(items[from].Span.Start, items[to].Span.End), text);
    }

    /// <summary>
    /// <paramref name="items"/> in §6 order (a stable sort by the key above); items without a key (<c>…</c>, <c>inc</c>,
    /// <c>or</c> readings) follow in their given order. Used to write out the full form of a clone (T-070).
    /// </summary>
    public static List<T> Sort<T>(IEnumerable<T> items, Func<T, KaryotypeItem> item)
    {
        var withKeys = items.Select(x => (Value: x, Key: KeyOf(item(x)))).ToList();
        return
        [
            .. withKeys.Where(x => x.Key is not null).OrderBy(x => x.Key!.Value, Comparer<Key>.Create(Compare)).Select(x => x.Value),
            .. withKeys.Where(x => x.Key is null).Select(x => x.Value),
        ];
    }

    private static int Compare(Key a, Key b)
    {
        var c = a.Category.CompareTo(b.Category);
        if (c != 0 || a.Category != Category.Identified)
        {
            return c;
        }

        c = a.Chromosome.CompareTo(b.Chromosome);
        if (c != 0)
        {
            return c;
        }

        c = a.Structural.CompareTo(b.Structural);
        if (c != 0)
        {
            return c;
        }

        return a.Symbol is null || b.Symbol is null ? 0 : string.CompareOrdinal(a.Symbol, b.Symbol);
    }

    private static OrderRule RuleOf(Key earlier, Key later)
    {
        if (earlier.Category != later.Category)
        {
            return (earlier.Category, later.Category) switch
            {
                (Category.UnknownDer, _) or (_, Category.UnknownDer) => OrderRule.UnknownCentromereDer,
                (Category.Marker, Category.UnidentifiedRing) => OrderRule.Ring,
                (Category.DoubleMinute, Category.Marker or Category.UnidentifiedRing) => OrderRule.DoubleMinute,
                _ => OrderRule.UnidentifiedLast,
            };
        }

        if (earlier.Chromosome != later.Chromosome)
        {
            return earlier.Chromosome <= 1 && later.Chromosome <= 1 ? OrderRule.XBeforeY
                : later.Symbol == "r" ? OrderRule.Ring
                : OrderRule.ChromosomeNumber;
        }

        return earlier.Structural != later.Structural ? OrderRule.NumericalBeforeStructural : OrderRule.HomologuesAlphabetical;
    }

    private static Key? KeyOf(KaryotypeItem item) => item switch
    {
        WholeChromosomeChange w => Structure.Rank(w.Chromosome) is { } r ? new Key(Category.Identified, r, 0, null) : null,
        ChromosomeRegion { Chromosome: { } c } => Structure.Rank(c) is { } r ? new Key(Category.Identified, r, 1, null) : null,
        Abnormality a => AbnormalityKey(a),
        _ => null,
    };

    private static Key? AbnormalityKey(Abnormality a)
    {
        if (a.Parts.IsEmpty || a.Parts[0].Head is not { } head)
        {
            return null;
        }

        var part = a.Parts[0];
        switch (head)
        {
            case "dmin":
                return new Key(Category.DoubleMinute, 0, 1, null);
            case "mar" when part.Chromosomes.IsEmpty:
                return new Key(Category.Marker, 0, 1, null);
            case "r" when part.Chromosomes.IsEmpty:
                return new Key(Category.UnidentifiedRing, 0, 1, null);
            case "inc" or "idem" or "sl" or "sdl":
                return null;
            default:
                break;
        }

        if (part.Chromosomes.IsEmpty)
        {
            return null;
        }

        var isDerivative = part.Symbols.Any(s => s.Symbol is "der" or "rec" or "ider");
        var chromosome = part.Chromosomes[0];
        if (chromosome.IsUnknown)
        {
            return isDerivative ? new Key(Category.UnknownDer, 0, 1, null) : null;
        }

        return Structure.Rank(chromosome) is { } rank ? new Key(Category.Identified, rank, 1, part.Symbols[0].Symbol) : null;
    }
}
