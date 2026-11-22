using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.BandPhrases;

namespace CytoLex.Core.Translation;

/// <summary>
/// Band compositions of the detailed system (§4.3.2): <c>the segment from the end of the short arm to 6q25.2, joined to the
/// segment from 6p22.2 back to the end of the short arm</c> (templates: segment, break, break_reunion, ring, arm ends, …).
/// </summary>
internal sealed class CompositionRenderer(RenderContext ctx, ChromosomePhrases chromosomes, BandPhrases bands)
{
    /// <summary>What a composition has seen so far: the arm ends it started from ("back to") and its neocentromere clauses.</summary>
    public sealed class Track
    {
        public List<(string? Chromosome, string Landmark)> Terminals { get; } = [];

        public List<string> Neocentromeres { get; } = [];
    }

    /// <summary>
    /// The composition of <paramref name="s"/>. <paramref name="group"/> is set inside a multi-group composition
    /// (<c>its short arm</c>); <paramref name="ring"/> tells whether it is a ring (<c>::…::</c>).
    /// </summary>
    public string Composition(DerivativeStructure s, ChromosomeRef? group, out bool ring)
    {
        ring = s.LeadingJoin == JoinKind.Junction && s.TrailingJoin == JoinKind.Junction;
        var track = new Track();
        var pieces = new List<string>();
        for (var i = 0; i < s.Pieces.Length; i++)
        {
            var leadingBreak = i == 0 && s.LeadingJoin == JoinKind.Break;
            var trailingBreak = i == s.Pieces.Length - 1 && s.TrailingJoin == JoinKind.Break;
            pieces.Add(Piece(s.Pieces[i], group, leadingBreak, trailingBreak, track));
        }

        var joined = string.Join(", " + ctx.P("break_reunion", null) + " ", pieces);
        if (ring)
        {
            joined += ", " + ctx.P("ring", pieces.Count == 1 ? "ends_joined" : "joined_back");
        }

        foreach (var clause in track.Neocentromeres)
        {
            joined += ", " + clause;
        }

        return joined;
    }

    /// <summary>One piece: <c>the segment from 3q21 to 3q29</c>, <c>a homogeneously staining region</c>, <c>unknown material</c>.</summary>
    public string Piece(SegmentPiece piece, ChromosomeRef? group, bool leadingBreak, bool trailingBreak, Track track)
    {
        var points = piece.Points;
        if (points.Length == 1)
        {
            return points[0].Landmark switch
            {
                "hsr" => ctx.P("hsr", "piece"),
                "?" => ctx.P("unknown_designation", "material"),
                _ => Point(points[0], group, false, track),
            };
        }

        if (points is [{ Landmark: "hsr" }, { Landmark: "?" }])
        {
            return ctx.P("hsr", "piece_unidentified");
        }

        if (points.Length >= 3 && points[0].Landmark == "?" && points[^1].Landmark == "?" && points.Any(p => p.Landmark == "cen"))
        {
            return ctx.P("centromere", "unidentified_segment");
        }

        // A neocentromere inside the piece (q28→neo→q28): the piece runs from its first to its last point.
        for (var j = 1; j < points.Length - 1; j++)
        {
            if (points[j].Landmark == "neo")
            {
                track.Neocentromeres.Add(ctx.P("neocentromere", "clause", ("band", bands.Band(points[j - 1]))));
            }
        }

        var from = leadingBreak ? ctx.P("break", null, ("band", bands.Band(points[0]))) : Point(points[0], group, false, track);
        var last = points[^1];

        // "back to" an arm end the composition started from, when the chromosome is implied (pter→q25.2::p22.2→pter).
        var back = last.IsTerminal && !last.ChromosomeWritten && track.Terminals.Contains((ChromosomeName(last), last.Landmark!));
        var to = trailingBreak ? ctx.P("break", null, ("band", bands.Band(last))) : Point(last, group, back, track);
        return ctx.P("segment", back ? "from_back_to" : "from_to", ("from", from), ("to", to));
    }

    /// <summary>A telomeric association chain: <c>chromosome 12 from the end of the short arm to the end of the long arm, joined to …</c>.</summary>
    public string TasChain(DerivativeStructure s)
    {
        var points = s.Pieces.SelectMany(piece => piece.Points).ToList();
        var parts = new List<string>();
        for (var i = 0; i + 1 < points.Count; i += 2)
        {
            var (from, to) = (points[i], points[i + 1]);
            parts.Add(ctx.P(
                "telomeric_association",
                "chain_part",
                ("chromosome", from.Chromosome is { } c ? chromosomes.Chromosome(c) : "?"),
                ("from", TasPoint(from)),
                ("to", TasPoint(to))));
        }

        return string.Join(", " + ctx.P("break_reunion", null) + " ", parts);
    }

    /// <summary>A point of a composition: a band, an arm end (<c>the end of the long arm of chromosome 3</c>), the centromere.</summary>
    private string Point(Breakpoint b, ChromosomeRef? group, bool back, Track track)
    {
        if (b.IsTerminal)
        {
            var construct = ArmEnd(b);
            if (!back)
            {
                track.Terminals.Add((ChromosomeName(b), b.Landmark!));
            }

            if (group is not null && b.Chromosome is { } c && c.Name == group.Name && c.IsUnderlined == group.IsUnderlined)
            {
                return ctx.P(construct, "its");
            }

            return (group is not null || b.ChromosomeWritten) && b.Chromosome is { IsUnknown: false } written
                ? ctx.P(construct, "of_chromosome", ("chromosome", chromosomes.Chromosome(written)))
                : ctx.P(construct, "plain");
        }

        return b.Landmark switch
        {
            "cen" => ctx.P("centromere", "of_chromosome", ("chromosome", b.Chromosome is { } c2 ? chromosomes.Chromosome(c2) : ctx.P("unknown_designation", "chromosome"))),
            "hsr" => ctx.P("hsr", "piece"),
            "?" => ctx.P("unknown_designation", "material"),
            _ => bands.Band(b),
        };
    }

    private string TasPoint(Breakpoint b) => b.IsTerminal ? ctx.P(ArmEnd(b), "plain") : bands.Band(b);

    private static string ArmEnd(Breakpoint b) => b.Landmark switch
    {
        "pter" => "short_arm_end",
        "qter" => "long_arm_end",
        _ => "arm_end",
    };
}
