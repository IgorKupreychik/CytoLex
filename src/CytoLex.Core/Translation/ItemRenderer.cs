using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.Phrasing;
using Desc = CytoLex.Core.Translation.AbnormalityDescription;

namespace CytoLex.Core.Translation;

/// <summary>
/// The text of one item: whole-chromosome changes, abnormalities with their sign, copies and suffixes, uniparental
/// disomy, bands and variant regions, alternatives (templates: gain_loss, copies, constitutional_c, inheritance, …).
/// Abnormalities are described by <see cref="SymbolDescriber"/> or <see cref="DerivativeDescriber"/>.
/// </summary>
internal sealed class ItemRenderer(
    RenderContext ctx,
    BandPhrases bands,
    CompositionRenderer composition,
    SymbolDescriber symbols,
    DerivativeDescriber derivatives)
{
    /// <summary>The text of an item, without the final full stop.</summary>
    public string Text(KaryotypeItem item) => item switch
    {
        WholeChromosomeChange w => WholeChromosome(w),
        Abnormality a => AbnormalityText(a),
        ChromosomeRegion r => Region(r, null),
        AlternativeItem alt => Alternative(alt),
        OmittedItems => P("summary", "omitted_item"),
        BandComposition bc => P(
            "band_composition",
            "fragment",
            ("segments", string.Join("; ", bc.Groups.Select(g => g.Structure is { } s ? composition.Composition(s, null, out _) : bands.BreakpointList([g]))))),
        _ => P("summary", "not_translated_unparsed"),
    };

    /// <summary>The description of a symbol-led abnormality, by its head symbol.</summary>
    private Desc Describe(Abnormality a)
    {
        var part = a.Parts[0];
        return part.Head switch
        {
            "der" or "rec" or "ider" => derivatives.Describe(a),
            "mar" => symbols.Marker(a),
            "dmin" => symbols.DoubleMinutes(a),
            "r" => symbols.Ring(part),
            "idem" or "sl" or "sdl" => symbols.CloneReference(a),
            _ => symbols.Single(part),
        };
    }

    private string WholeChromosome(WholeChromosomeChange w)
    {
        var chromosome = w.Chromosome.IsUnknown ? "?" : w.Chromosome.Name;
        string text;
        if (w.Constitutional is not null && w.Sign != GainLoss.None)
        {
            text = P("gain_loss", w.Sign == GainLoss.Gain ? "gain_marked" : "loss_marked", ("chromosome", chromosome));
        }
        else
        {
            text = w.Sign switch
            {
                GainLoss.Gain => P("gain_loss", "gain", ("chromosome", chromosome)),
                GainLoss.Loss => P("gain_loss", "loss", ("chromosome", chromosome)),
                _ => P("gain_loss", "no_sign", ("chromosome", chromosome)),
            };
        }

        text += Suffixes(w, copiesDone: false);
        return w.IsQuestioned ? P("questionable_item", null, ("item", text)) : text;
    }

    private string AbnormalityText(Abnormality a)
    {
        if (a.Parts.IsEmpty)
        {
            return P("summary", "not_translated_unparsed");
        }

        if (a.Parts[0].Symbols.Any(s => s.Symbol == "upd"))
        {
            return Upd(a);
        }

        var d = Describe(a);
        var copies = a.Copies?.Count ?? 1;
        var copiesDone = false;
        var gainNoun = d.GainNoun ?? Lower(d.Head);
        string head;
        if (a.Sign == GainLoss.Gain && !d.AlwaysGained)
        {
            if (a.LeadingCount is { } count && d.Plural is not null)
            {
                head = P("gain_loss", "gain_counted", ("copies", ctx.Count(count)), ("item", d.Plural));
            }
            else if (copies > 1)
            {
                head = P("gain_loss", "gain_rearranged_copies", ("copies", N(copies)), ("item", gainNoun));
                copiesDone = true;
            }
            else
            {
                head = P("gain_loss", "gain_rearranged", ("item", gainNoun));
            }
        }
        else if (a.Sign == GainLoss.Loss)
        {
            head = P("gain_loss", "loss_rearranged", ("item", gainNoun));
        }
        else
        {
            head = d.Head;
        }

        var text = head + d.Rest + Suffixes(a, copiesDone);
        return a.IsQuestioned ? P("questionable_item", null, ("item", text)) : text;
    }

    /// <summary><c>; 2 copies</c>, <c>; constitutional</c>, <c>; inherited from the mother</c>.</summary>
    private string Suffixes(SuffixedItem item, bool copiesDone)
    {
        var text = "";
        if (!copiesDone && item.Copies?.Count is { } n and > 1)
        {
            text += "; " + P("copies", null, ("copies", N(n)));
        }

        if (item.Constitutional is { } c)
        {
            text += "; " + P(c.IsQuestioned ? "uncertain_constitutional" : "constitutional_c", null);
        }

        if (item.Inheritance is { } inh)
        {
            text += "; " + Inheritance(inh.Symbol);
        }

        return text;
    }

    private string Inheritance(string symbol) => symbol switch
    {
        "mat" => P("maternal", null),
        "pat" => P("paternal", null),
        "dn" => P("de_novo", null),
        _ => P("inherited", null),
    };

    /// <summary><c>upd(15)mat</c>, <c>upd der(13;13)(q10;q10)pat</c>.</summary>
    private string Upd(Abnormality a)
    {
        var part = a.Parts[0];
        var chromosome = part.Chromosomes.IsEmpty ? "?" : part.Chromosomes[0].Name;
        var origin = a.Inheritance is { } inh ? Inheritance(inh.Symbol) : P("uniparental_disomy", "origin_unknown");
        if (part.Head == "upd")
        {
            return P("uniparental_disomy", "simple", ("chromosome", chromosome), ("origin", origin));
        }

        var d = Describe(a);
        return P("uniparental_disomy", "of_item", ("chromosome", chromosome), ("origin", origin), ("item", Lower(d.Head) + d.Rest));
    }

    /// <summary>A band or region given on its own (book fragments): <c>Region 1p1; the band is not given</c>; variant features.</summary>
    private string Region(ChromosomeRegion r, ChromosomeRef? implied)
    {
        var chromosome = r.Chromosome ?? implied;
        var elements = r.Elements;
        if (elements is [{ Kind: RegionElementKind.Band, Band: { } b }])
        {
            var name = (chromosome?.Name ?? "") + b.Text.TrimEnd('?');
            if (b.IsQuestioned)
            {
                return P("region_only", "band_unknown", ("region", name));
            }

            return b.BandNumber is { Length: 1 }
                ? P("region_only", "band_not_given", ("region", name))
                : P("band", "band", ("band", name));
        }

        // Variant features (ch. 7): 16qh+, 1q12–.
        var where = chromosome is null ? "" : P("band", "chromosome", ("chromosome", chromosome.Name));
        var parts = new List<string>();
        string? place = null;
        RegionElementKind? sign = null;
        foreach (var e in elements)
        {
            switch (e.Kind)
            {
                case RegionElementKind.Band:
                    place = e.Band is { BandNumber: not null } band
                        ? P("band", "band", ("band", (chromosome?.Name ?? "") + band.Text))
                        : bands.Arm(e.Text.Length > 0 ? e.Text[..1] : null) + " " + P("band", "of", ("where", where));
                    break;
                case RegionElementKind.Feature:
                    parts.Add(Feature(e.Text, place ?? where, chromosome?.Name ?? "?"));
                    break;
                default:
                    sign = e.Kind;
                    break;
            }
        }

        var feature = parts.Count > 0 ? And(parts) : place ?? where;
        return sign switch
        {
            RegionElementKind.Increase => P("length_change", "increase", ("feature", feature)),
            RegionElementKind.Decrease => P("length_change", "decrease", ("feature", feature)),
            _ => P("length_change", "plain", ("feature", feature)),
        };
    }

    /// <summary>A band or region inside an alternative: <c>band 1q23</c>, <c>region 1q2</c>.</summary>
    private string RegionShort(ChromosomeRegion r, ChromosomeRef? implied)
    {
        if (r.Elements is [{ Kind: RegionElementKind.Band, Band: { IsQuestioned: false } b }])
        {
            var name = ((r.Chromosome ?? implied)?.Name ?? "") + b.Text;
            return b.BandNumber is { Length: 1 } ? P("region_only", "region", ("region", name)) : P("band", "band", ("band", name));
        }

        return Region(r, implied);
    }

    private string Feature(string symbol, string where, string chromosome) => symbol switch
    {
        "h" => P("heterochromatin", null, ("where", where)),
        "s" => P("satellite", null, ("where", where)),
        "stk" => P("stalk", null, ("where", where)),
        "ps" => P("short_arm_satellite", null, ("chromosome", chromosome)),
        "qs" => P("long_arm_satellite", null, ("chromosome", chromosome)),
        "var" => P("variant_region", null, ("where", where)),
        "cen" => P("centromere", "of_chromosome", ("chromosome", where)),
        _ => symbol,
    };

    /// <summary><c>Either band 1q23 or band 1q24</c>.</summary>
    private string Alternative(AlternativeItem alt)
    {
        var implied = alt.Readings.FirstOrDefault() switch
        {
            ChromosomeRegion r => r.Chromosome,
            WholeChromosomeChange w => w.Chromosome,
            _ => null,
        };
        var readings = alt.Readings.Select(r => Lower(r is ChromosomeRegion region ? RegionShort(region, implied) : Text(r))).ToList();
        return P("alternative", "items", ("first", string.Join(", ", readings.Take(readings.Count - 1))), ("second", readings[^1]));
    }

    private string P(string construct, string? when, params (string Slot, string Value)[] slots) => ctx.P(construct, when, slots);
}
