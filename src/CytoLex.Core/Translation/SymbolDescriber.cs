using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.BandPhrases;
using static CytoLex.Core.Translation.Phrasing;
using Desc = CytoLex.Core.Translation.AbnormalityDescription;

namespace CytoLex.Core.Translation;

/// <summary>
/// Describes symbol-led abnormalities of one symbol group (ch. 9; not derivatives): one description per head symbol, in
/// the short or the detailed system, plus markers, double minutes, rings of unknown origin and clone references.
/// </summary>
internal sealed class SymbolDescriber(RenderContext ctx, ChromosomePhrases chromosomes, BandPhrases bands, CompositionRenderer composition)
{
    /// <summary>An abnormality of one symbol group.</summary>
    public Desc Single(AbnormalityPart p)
    {
        if (p.IsDetailed)
        {
            return Detailed(p);
        }

        var groups = p.Groups;
        var bps = groups.SelectMany(g => g.Breakpoints).ToList();
        var c = p.Chromosomes.IsEmpty ? "?" : p.Chromosomes[0].Name;
        switch (p.Head)
        {
            case "del":
                return Deletion(p, c, bps);

            case "dup":
            case "trp":
            case "qdp":
                return Repeat(p, c, bps);

            case "inv":
                if (bps.Count < 2)
                {
                    return new Desc(P("inversion", "none_head", ("chromosome", c)), "; " + ctx.NoneGiven(p));
                }

                {
                    var kind = bps[0].Arm == bps[1].Arm ? "paracentric" : "pericentric";
                    var (from, to) = Ordered(bps[0], bps[1]);
                    return new Desc(
                        P("inversion", kind + "_head", ("chromosome", c)),
                        P("inversion", kind + "_rest", ("from", bands.Band(from)), ("to", bands.Band(to))),
                        P("inversion", kind + "_gain", ("chromosome", c)));
                }

            case "i":
                if (bps.Count == 0)
                {
                    return new Desc(P("isochromosome", "none_head", ("chromosome", c)), "; " + ctx.NoneGiven(p));
                }

                return new Desc(
                    P("isochromosome", "head", ("chromosome", c), ("arm", bands.Arm(bps[0].Arm))),
                    P("isochromosome", "rest", ("arm", bands.Arm(bps[0].Arm))));

            case "idic":
                {
                    var psu = p.Symbols.Any(s => s.Symbol == "psu") ? "psu_" : "";
                    if (bps.Count == 0)
                    {
                        return new Desc(P("isodicentric", psu + "head", ("chromosome", c)), "; " + ctx.NoneGiven(p));
                    }

                    return new Desc(
                        P("isodicentric", psu + "head", ("chromosome", c)),
                        P("isodicentric", psu + "rest", ("chromosome", c), ("band", bands.Band(bps[0]))));
                }

            case "dic":
            case "trc":
                return Centric(p, c);

            case "t":
                return Translocation(p);

            case "ins":
                return Insertion(p);

            case "add":
                if (bps.Count == 0)
                {
                    return new Desc(P("additional_material", "none_head", ("chromosome", c)), "; " + ctx.NoneGiven(p));
                }

                return new Desc(
                    P("additional_material", "head", ("chromosome", c), ("band", bands.Band(bps[0]))),
                    bps[0].Arm is null ? "" : P("additional_material", "rest", ("band", bands.Band(bps[0])), ("arm", bands.Arm(bps[0].Arm))));

            case "hsr":
                return bps.Count == 0
                    ? new Desc(P("hsr", "head_unknown", ("chromosome", c)))
                    : new Desc(P("hsr", "head", ("chromosome", c), ("band", bands.Band(bps[0]))));

            case "fra":
                return new Desc(P("fragile_site", "head", ("chromosome", c), ("band", bps.Count == 0 ? "?" : bands.Band(bps[0]))));

            case "fis":
                return bps.Count == 0
                    ? new Desc(P("fission", "head", ("chromosome", c)), "; " + ctx.NoneGiven(p))
                    : new Desc(P("fission", "head", ("chromosome", c)), P("fission", "rest", ("arm", bands.Arm(bps[0].Arm)), ("band", bands.Band(bps[0]))));

            case "neo":
                return bps.Count == 0
                    ? new Desc(P("neocentromere", "head", ("chromosome", c)))
                    : new Desc(P("neocentromere", "head_at", ("chromosome", c), ("band", bands.Band(bps[0]))));

            case "tas":
                return new Desc(
                    P("telomeric_association", "head", ("chromosomes", chromosomes.Chromosomes(p.Chromosomes))),
                    groups.IsEmpty ? "; " + ctx.NoneGiven(p) : P("telomeric_association", "rest", ("breakpoints", bands.BreakpointList(groups))));

            case "rob":
                return new Desc(
                    P("robertsonian", "head", ("chromosomes", chromosomes.Chromosomes(p.Chromosomes))),
                    groups.IsEmpty ? "; " + ctx.NoneGiven(p) : P("robertsonian", "rest", ("breakpoints", bands.BreakpointList(groups))));

            case "inc":
                return new Desc(P("incomplete", null));

            default:
                return new Desc(ctx.Written(p));
        }
    }

    /// <summary><c>r(7)(p22q36)</c>, <c>r(1;3)(…)</c>, <c>dic r(…)</c>, <c>+r</c>, <c>+5r</c>, <c>r1</c>.</summary>
    public Desc Ring(AbnormalityPart p)
    {
        if (p.IsDetailed)
        {
            return Detailed(p);
        }

        if (p.Chromosomes.IsEmpty)
        {
            if (p.SymbolNumber is { } number)
            {
                var numbered = P("ring", "numbered", ("number", N(number)));
                return new Desc(numbered, GainNoun: Lower(numbered));
            }

            return new Desc(P("ring", "unknown_origin"), GainNoun: P("ring", "unknown_origin_noun"), Plural: P("ring", "unknown_origin_plural"));
        }

        var c = p.Chromosomes[0].Name;
        if (p.Chromosomes.Length == 1 && RingPrefix(p) is null)
        {
            var bps = p.Groups.SelectMany(g => g.Breakpoints).ToList();
            return bps.Count < 2
                ? new Desc(P("ring", "one_chromosome", ("chromosome", c)), "; " + ctx.NoneGiven(p))
                : new Desc(P("ring", "one_chromosome", ("chromosome", c)), P("ring", "one_chromosome_rest", ("from", bands.Band(bps[0])), ("to", bands.Band(bps[1]))));
        }

        return new Desc(RingHead(p), p.Groups.IsEmpty ? "; " + ctx.NoneGiven(p) : ", " + bands.WithBreaks(p.Groups));
    }

    /// <summary><c>+mar</c>, <c>+3mar</c>, <c>+1~5mar</c>, <c>+mar1</c>: always a gain, without "additional".</summary>
    public Desc Marker(Abnormality a)
    {
        if (a.LeadingCount is { } count)
        {
            return new Desc(
                count is { IsRange: false, From: 1 } ? P("marker", "one") : P("marker", "counted", ("count", ctx.Count(count))),
                AlwaysGained: true);
        }

        return a.Parts[0].SymbolNumber is { } number
            ? new Desc(P("marker", "numbered", ("number", N(number))), AlwaysGained: true)
            : new Desc(P("marker", "one"), AlwaysGained: true);
    }

    /// <summary><c>dmin</c>, <c>1dmin</c>, <c>~14dmin</c>, <c>9~34dmin</c>.</summary>
    public Desc DoubleMinutes(Abnormality a) => a.LeadingCount switch
    {
        null => new Desc(P("double_minutes", "uncounted"), AlwaysGained: true),
        { IsRange: false, From: 1 } => new Desc(P("double_minutes", "one"), AlwaysGained: true),
        { IsRange: true, From: null, To: { } n } => new Desc(P("double_minutes", "about", ("count", N(n))), AlwaysGained: true),
        var c => new Desc(P("double_minutes", "counted", ("count", ctx.Count(c))), AlwaysGained: true),
    };

    /// <summary><c>idem</c>, <c>sl</c>, <c>sdl1</c>, <c>idem×2</c> (§11.1.4): the abnormalities of the referenced cell line.</summary>
    public Desc CloneReference(Abnormality a)
    {
        var symbol = a.Parts[0].Head!;
        var target = ctx.Line?.BasedOn is { TargetIndex: { } t } ? N(t + 1) : null;
        var head = target is null
            ? P("clone_reference", symbol + "_unresolved")
            : P("clone_reference", symbol, ("line", target));
        var rest = a.Copies?.Count is { } copies and > 1 ? "; " + P("clone_reference", "multiplied", ("copies", N(copies))) : "";
        return new Desc(head, rest, AlwaysGained: true);
    }

    private Desc Deletion(AbnormalityPart p, string c, List<Breakpoint> bps)
    {
        if (bps.Count == 0)
        {
            return p.Chromosomes is [{ Arm: { } arm }]
                ? new Desc(P("deletion", "arm_head", ("chromosome", c), ("arm", bands.Arm(arm))))
                : new Desc(P("deletion", "none_head", ("chromosome", c)), "; " + ctx.NoneGiven(p));
        }

        if (bps.Count == 1)
        {
            var b = bps[0];
            if (b.IsQuestioned && b.BandNumber is null && b.Text == b.Arm + "?")
            {
                return new Desc(P("deletion", "unknown_head", ("chromosome", c), ("arm", bands.Arm(b.Arm))), P("deletion", "unknown_rest"));
            }

            return new Desc(
                P("deletion", "terminal_head", ("chromosome", c)),
                P("deletion", "terminal_rest", ("break", bands.BreakAt(b)), ("arm", bands.Arm(b.Arm))),
                P("deletion", "terminal_gain", ("chromosome", c)));
        }

        if (bps[0].Name is { } same && same == bps[1].Name)
        {
            return new Desc(
                P("deletion", "within_band_head", ("band", same)),
                GainNoun: P("deletion", "within_band_gain", ("chromosome", c), ("band", same)));
        }

        var (from, to) = Ordered(bps[0], bps[1]);
        return new Desc(
            P("deletion", "interstitial_head", ("chromosome", c)),
            P("deletion", "interstitial_rest", ("from", bands.Band(from)), ("to", bands.Band(to))),
            P("deletion", "interstitial_gain", ("chromosome", c)));
    }

    /// <summary><c>dup</c>, <c>trp</c>, <c>qdp</c>.</summary>
    private Desc Repeat(AbnormalityPart p, string c, List<Breakpoint> bps)
    {
        var construct = p.Head switch
        {
            "dup" => "duplication",
            "trp" => "triplication",
            _ => "quadruplication",
        };
        if (bps.Count < 2)
        {
            return p.Chromosomes is [{ Arm: { } arm }]
                ? new Desc(P(construct, "arm_head", ("chromosome", c), ("arm", bands.Arm(arm))))
                : new Desc(P(construct, "none_head", ("chromosome", c)), "; " + ctx.NoneGiven(p));
        }

        var ascending = IsAscending(bps[0], bps[1]);
        var (from, to) = Ordered(bps[0], bps[1]);
        if (p.Head == "dup")
        {
            var kind = ascending ? "direct" : "inverted";
            return new Desc(
                P(construct, kind + "_head", ("chromosome", c)),
                P(construct, kind + "_rest", ("from", bands.Band(from)), ("to", bands.Band(to))),
                P(construct, kind + "_gain", ("chromosome", c)));
        }

        var rest = ascending
            ? P(construct, "rest", ("from", bands.Band(from)), ("to", bands.Band(to)))
            : P(construct, "rest_reversed", ("from", bands.Band(from)), ("to", bands.Band(to)), ("written_from", bands.Band(bps[0])), ("written_to", bands.Band(bps[1])));
        return new Desc(P(construct, "head", ("chromosome", c)), rest, P(construct, "gain", ("chromosome", c)));
    }

    /// <summary><c>dic</c>, <c>trc</c>, <c>psu dic</c> (not rings).</summary>
    private Desc Centric(AbnormalityPart p, string c)
    {
        var construct = p.Head == "dic" ? "dicentric" : "tricentric";
        var psu = p.Symbols.Any(s => s.Symbol == "psu");
        var head = p.Chromosomes.Length == 1
            ? P(construct, "head_one", ("chromosome", c))
            : P(psu ? "pseudodicentric" : construct, "head", ("chromosomes", chromosomes.Chromosomes(p.Chromosomes)));
        var rest = p.Groups.IsEmpty ? "; " + ctx.NoneGiven(p) : ", " + bands.WithBreaks(p.Groups);
        if (psu)
        {
            rest += "; " + P("pseudodicentric", "active", ("chromosome", c));
        }

        return new Desc(head, rest);
    }

    /// <summary><c>dicentric</c>/<c>tricentric</c> before <c>r</c>; <c>null</c> without.</summary>
    private string? RingPrefix(AbnormalityPart p)
    {
        if (p.Symbols.Any(s => s.Symbol == "dic"))
        {
            return P("dicentric", "adjective");
        }

        return p.Symbols.Any(s => s.Symbol == "trc") ? P("tricentric", "adjective") : null;
    }

    /// <summary><c>Ring chromosome formed from chromosomes 1 and 3</c>, <c>Dicentric ring chromosome formed from …</c>.</summary>
    private string RingHead(AbnormalityPart p)
    {
        var list = chromosomes.Chromosomes(p.Chromosomes);
        return RingPrefix(p) is { } prefix
            ? P("ring", "prefixed_head", ("prefix", Cap(prefix)), ("chromosomes", list))
            : P("ring", "head", ("chromosomes", list));
    }

    private Desc Translocation(AbnormalityPart p)
    {
        var list = chromosomes.Chromosomes(p.Chromosomes);
        var head = P("translocation", "head", ("chromosomes", list));
        var groups = p.Groups;
        if (groups.IsEmpty)
        {
            return new Desc(head, "; " + ctx.NoneGiven(p));
        }

        if (groups.Length != p.Chromosomes.Length)
        {
            return new Desc(head, ", " + bands.WithBreaks(groups));
        }

        if (groups.Length == 2 && groups.All(g => g.Breakpoints is [{ BandNumber: "10" }]))
        {
            return WholeArmTranslocation(p, list);
        }

        if (groups.All(g => g.Breakpoints.Length == 1))
        {
            var with = ", " + bands.WithBreaks(groups);
            if (groups.Length == 2)
            {
                return new Desc(head, with + "; " + P("translocation", "two_way_rest"));
            }

            var moves = new List<string>();
            for (var i = 0; i < groups.Length; i++)
            {
                moves.Add(P(
                    "translocation",
                    "move_distal",
                    ("chromosome", chromosomes.Chromosome(p.Chromosomes[i])),
                    ("band", bands.Band(groups[i].Breakpoints[0])),
                    ("target", chromosomes.Chromosome(p.Chromosomes[(i + 1) % p.Chromosomes.Length]))));
            }

            return new Desc(head, with + ": " + SerialAnd(moves));
        }

        var segments = groups.Select(g =>
        {
            if (g.Breakpoints.Length != 2)
            {
                return bands.Band(g.Breakpoints[0]);
            }

            var (from, to) = Ordered(g.Breakpoints[0], g.Breakpoints[1]);
            return P("translocation", "segment", ("from", bands.Band(from)), ("to", bands.Band(to)));
        }).ToList();
        if (groups.Length == 2)
        {
            return new Desc(head, ": " + P("translocation", "segments_exchanged", ("first", segments[0]), ("second", segments[1])));
        }

        var segmentMoves = segments.Select((s, i) => P(
            "translocation",
            "move_segment",
            ("segment", s),
            ("target", chromosomes.Chromosome(p.Chromosomes[(i + 1) % p.Chromosomes.Length])))).ToList();
        return new Desc(head, ": " + SerialAnd(segmentMoves));
    }

    /// <summary><c>t(1;3)(p10;q10)</c>, <c>t(1;3)(p10;p10)</c>.</summary>
    private Desc WholeArmTranslocation(AbnormalityPart p, string list)
    {
        var (a, b) = (p.Groups[0].Breakpoints[0], p.Groups[1].Breakpoints[0]);
        var head = P("translocation", "whole_arm_head", ("chromosomes", list));
        var breakpoints = And([bands.Band(a), bands.Band(b)]);
        if (a.Arm == b.Arm)
        {
            return new Desc(head, P(
                "translocation",
                "whole_arm_rest_same",
                ("breakpoints", breakpoints),
                ("arms", bands.Arm(a.Arm, "plural")),
                ("other_arms", bands.Arm(Opposite(a.Arm), "plural"))));
        }

        return new Desc(head, P(
            "translocation",
            "whole_arm_rest",
            ("breakpoints", breakpoints),
            ("arm1", bands.Arm(a.Arm)),
            ("chromosome1", chromosomes.Chromosome(p.Chromosomes[0])),
            ("arm2", bands.Arm(b.Arm)),
            ("chromosome2", chromosomes.Chromosome(p.Chromosomes[1])),
            ("other_arm2", bands.Arm(Opposite(b.Arm))),
            ("other_arm1", bands.Arm(Opposite(a.Arm)))));
    }

    private Desc Insertion(AbnormalityPart p)
    {
        var groups = p.Groups;
        if (p.Chromosomes.IsEmpty)
        {
            return new Desc(ctx.Written(p));
        }

        if (p.Chromosomes.Length == 1)
        {
            var c = p.Chromosomes[0].Name;
            var bps = groups.SelectMany(g => g.Breakpoints).ToList();
            if (bps.Count < 3)
            {
                return new Desc(P("insertion", "within_head", ("chromosome", c)), "; " + ctx.NoneGiven(p));
            }

            var (site, from, to) = (bps[0], bps[1], bps[2]);
            var kind = IsAscending(from, to) ? "direct" : "inverted";
            var (f, t) = Ordered(from, to);
            return new Desc(
                P(
                    "insertion",
                    kind + "_within_head",
                    ("chromosome", c),
                    ("segment_arm", bands.Arm(f.Arm, "hyphenated")),
                    ("site_arm", bands.Arm(site.Arm)),
                    ("from", bands.Band(f)),
                    ("to", bands.Band(t)),
                    ("site", bands.Band(site))),
                kind == "inverted" ? "; " + P("insertion", "reversed") : "");
        }

        var receiver = p.Chromosomes[0];
        var donor = p.Chromosomes[1];
        if (groups.Length < 2)
        {
            return new Desc(
                P("insertion", "none_head", ("donor", chromosomes.Chromosome(donor)), ("receiver", chromosomes.Chromosome(receiver))),
                "; " + ctx.NoneGiven(p));
        }

        var siteBp = groups[0].Breakpoints.FirstOrDefault();
        var donorBps = groups[1].Breakpoints;
        if (donor.IsUnknown || donorBps.All(b => !IsKnown(b)))
        {
            return new Desc(P("insertion", "unknown_material_head", ("receiver", chromosomes.Chromosome(receiver)), ("site", siteBp is null ? "?" : bands.Band(siteBp))));
        }

        if (donorBps.Length < 2)
        {
            return new Desc(ctx.Written(p));
        }

        var direct = IsAscending(donorBps[0], donorBps[1]);
        var (sf, st) = Ordered(donorBps[0], donorBps[1]);
        return new Desc(
            P(
                "insertion",
                direct ? "direct_head" : "inverted_head",
                ("from", bands.Band(sf)),
                ("to", bands.Band(st)),
                ("donor", chromosomes.Chromosome(donor)),
                ("receiver", chromosomes.Chromosome(receiver)),
                ("site", siteBp is null ? "?" : bands.Band(siteBp))),
            direct ? "" : "; " + P("insertion", "reversed"));
    }

    /// <summary>The detailed system for a symbol group: a head plus its band composition(s).</summary>
    private Desc Detailed(AbnormalityPart p)
    {
        var c = p.Chromosomes.IsEmpty ? "?" : p.Chromosomes[0].Name;
        var list = chromosomes.Chromosomes(p.Chromosomes);
        if (p.Groups.Length >= 2)
        {
            return DetailedGroups(p, list);
        }

        var structure = p.Groups[0].Structure!;
        if (p.Head == "tas")
        {
            return new Desc(P("telomeric_association", "head", ("chromosomes", list)), ": " + composition.TasChain(structure));
        }

        if (p.Head == "del" && DetailedDeletion(structure, c) is { } deletion)
        {
            return deletion;
        }

        var segments = composition.Composition(structure, null, out var ring);
        var (first, join, suffix) = DetailedHead(p, c, list);

        // A ring symbol already says "ring": its composition ends "joined back to the start" without "a ring made up of".
        var madeUp = ring && p.Head != "r"
            ? ": " + P("band_composition", "ring", ("segments", segments))
            : join switch
            {
                Join.Colon => ": " + segments,
                Join.Comma => ", " + P("band_composition", "made_up_of", ("segments", segments)),
                _ => " " + P("band_composition", "made_up_of", ("segments", segments)),
            };
        return new Desc(first, madeUp + suffix);
    }

    /// <summary>How a detailed head is joined to its composition.</summary>
    private enum Join
    {
        /// <summary><c>Duplication of chromosome 1 made up of …</c>.</summary>
        Space,

        /// <summary><c>Dicentric chromosome formed from …, made up of …</c>.</summary>
        Comma,

        /// <summary><c>Chromosome 1 with a homogeneously staining region: the segment …</c>.</summary>
        Colon,
    }

    private (string Head, Join Join, string Suffix) DetailedHead(AbnormalityPart p, string c, string list)
    {
        var psu = p.Symbols.Any(s => s.Symbol == "psu");
        return p.Head switch
        {
            "del" => (P("deletion", "detailed_head", ("chromosome", c)), Join.Space, ""),
            "dup" => (P("duplication", "detailed_head", ("chromosome", c)), Join.Space, ""),
            "trp" => (P("triplication", "head", ("chromosome", c)), Join.Space, ""),
            "qdp" => (P("quadruplication", "head", ("chromosome", c)), Join.Space, ""),
            "inv" => (P("inversion", "detailed_head", ("chromosome", c)), Join.Space, ""),
            "ins" => (P("insertion", "within_head", ("chromosome", c)), Join.Space, ""),
            "i" => (P("isochromosome", "detailed_head", ("chromosome", c)), Join.Space, ""),
            "idic" => (P("isodicentric", psu ? "psu_head" : "head", ("chromosome", c)), Join.Space, psu ? "; " + P("isodicentric", "psu_active") : ""),
            "r" when p.Chromosomes.Length == 1 && RingPrefix(p) is null => (P("ring", "one_chromosome", ("chromosome", c)), Join.Space, ""),
            "r" => (RingHead(p), Join.Comma, ""),
            "dic" or "trc" when p.Chromosomes.Length == 1 => (P(p.Head == "dic" ? "dicentric" : "tricentric", "head_one", ("chromosome", c)), Join.Comma, ""),
            "dic" or "trc" => (
                P(psu ? "pseudodicentric" : p.Head == "dic" ? "dicentric" : "tricentric", "head", ("chromosomes", list)),
                Join.Comma,
                psu ? "; " + P("pseudodicentric", "active", ("chromosome", c)) : ""),
            "add" => (P("additional_material", "detailed_head", ("chromosome", c)), Join.Colon, ""),
            "hsr" => (P("hsr", "detailed_head", ("chromosome", c)), Join.Colon, ""),
            "neo" => (P("neocentromere", "head", ("chromosome", c)), Join.Comma, ""),
            "fis" => (P("fission", "head", ("chromosome", c)), Join.Colon, ""),
            _ => (ctx.Written(p), Join.Space, ""),
        };
    }

    /// <summary>A detailed composition per chromosome (<c>t(2;5)(2pter→2q21::5q31→5qter;5pter→5q31::2q21→2qter)</c>).</summary>
    private Desc DetailedGroups(AbnormalityPart p, string list)
    {
        var head = p.Head switch
        {
            "t" when p.Groups.Length == 2 && p.Groups.All(g => g.Breakpoints.Any(b => b.BandNumber == "10")) => P("translocation", "whole_arm_head", ("chromosomes", list)),
            "t" => P("translocation", "head", ("chromosomes", list)),
            "ins" => P("insertion", "between_head", ("chromosomes", list)),
            _ => ctx.Written(p),
        };
        var groups = p.Groups.Select(g => P(
            "band_composition",
            "group",
            ("chromosome", g.Chromosome is { } gc ? chromosomes.Chromosome(gc) : "?"),
            ("segments", g.Structure is { } s ? composition.Composition(s, g.Chromosome, out _) : bands.BreakpointList([g])))).ToList();
        return new Desc(head, ": " + string.Join("; ", groups));
    }

    /// <summary>
    /// A deletion in the detailed system: one piece ending in a break (terminal: <c>pter→q13:</c>) or two pieces joined
    /// (interstitial: <c>pter→q13::q33→qter</c>, within one band: <c>pter→q13::q13→qter</c>); <c>null</c> otherwise.
    /// </summary>
    private Desc? DetailedDeletion(DerivativeStructure s, string c)
    {
        var track = new CompositionRenderer.Track();
        if (s.Pieces.Length == 1 && s.TrailingJoin == JoinKind.Break && s.LeadingJoin is null)
        {
            var remaining = composition.Piece(s.Pieces[0], null, false, true, track);
            return new Desc(P("deletion", "detailed_terminal", ("chromosome", c), ("segments", remaining)));
        }

        if (s.Pieces.Length == 2 && s.Joins is [JoinKind.Junction] && s.LeadingJoin is null && s.TrailingJoin is null)
        {
            var first = composition.Piece(s.Pieces[0], null, false, false, track);
            var second = composition.Piece(s.Pieces[1], null, false, false, track);
            var (end, start) = (s.Pieces[0].To, s.Pieces[1].From);
            return end.Name is { } band && band == start.Name
                ? new Desc(P("deletion", "detailed_within_band", ("band", band), ("first", first), ("second", second)))
                : new Desc(P("deletion", "detailed_interstitial", ("chromosome", c), ("first", first), ("second", second)));
        }

        return null;
    }

    private string P(string construct, string? when, params (string Slot, string Value)[] slots) => ctx.P(construct, when, slots);
}
