using CytoLex.Core.Semantics;

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
}

/// <summary>An item listed after <see cref="Before"/> although it belongs before it.</summary>
internal sealed record OrderViolation(OrderRule Rule, KaryotypeItem Item, KaryotypeItem Before);

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
/// earlier item with the greatest key, by the rule of the first key part that differs.
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
    public static IEnumerable<OrderViolation> Violations(KaryotypeModel model)
    {
        foreach (var line in model.CellLines)
        {
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
                    yield return new OrderViolation(RuleOf(w.Key, key), item, w.Item);
                }

                seen.Add((item, key));
            }
        }
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
            return earlier.Category == Category.UnknownDer || later.Category == Category.UnknownDer ? OrderRule.UnknownCentromereDer : OrderRule.UnidentifiedLast;
        }

        if (earlier.Chromosome != later.Chromosome)
        {
            return earlier.Chromosome <= 1 && later.Chromosome <= 1 ? OrderRule.XBeforeY : OrderRule.ChromosomeNumber;
        }

        return earlier.Structural != later.Structural ? OrderRule.NumericalBeforeStructural : OrderRule.HomologuesAlphabetical;
    }

    private static Key? KeyOf(KaryotypeItem item) => item switch
    {
        WholeChromosomeChange w => Rank(w.Chromosome) is { } r ? new Key(Category.Identified, r, 0, null) : null,
        ChromosomeRegion { Chromosome: { } c } => Rank(c) is { } r ? new Key(Category.Identified, r, 1, null) : null,
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

        return Rank(chromosome) is { } rank ? new Key(Category.Identified, rank, 1, part.Symbols[0].Symbol) : null;
    }

    /// <summary>X = 0, Y = 1, autosome n = n + 1; <c>null</c> for <c>?</c> or an invalid identifier.</summary>
    private static int? Rank(ChromosomeRef c) =>
        c.Name switch
        {
            "X" => 0,
            "Y" => 1,
            _ when c.IsValidIdentifier() && c.Number is { } n => n + 1,
            _ => null,
        };
}
