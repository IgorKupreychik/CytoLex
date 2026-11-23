using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Composing;

/// <summary>
/// The band composition of an abnormality (T-066): computed from the short system, or read from the detailed system.
/// Each rearrangement type traces to the KB rule whose statement defines it (<see cref="Rearrangements"/>); the
/// composition is read from the end <c>STRUCT.COMPOSITION_ORDER</c> names (<see cref="Orientation"/>).
/// </summary>
/// <remarks>
/// No composition (<c>null</c>) for partial abnormalities, uncertain breakpoints (<c>q2?3</c>, <c>~</c>, <c>or</c>),
/// symbols without one (<see cref="WithoutComposition"/>), and bands that can't be placed in the chromosome being built.
/// </remarks>
internal sealed class Composer(Func<string, bool> hasRule)
{
    /// <summary>Symbols that have no band composition: markers, minutes, fragile sites, disomy, neocentromeres, incomplete karyotypes, clone references.</summary>
    public static readonly ImmutableHashSet<string> WithoutComposition = ["mar", "dmin", "ace", "fra", "upd", "neo", "inc", "idem", "sl", "sdl"];

    private const string _shortAndDetailed = "STRUCT.SHORT_AND_DETAILED_SYSTEMS";
    private const string _detailed = "STRUCT.DETAILED_SYSTEM";

    /// <summary>The composition of <paramref name="a"/>, or <c>null</c> when it has none.</summary>
    public AbnormalityComposition? Compose(Abnormality a)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (a.IsPartial || a.Parts.IsEmpty || a.Kind is null || WithoutComposition.Contains(a.Kind))
        {
            return null;
        }

        var rules = new List<string>();
        List<ComposedChromosome>? products;
        CompositionSource source;
        if (a.Parts.Any(p => p.IsDetailed))
        {
            products = Written(a);
            source = CompositionSource.Written;
            rules.Add(_detailed);
        }
        else
        {
            products = a.Kind is "der" or "rec" or "ider" ? Derivative(a, rules) : a.Parts.Length == 1 ? Single(a.Parts[0], rules) : null;
            source = CompositionSource.Computed;
            rules.Insert(0, _shortAndDetailed);
            rules.Add(Orientation.Rule);
        }

        if (products is not { Count: > 0 })
        {
            return null;
        }

        return new AbnormalityComposition(
            [.. products],
            source,
            Format(a, products),
            [.. rules.Distinct(StringComparer.Ordinal).Where(hasRule).Select(KbRef.Rule)]);
    }

    // ── Detailed system ──────────────────────────────────────────────────────────────────────────

    private static List<ComposedChromosome>? Written(Abnormality a)
    {
        var products = new List<ComposedChromosome>();
        foreach (var structure in a.Parts.Where(p => p.IsDetailed).SelectMany(p => p.Groups).Select(g => g.Structure))
        {
            if (structure is null || ComposedChromosome.FromWritten(structure) is not { } composed)
            {
                return null;
            }

            products.Add(composed);
        }

        return products;
    }

    // ── One rearrangement ────────────────────────────────────────────────────────────────────────

    private static List<ComposedChromosome>? Single(AbnormalityPart part, List<string> rules)
    {
        if (Groups(part) is not { Count: > 0 } groups || part.Head is not { } head)
        {
            return null;
        }

        var c = groups[0].Chromosome;
        switch (head)
        {
            case "del" when groups is [{ Points.Length: 1 or 2 } g]:
                rules.Add("DEL.FORMAT");
                return One(c, w => Rearrangements.Delete(w, g.Points));
            case "add" when groups is [{ Points: [var band] }]:
                rules.Add("ADD.FORMAT");
                return One(c, w => Rearrangements.Add(w, band));
            case "inv" when groups is [{ Points: [var x, var y] }]:
                rules.Add("INV.FORMAT");
                return One(c, w => Rearrangements.Invert(w, x, y));
            case "dup" or "trp" or "qdp" when groups is [{ Points: [var x, var y] }]:
                rules.Add(head.ToUpperInvariant() + ".FORMAT");
                return One(c, w => Rearrangements.Multiply(w, x, y, head switch { "dup" => 2, "trp" => 3, _ => 4 }));
            case "hsr" when groups is [{ Points: [var band] }]:
                rules.Add("HSR.FORMAT");
                return One(c, w => Rearrangements.InsertHsr(w, band));
            case "fis" when groups is [{ Points: [var band] }] && Rearrangements.IsCentromeric(band):
                rules.Add("FIS.FORMAT");
                return One(c, w => Rearrangements.Fission(w, band));
            case "i" when groups is [{ Points: [var band] }] && Rearrangements.IsCentromeric(band):
                rules.Add("ISO.FORMAT");
                return Oriented(Rearrangements.Mirror(WorkingChromosome.Normal(c), band), c);
            case "idic" when groups is [{ Points: [var band] }]:
                rules.AddRange(["DIC.FORMAT", "ISO.FORMAT"]);
                return Oriented(Rearrangements.Isodicentric(WorkingChromosome.Normal(c), band), c);
            case "ins":
                rules.Add("INS.ORIENTATION");
                return Insertion(groups);
            case "dic":
                rules.Add("DIC.FORMAT");
                if (part.Symbols.Any(s => s.Symbol == "psu"))
                {
                    rules.Add("DIC.PSEUDODICENTRIC_ORDER");
                }

                return Dicentric(groups);
            case "trc" when groups is [{ Points.Length: 1 } g1, { Points.Length: 2 } g2, { Points.Length: 1 } g3]:
                rules.Add("TRC.FORMAT");
                return Join(c, Rearrangements.CentricSide(g1, endsAtBand: true), Rearrangements.Segment(g2, proximalLeft: true), Rearrangements.CentricSide(g3, endsAtBand: false));
            case "r":
                rules.Add("RING.FORMAT");
                if (part.Symbols.Length > 1)
                {
                    rules.Add("RING.DIC_TRC_ORDER");
                }

                return Ring(groups, unknownCentric: false) is { } ring ? [ring] : null;
            case "t":
                rules.Add("TRANSLOC.FORMAT");
                return Translocation(groups, rules);
            case "rob" when WholeArm(groups) is { } rob:
                rules.Add("ROB.FORMAT");
                return [Orientation.Orient(rob.First, c)];
            case "tas" when Telomeric(groups) is { } tas:
                rules.Add("TAS.FORMAT");
                return [tas];
            default:
                return null;
        }
    }

    private static List<ComposedChromosome>? Insertion(List<Group> groups)
    {
        if (groups is [{ Points: [var site, var x, var y] } one])
        {
            var segment = one with { Points = [x, y] };
            return One(one.Chromosome, w => Rearrangements.Delete(w, [x, y]) && Rearrangements.InsertAt(w, site, segment));
        }

        if (groups is not [{ Points: [var receivingSite] } receiving, var donor])
        {
            return null;
        }

        var products = One(receiving.Chromosome, w => Rearrangements.InsertAt(w, receivingSite, donor));
        if (products is null || donor.IsUnknown)
        {
            return products;
        }

        return One(donor.Chromosome, w => Rearrangements.Delete(w, donor.Points)) is [var der] ? [.. products, der] : null;
    }

    private static List<ComposedChromosome>? Dicentric(List<Group> groups)
    {
        // dic(13)(q14q32): sister chromatids, as dic(13;13)(q14;q32)
        if (groups is [{ Points: [var x, var y] } g])
        {
            groups = [g with { Points = [x] }, g with { Points = [y] }];
        }

        if (groups is not [{ Points.Length: 1 } first, { Points.Length: 1 } second])
        {
            return null;
        }

        return Join(first.Chromosome, Rearrangements.CentricSide(first, endsAtBand: true), Rearrangements.CentricSide(second, endsAtBand: false));
    }

    private static List<ComposedChromosome>? Translocation(List<Group> groups, List<string> rules)
    {
        if (groups.Count < 2)
        {
            return null;
        }

        if (WholeArm(groups) is { } arms)
        {
            rules.Add("TRANSLOC.WHOLE_ARM");
            return [Orientation.Orient(arms.First, groups[0].Chromosome), Orientation.Orient(arms.Second, groups[1].Chromosome)];
        }

        // der(c_i) receives from the chromosome before it (§9.2.17.1 ¶1: the second receives from the first, the first from the last)
        var products = new List<ComposedChromosome>();
        for (var i = 0; i < groups.Count; i++)
        {
            if (groups[i].IsUnknown)
            {
                continue;
            }

            var donor = groups[(i - 1 + groups.Count) % groups.Count];
            if (One(groups[i].Chromosome, w => Rearrangements.Receive(w, groups[i], donor, centricDonor: false)) is not [var der])
            {
                return null;
            }

            products.Add(der);
        }

        return products;
    }

    /// <summary>Both arms of a whole-arm exchange (<c>TRANSLOC.WHOLE_ARM</c>): first = the named arms (p10 of the first with the arm of the second), second = the others.</summary>
    private static (ComposedChromosome First, ComposedChromosome Second)? WholeArm(List<Group> groups)
    {
        if (groups is not [{ Points: [var a] } g1, { Points: [var b] } g2] || !Rearrangements.IsCentromeric(a) || !Rearrangements.IsCentromeric(b) || g1.IsUnknown || g2.IsUnknown)
        {
            return null;
        }

        var otherA = BandPoint.OfBand(g1.Chromosome, a.Arm == "p" ? "q" : "p", "10");
        var otherB = BandPoint.OfBand(g2.Chromosome, b.Arm == "p" ? "q" : "p", "10");
        var first = WorkingChromosome.Of([Rearrangements.WholeArm(g1.Chromosome, a, fromCentromere: false), Rearrangements.WholeArm(g2.Chromosome, b, fromCentromere: true)]).ToComposed();
        var second = WorkingChromosome.Of([Rearrangements.WholeArm(g2.Chromosome, otherB, fromCentromere: false), Rearrangements.WholeArm(g1.Chromosome, otherA, fromCentromere: true)]).ToComposed();
        return (first, second);
    }

    /// <summary>
    /// <c>RING.FORMAT</c>: the segment between each group's two bands, in written order, ends joined. A <c>?</c> group is
    /// unknown material, or the unknown chromosome with its centromere in a der(?).
    /// </summary>
    private static ComposedChromosome? Ring(List<Group> groups, bool unknownCentric)
    {
        var pieces = new List<ComposedPiece>();
        foreach (var g in groups)
        {
            if (g.IsUnknown)
            {
                pieces.Add(unknownCentric ? WorkingChromosome.UnknownCentric() : WorkingChromosome.Single(PointKind.Unknown));
                unknownCentric = false;
            }
            else if (g.Points is [var x, var y])
            {
                pieces.Add(WorkingChromosome.Range(x, y));
            }
            else
            {
                return null;
            }
        }

        var w = WorkingChromosome.Of(pieces);
        w.Leading = JoinKind.Junction;
        w.Trailing = JoinKind.Junction;
        return w.ToComposed();
    }

    /// <summary>
    /// <c>TAS.FORMAT</c>: one chain, each chromosome oriented by its terminal bands: the first from its free end to the
    /// associated one, the middle ones from the first named end to the second, the last from the associated end on.
    /// </summary>
    private static ComposedChromosome? Telomeric(List<Group> groups)
    {
        if (groups.Count < 2 || groups.Any(g => g.IsUnknown || g.Points.Any(p => p.Arm is null)))
        {
            return null;
        }

        var points = new List<BandPoint>();
        for (var i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            var (from, to) = i == 0
                ? (Other(g.First.Arm!), g.First.Arm!)
                : i == groups.Count - 1
                    ? (g.Last.Arm!, Other(g.Last.Arm!))
                    : g.Points.Length == 2 ? (g.First.Arm!, g.Last.Arm!) : (null, null);
            if (from is null || to is null)
            {
                return null;
            }

            points.Add(BandPoint.ArmEnd(g.Chromosome, from));
            points.Add(BandPoint.ArmEnd(g.Chromosome, to));
        }

        return new ComposedChromosome(null, [new ComposedPiece([.. points])], [], null);

        static string Other(string arm) => arm == "p" ? "q" : "p";
    }

    // ── Derivatives ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>DER.CENTROMERE_CHROMOSOME</c>: chromosome n changed by each aberration after <c>der(n)</c> in turn;
    /// <c>der(a;b)</c> keeps both centromeres (<c>DER.WHOLE_ARM_TWO_CHROMOSOMES</c> with its own breakpoints);
    /// <c>ider</c> ends with the isochromosome of one arm (<c>DER.ISODERIVATIVE</c>); <c>der(?)</c> starts from the
    /// unknown chromosome with its centromere (<c>DER.UNKNOWN_CENTROMERE</c>).
    /// </summary>
    private static List<ComposedChromosome>? Derivative(Abnormality a, List<string> rules)
    {
        var head = a.Parts[0];
        var chromosomes = head.Chromosomes.Select(ChromosomeId.Of).ToList();
        rules.AddRange(head.Head switch
        {
            "rec" => ["REC.FORMAT", "REC.DEFINITION"],
            "ider" => ["DER.ISODERIVATIVE"],
            _ => ["DER.CENTROMERE_CHROMOSOME", "DER.DEFINITION"],
        });

        if (Groups(head) is not { } headGroups || chromosomes.Count is 0 or > 2)
        {
            return null;
        }

        if (head.Head == "rec")
        {
            return Recombinant(a, chromosomes[0]);
        }

        WorkingChromosome w;
        var pendingCentric = new HashSet<ChromosomeId>();
        BandPoint? mirror = null;
        if (head.Head == "ider")
        {
            if (chromosomes.Count != 1 || headGroups is not [{ Points: [var band] }] || !Rearrangements.IsCentromeric(band))
            {
                return null;
            }

            mirror = band;
            w = WorkingChromosome.Normal(chromosomes[0]);
        }
        else if (headGroups.Count > 0)
        {
            if (WholeArm(headGroups) is not { } arms)
            {
                return null;
            }

            rules.Add("DER.WHOLE_ARM_TWO_CHROMOSOMES");
            w = WorkingChromosome.Of(arms.First.Pieces);
        }
        else if (chromosomes is [{ IsUnknown: true }])
        {
            rules.Add("DER.UNKNOWN_CENTROMERE");
            w = WorkingChromosome.Of([WorkingChromosome.UnknownCentric()]);
        }
        else
        {
            w = WorkingChromosome.Normal(chromosomes[0]);
            pendingCentric.UnionWith(chromosomes.Skip(1));
        }

        for (var i = 1; i < a.Parts.Length; i++)
        {
            var part = a.Parts[i];
            if (Groups(part) is not { Count: > 0 } groups)
            {
                return null;
            }

            if (part.Head == "r")
            {
                rules.Add("RING.FORMAT");
                rules.Add("RING.MONOCENTRIC_AS_DER");
                return i == a.Parts.Length - 1 && Ring(groups, unknownCentric: w.HasUnknownCentric) is { } ring ? [ring] : null;
            }

            if (!Apply(w, part.Head, groups, pendingCentric, rules))
            {
                return null;
            }
        }

        if (mirror is not null)
        {
            if (Rearrangements.Mirror(w, mirror) is not { } mirrored)
            {
                return null;
            }

            w = mirrored;
        }

        return [Orientation.Orient(w.ToComposed(), chromosomes[0].IsUnknown ? null : chromosomes[0])];
    }

    /// <summary>
    /// <c>REC.DEFINITION</c> (§4.5 ¶4): the recombinants of a pericentric inversion inv(n)(pXqY): <c>dup(np)</c> → pter→qY::pX→pter,
    /// <c>dup(nq)</c> → qter→qY::pX→qter (then oriented). Other recombinants (insertions) have no defined composition yet.
    /// </summary>
    private static List<ComposedChromosome>? Recombinant(Abnormality a, ChromosomeId c)
    {
        if (a.Parts is not [_, { Head: "dup", Groups.IsEmpty: true, Chromosomes: [{ Arm: { } arm } dupChromosome] }, { Head: "inv" } inv]
            || ChromosomeId.Of(dupChromosome) != c
            || Groups(inv) is not [{ Points: [{ Arm: "p" } x, { Arm: "q" } y] } g]
            || g.Chromosome != c)
        {
            return null;
        }

        var pieces = arm == "p"
            ? new[] { WorkingChromosome.Range(BandPoint.ArmEnd(c, "p"), y), WorkingChromosome.Range(x, BandPoint.ArmEnd(c, "p")) }
            : [WorkingChromosome.Range(BandPoint.ArmEnd(c, "q"), y), WorkingChromosome.Range(x, BandPoint.ArmEnd(c, "q"))];
        return [Orientation.Orient(WorkingChromosome.Of(pieces).ToComposed(), c)];
    }

    /// <summary>One aberration of a derivative applied to the chromosome being built.</summary>
    private static bool Apply(WorkingChromosome w, string? head, List<Group> groups, HashSet<ChromosomeId> pendingCentric, List<string> rules)
    {
        switch (head)
        {
            case "t":
                rules.Add("TRANSLOC.FORMAT");
                return Receive(w, groups, pendingCentric);
            case "del" when groups is [{ Points.Length: 1 or 2 } g]:
                rules.Add("DEL.FORMAT");
                return Rearrangements.Delete(w, g.Points);
            case "add" when groups is [{ Points: [var band] }]:
                rules.Add("ADD.FORMAT");
                return Rearrangements.Add(w, band);
            case "inv" when groups is [{ Points: [var x, var y] }]:
                rules.Add("INV.FORMAT");
                return Rearrangements.Invert(w, x, y);
            case "dup" or "trp" or "qdp" when groups is [{ Points: [var x, var y] }]:
                rules.Add(head.ToUpperInvariant() + ".FORMAT");
                return Rearrangements.Multiply(w, x, y, head switch { "dup" => 2, "trp" => 3, _ => 4 });
            case "hsr" when groups is [{ Points: [var band] }]:
                rules.AddRange(["HSR.FORMAT", "HSR.REPLACED_SEGMENT_PROXIMAL"]);
                return Rearrangements.InsertHsr(w, band);
            case "hsr" when groups is [{ Points: [var a] }, { Points: [var b] }]:
                rules.AddRange(["HSR.FORMAT", "HSR.INTERFACE_TWO_CHROMOSOMES"]);
                return Rearrangements.InsertHsrAtInterface(w, a, b);
            case "ins":
                rules.AddRange(["INS.ORIENTATION", "DER.INSERTION_POINT_CONVENTION"]);
                return Insert(w, groups);
            default:
                return false;
        }
    }

    private static bool Receive(WorkingChromosome w, List<Group> groups, HashSet<ChromosomeId> pendingCentric)
    {
        for (var i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (!g.IsUnknown && g.Points.Length is 1 or 2 && w.Contains(g.First))
            {
                var donor = groups[(i - 1 + groups.Count) % groups.Count];
                var centric = !donor.IsUnknown && pendingCentric.Remove(donor.Chromosome);
                return Rearrangements.Receive(w, g, donor, centric);
            }
        }

        // der(?)t(?;9)(?;q22): the unknown chromosome receives
        return w.HasUnknownCentric && groups is [{ IsUnknown: true }, var known] && !known.IsUnknown && Rearrangements.ReceiveUnknown(w, known);
    }

    private static bool Insert(WorkingChromosome w, List<Group> groups)
    {
        if (groups is [{ Points: [var site, var x, var y] } one])
        {
            return (!w.Contains(x) || Rearrangements.Delete(w, [x, y])) && Rearrangements.InsertAt(w, site, one with { Points = [x, y] });
        }

        if (groups is not [var receiving, var donor])
        {
            return false;
        }

        if (!receiving.IsUnknown && receiving.Points is [var receivingSite] && w.Contains(receivingSite))
        {
            return Rearrangements.InsertAt(w, receivingSite, donor);
        }

        return !donor.IsUnknown && donor.Points.Length == 2 && w.Contains(donor.First) && Rearrangements.Delete(w, donor.Points);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The breakpoint groups as points; <c>null</c> when a group has no chromosome or an uncertain point.</summary>
    private static List<Group>? Groups(AbnormalityPart part)
    {
        var groups = new List<Group>();
        foreach (var g in part.Groups)
        {
            if (g.Chromosome is not { } c || g.Breakpoints.IsEmpty)
            {
                return null;
            }

            var points = g.Breakpoints.Select(BandPoint.Of).ToList();
            if (points.Any(p => p is null || p.Kind is not (PointKind.Band or PointKind.Unknown)))
            {
                return null;
            }

            groups.Add(new Group(ChromosomeId.Of(c), [.. points.OfType<BandPoint>()]));
        }

        return groups;
    }

    /// <summary>A normal chromosome changed by <paramref name="change"/>, oriented.</summary>
    private static List<ComposedChromosome>? One(ChromosomeId c, Func<WorkingChromosome, bool> change)
    {
        if (c.IsUnknown)
        {
            return null;
        }

        var w = WorkingChromosome.Normal(c);
        return change(w) ? Oriented(w, c) : null;
    }

    private static List<ComposedChromosome>? Oriented(WorkingChromosome? w, ChromosomeId centromere) =>
        w is null ? null : [Orientation.Orient(w.ToComposed(), centromere)];

    private static List<ComposedChromosome>? Join(ChromosomeId centromere, params ComposedPiece?[] pieces) =>
        pieces.Any(p => p is null) ? null : Oriented(WorkingChromosome.Of(pieces.OfType<ComposedPiece>()), centromere);

    // ── Detailed-system text ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The abnormality in the detailed system: <c>der(9)(9pter→9q34::22q11.2→22qter)</c>, with its sign and leading count;
    /// chromosome numbers in the composition only when several chromosomes are involved (<c>STRUCT.COMPOSITION_CHROMOSOME_NUMBERS</c>).
    /// </summary>
    private static string Format(Abnormality a, List<ComposedChromosome> products)
    {
        var head = a.Kind is "der" or "rec" or "ider" ? a.Parts[0] : a.Parts[^1];
        var involved = a.Parts.SelectMany(p => p.Chromosomes).Select(ChromosomeId.Of)
            .Concat(products.SelectMany(p => p.Pieces).SelectMany(p => p.Points).Select(p => p.Chromosome).OfType<ChromosomeId>().Where(c => !c.IsUnknown))
            .Distinct()
            .Count();
        var withChromosomes = involved > 1 || a.Parts.Any(p => p.Chromosomes.Length > 1);

        var sb = new StringBuilder();
        sb.Append(a.Sign switch
        {
            GainLoss.Gain => "+",
            GainLoss.Loss => "–",
            _ => string.Empty,
        });
        if (a.LeadingCount is { IsMissing: false } count)
        {
            sb.Append(count.From?.ToString(CultureInfo.InvariantCulture)).Append(count.IsRange ? "~" + count.To?.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        sb.Append(string.Join(' ', head.Symbols.Select(s => s.Symbol)));
        if (head.Chromosomes.Length > 0)
        {
            sb.Append('(').Append(string.Join(';', head.Chromosomes.Select(c => (c.IsQuestioned ? "?" : string.Empty) + ChromosomeId.Of(c)))).Append(')');
        }

        return sb.Append('(').Append(string.Join(';', products.Select(p => p.Format(withChromosomes)))).Append(')').ToString();
    }
}
