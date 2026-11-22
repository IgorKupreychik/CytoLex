using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.BandPhrases;
using static CytoLex.Core.Translation.Phrasing;
using Desc = CytoLex.Core.Translation.AbnormalityDescription;

namespace CytoLex.Core.Translation;

/// <summary>
/// Describes derivatives (<c>der</c>, <c>rec</c>, <c>ider</c>; §9.1) and the phrases of the rearrangements they result from
/// ("part" forms: <c>a translocation between chromosomes 1 and 3 with breaks at 1p32 and 3q21</c>).
/// </summary>
internal sealed class DerivativeDescriber(
    RenderContext ctx,
    ChromosomePhrases chromosomes,
    BandPhrases bands,
    CompositionRenderer composition,
    SymbolDescriber symbols)
{
    /// <summary>A derivative abnormality: its own part (<c>der(1)</c>) and the rearrangements after it.</summary>
    public Desc Describe(Abnormality a)
    {
        var own = a.Parts[0];
        var changes = a.Parts.Skip(1).ToList();
        return own.Head switch
        {
            "rec" => Recombinant(own, changes),
            "ider" => Isoderivative(own, changes),
            _ => Der(own, changes),
        };
    }

    /// <summary><c>Derivative chromosome 1</c>, <c>… of unknown centromere origin</c>, <c>… with the centromeres of chromosomes 5 and 7</c>.</summary>
    private (string Head, bool Descriptive) DerHead(AbnormalityPart own)
    {
        var list = own.Chromosomes;
        if (list.IsEmpty || list is [{ IsUnknown: true }])
        {
            return (P("derivative", "head_unknown"), true);
        }

        if (list.Length >= 2)
        {
            return (P("derivative", "head_centromeres", ("chromosomes", chromosomes.Chromosomes(list))), true);
        }

        var c = list[0];
        var name = c.Name;
        if (c.IsUnderlined)
        {
            name = P("band", "of_underlined_homologue", ("chromosome", c.Name));
        }
        else if (ctx.Homologues.Contains(c.Name))
        {
            name = P("band", "of_first_homologue", ("chromosome", c.Name));
        }

        return (P("derivative", "head", ("chromosome", name)), false);
    }

    private Desc Der(AbnormalityPart own, List<AbnormalityPart> changes)
    {
        var (head, descriptive) = DerHead(own);
        var mosaic = own.Symbols.Any(s => s.Symbol == "mos") ? ", " + P("mosaic", "in_abnormality") : "";
        head += mosaic;
        descriptive |= mosaic.Length > 0;

        // Whole-arm derivative: der(1;3)(p10;q10), der(8;8)(q10;q10)del(8)(q22)…
        if (!own.IsDetailed && own.Groups.Length == 2 && own.Groups.All(g => g.Breakpoints is [{ BandNumber: "10" }]))
        {
            return WholeArm(own, changes);
        }

        if (own.IsDetailed)
        {
            return new Desc(head, MadeUp(own, descriptive));
        }

        // The composition written on the one rearrangement: der(5)ins(5;?)(pter→q13::?::q13→qter).
        if (changes is [{ IsDetailed: true, Groups.Length: 1 } detailed])
        {
            return new Desc(head, MadeUp(detailed, descriptive));
        }

        if (changes.Count == 0)
        {
            return new Desc(head, "; " + P("derivative", "none"));
        }

        if (changes.All(c => c.Groups.IsEmpty && ctx.GivenEarlier(c)))
        {
            return new Desc(head, " " + P("derivative", "same", ("changes", And([.. changes.Select(SameNoun)]))) + "; " + P("band", "as_first_given"));
        }

        if (changes is [{ Head: "r" } ring])
        {
            return new Desc(head, ": " + Component(ring));
        }

        return new Desc(head, (mosaic.Length > 0 ? ": " : ", ") + P("derivative", "resulting", ("changes", Components(changes))));
    }

    private Desc WholeArm(AbnormalityPart own, List<AbnormalityPart> changes)
    {
        var (b1, b2) = (own.Groups[0].Breakpoints[0], own.Groups[1].Breakpoints[0]);
        var arms = b1.Arm == b2.Arm
            ? P("derivative", "arms_same", ("arms", bands.Arm(b1.Arm, "plural")), ("chromosomes", chromosomes.Chromosomes(own.Chromosomes)))
            : P(
                "derivative",
                "arms_different",
                ("arm1", bands.Arm(b1.Arm)),
                ("chromosome1", chromosomes.Chromosome(own.Chromosomes[0])),
                ("arm2", bands.Arm(b2.Arm)),
                ("chromosome2", chromosomes.Chromosome(own.Chromosomes[1])));
        var whole = P("derivative", "whole_arm_head", ("arms", arms))
            + P("derivative", "whole_arm_rest", ("breakpoints", And([bands.Band(b1), bands.Band(b2)])));
        return changes.Count == 0
            ? new Desc(whole)
            : new Desc(whole, ", " + P("derivative", "resulting", ("changes", Components(changes))));
    }

    /// <summary>The composition of a derivative in the detailed system: <c> made up of …</c>, <c>: a ring made up of …</c>.</summary>
    private string MadeUp(AbnormalityPart part, bool descriptive)
    {
        var segments = composition.Composition(part.Groups[0].Structure!, null, out var ring);
        return ring
            ? ": " + P("band_composition", "ring", ("segments", segments))
            : (descriptive ? ", " : " ") + P("band_composition", "made_up_of", ("segments", segments));
    }

    /// <summary><c>Recombinant chromosome 2 with a duplication of part of the short arm, resulting from … in a parent</c>.</summary>
    private Desc Recombinant(AbnormalityPart own, List<AbnormalityPart> changes)
    {
        var c = own.Chromosomes.IsEmpty ? null : own.Chromosomes[0];
        var head = P("recombinant", "head", ("chromosome", c?.Name ?? "?"));
        if (own.IsDetailed)
        {
            return new Desc(head, MadeUp(own, false));
        }

        // Arm-only or bare dup/del: what the recombinant carries; the rest: what it results from.
        var carries = changes.Where(p => p.Groups.IsEmpty && p.Head is "dup" or "del").ToList();
        var from = changes.Except(carries).ToList();
        var with = carries.Select(p => Carried(p, c)).ToList();
        var rest = with.Count > 0 ? " " + P("recombinant", "with", ("changes", And(with))) : "";
        if (from.Count == 0)
        {
            return new Desc(head, rest);
        }

        if (from.All(p => p.Groups.IsEmpty && ctx.GivenEarlier(p)))
        {
            return new Desc(head, rest + ", " + P("recombinant", "same", ("changes", And([.. from.Select(SameNoun)]))));
        }

        return new Desc(head, rest + ", " + P("recombinant", "resulting", ("changes", Components(from))));
    }

    /// <summary><c>a duplication of part of the short arm</c>, <c>a deletion of part of the long arm of chromosome 1</c>, <c>a deletion</c>.</summary>
    private string Carried(AbnormalityPart p, ChromosomeRef? recombinant)
    {
        var construct = p.Head == "dup" ? "duplication" : "deletion";
        if (p.Chromosomes is not [{ Arm: { } arm } c])
        {
            return P(construct, "bare_part");
        }

        return recombinant is not null && c.Name == recombinant.Name
            ? P(construct, "arm_part_own", ("arm", bands.Arm(arm)))
            : P(construct, "arm_part", ("arm", bands.Arm(arm)), ("chromosome", c.Name));
    }

    /// <summary><c>Isochromosome of the long arm of a derivative chromosome 22 that results from …</c>.</summary>
    private Desc Isoderivative(AbnormalityPart own, List<AbnormalityPart> changes)
    {
        var c = own.Chromosomes.IsEmpty ? "?" : own.Chromosomes[0].Name;
        if (own.IsDetailed)
        {
            return new Desc(P("isoderivative", "detailed_head", ("chromosome", c)), MadeUp(own, false));
        }

        var arm = own.Groups.SelectMany(g => g.Breakpoints).FirstOrDefault()?.Arm;
        return new Desc(
            P("isoderivative", "head", ("chromosome", c), ("arm", bands.Arm(arm))),
            changes.Count == 0 ? "" : " " + P("isoderivative", "resulting", ("changes", Components(changes))));
    }

    /// <summary>The rearrangements of a derivative, joined: <c>A and B</c>, <c>A, and B</c> (when one has its own "and"), <c>A, B, and C</c>.</summary>
    private string Components(IReadOnlyList<AbnormalityPart> parts) => SerialAnd([.. parts.Select(Component)]);

    /// <summary>The noun of a rearrangement given before: <c>translocation between chromosomes 9 and 22</c>.</summary>
    private string SameNoun(AbnormalityPart p) => P(Construct(p.Head), "same_noun", ("chromosomes", chromosomes.Chromosomes(p.Chromosomes)));

    private static string Construct(string? head) => head switch
    {
        "t" => "translocation",
        "ins" => "insertion",
        "del" => "deletion",
        "dup" => "duplication",
        "inv" => "inversion",
        "add" => "additional_material",
        "hsr" => "hsr",
        "r" => "ring",
        "i" => "isochromosome",
        "trp" => "triplication",
        "qdp" => "quadruplication",
        "fra" => "fragile_site",
        "dic" => "dicentric",
        _ => "derivative",
    };

    /// <summary>One rearrangement of a derivative: <c>a translocation between chromosomes 1 and 3 with breaks at 1p32 and 3q21</c>.</summary>
    private string Component(AbnormalityPart p)
    {
        var groups = p.Groups;
        var bps = groups.SelectMany(g => g.Breakpoints).ToList();
        var c = p.Chromosomes.IsEmpty ? "?" : p.Chromosomes[0].Name;
        switch (p.Head)
        {
            case "t":
                return groups.IsEmpty
                    ? P("translocation", "part", ("chromosomes", chromosomes.Chromosomes(p.Chromosomes)), ("breaks", "")) + "; " + ctx.NoneGiven(p)
                    : P("translocation", "part", ("chromosomes", chromosomes.Chromosomes(p.Chromosomes)), ("breaks", " " + bands.WithBreaks(groups)));

            case "inv" when bps.Count == 2:
                {
                    var kind = bps[0].Arm == bps[1].Arm ? "paracentric" : "pericentric";
                    var (from, to) = Ordered(bps[0], bps[1]);
                    return P("inversion", kind + "_part", ("chromosome", c), ("from", bands.Band(from)), ("to", bands.Band(to)));
                }

            case "del" when bps.Count == 1:
                return P("deletion", "terminal_part", ("chromosome", c), ("break", bands.BreakAt(bps[0])));

            case "del" when bps.Count == 2:
                {
                    var (from, to) = Ordered(bps[0], bps[1]);
                    return P("deletion", "interstitial_part", ("chromosome", c), ("from", bands.Band(from)), ("to", bands.Band(to)));
                }

            case "dup" when bps.Count == 2:
                {
                    var (from, to) = Ordered(bps[0], bps[1]);
                    return P("duplication", IsAscending(bps[0], bps[1]) ? "direct_part" : "inverted_part", ("chromosome", c), ("from", bands.Band(from)), ("to", bands.Band(to)));
                }

            case "ins":
                return InsertionPart(p);

            case "add" when bps.Count == 1:
                return P("additional_material", "part", ("band", bands.Band(bps[0])));

            case "hsr":
                return bps.Count switch
                {
                    0 => P("hsr", "part_unknown"),
                    1 => bps[0].Landmark == "?" ? P("hsr", "part_unknown") : P("hsr", "part", ("band", bands.Band(bps[0]))),
                    _ => P("hsr", "part_junction", ("first", bands.Band(bps[0])), ("second", bands.Band(bps[1]))),
                };

            case "r":
                return P("ring", "part", ("chromosomes", chromosomes.Chromosomes(p.Chromosomes)))
                    + (groups.IsEmpty ? "; " + ctx.NoneGiven(p) : ", " + bands.WithBreaks(groups));

            case "i" when bps.Count == 1:
                return P("isochromosome", "part", ("chromosome", c), ("arm", bands.Arm(bps[0].Arm)));

            case "fra" when bps.Count == 1:
                return P("fragile_site", "part", ("band", bands.Band(bps[0])));

            default:
                {
                    var d = symbols.Single(p);
                    return P("derivative", "article", ("item", Lower(d.Head) + d.Rest));
                }
        }
    }

    private string InsertionPart(AbnormalityPart p)
    {
        var groups = p.Groups;
        if (p.Chromosomes.IsEmpty)
        {
            return P("derivative", "article", ("item", ctx.Written(p)));
        }

        if (p.Chromosomes.Length == 1)
        {
            var c = p.Chromosomes[0].Name;
            var bps = groups.SelectMany(g => g.Breakpoints).ToList();
            if (bps.Count < 3)
            {
                return P("insertion", "within_part_none", ("chromosome", c)) + "; " + ctx.NoneGiven(p);
            }

            var (site, from, to) = (bps[0], bps[1], bps[2]);
            var (f, t) = Ordered(from, to);
            return P(
                "insertion",
                IsAscending(from, to) ? "within_part" : "inverted_within_part",
                ("chromosome", c),
                ("segment_arm", bands.Arm(f.Arm, "hyphenated")),
                ("site_arm", bands.Arm(site.Arm)),
                ("from", bands.Band(f)),
                ("to", bands.Band(t)),
                ("site", bands.Band(site)));
        }

        var receiver = p.Chromosomes[0];
        var donor = p.Chromosomes[1];
        if (groups.Length < 2 || groups[1].Breakpoints.IsEmpty)
        {
            return P("insertion", "none_part", ("donor", chromosomes.Chromosome(donor)), ("receiver", chromosomes.Chromosome(receiver))) + "; " + ctx.NoneGiven(p);
        }

        var site2 = groups[0].Breakpoints.FirstOrDefault();
        var donorBps = groups[1].Breakpoints;
        if (donor.IsUnknown || donorBps.All(b => !IsKnown(b)))
        {
            return P("insertion", "unknown_material_part", ("receiver", chromosomes.Chromosome(receiver)), ("site", site2 is null ? "?" : bands.Band(site2)));
        }

        var (sf, st) = Ordered(donorBps[0], donorBps[^1]);
        var into = receiver.IsUnknown || site2 is null || !IsKnown(site2)
            ? P("insertion", "into_unknown")
            : P("insertion", "into", ("receiver", chromosomes.Chromosome(receiver)), ("site", bands.Band(site2)));
        return P(
            "insertion",
            IsAscending(donorBps[0], donorBps[^1]) ? "direct_part" : "inverted_part",
            ("from", bands.Band(sf)),
            ("to", bands.Band(st)),
            ("donor", chromosomes.Chromosome(donor)),
            ("into", into));
    }

    private string P(string construct, string? when, params (string Slot, string Value)[] slots) => ctx.P(construct, when, slots);
}
