using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// Band compositions in the detailed system (§4.3.2, §4.4), per breakpoint group:
/// <list type="bullet">
/// <item><c>STRUCT.DETAILED_SYMBOLS</c>: an arm end is <c>pter</c> or <c>qter</c>; a bare <c>ter</c> is reported.</item>
/// <item><c>STRUCT.COMPOSITION_ORDER</c>: the composition runs from the short-arm end, so a piece through the centromere of
/// its chromosome runs p → q. Reported when every such piece runs q → p and the composition starts on that chromosome's
/// long arm (a reversed centromere piece after a pter start is a pericentric inversion). Not checked where several centromeres or a
/// chain set the order (<c>tas</c>, <c>trc</c>, <c>dic</c>, <c>der(a;b)</c>, rings).</item>
/// <item><c>STRUCT.COMPOSITION_CHROMOSOME_NUMBERS</c>: with one chromosome involved no band carries a chromosome number;
/// with several every band and arm end does. Homologues (<c>dic(13;13)</c>, printed with and without numbers) and
/// compositions with unknown material (<c>?</c>, <c>ins(5;?)</c>) are left alone.</item>
/// <item><c>DER.CENTROMERE_CHROMOSOME</c>: <c>der(n)</c> whose only centromere-bearing pieces belong to another chromosome.
/// Not checked for <c>der(?)</c>, <c>der(a;b)</c>, rings, or with a neocentromere.</item>
/// </list>
/// </summary>
internal static class Composition
{
    public const string DetailedSymbolsRule = "STRUCT.DETAILED_SYMBOLS";
    public const string CompositionOrderRule = "STRUCT.COMPOSITION_ORDER";
    public const string ChromosomeNumbersRule = "STRUCT.COMPOSITION_CHROMOSOME_NUMBERS";
    public const string CentromereChromosomeRule = "DER.CENTROMERE_CHROMOSOME";

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var r in Structure.Parts(model))
        {
            foreach (var group in r.Part.Groups)
            {
                if (group.Structure is not { } structure || structure.Pieces.IsEmpty)
                {
                    continue;
                }

                foreach (var finding in Check(r, structure, input))
                {
                    yield return finding;
                }
            }
        }
    }

    private static IEnumerable<StructuralFinding> Check(Structure.PartRef r, DerivativeStructure structure, string input)
    {
        var part = r.Part;
        var points = structure.Pieces.SelectMany(p => p.Points).ToList();
        foreach (var ter in points.Where(p => p.Landmark == "ter"))
        {
            yield return new StructuralFinding(DetailedSymbolsRule, ter.Span, "An arm end is written pter (short arm) or qter (long arm), not ter.");
        }

        var isRing = structure.LeadingJoin == JoinKind.Junction && structure.TrailingJoin == JoinKind.Junction;
        var crossing = structure.Pieces.Select(Crossing).Where(c => c is not null).Select(c => c!.Value).ToList();
        var severalCentromeres = part.Head is "tas" or "trc" or "dic" || part.Has("dic", "trc")
            || (part.Head is "der" or "rec" && part.Chromosomes.Length > 1);
        if (!isRing && !severalCentromeres && crossing.Count > 0 && crossing.All(c => c.Reversed)
            && StartsOnLongArm(structure.Pieces[0], crossing[0].Chromosome))
        {
            var piece = crossing[0].Piece;
            yield return new StructuralFinding(
                CompositionOrderRule,
                piece.Span,
                $"'{piece.Span.Of(input)}' runs from the long arm to the short arm; the composition starts at the short-arm end (pter) and runs to qter.");
        }

        if (NumbersFinding(part, points) is { } numbers)
        {
            yield return numbers;
        }

        if (part.Head == "der" && r.Index == 0 && !isRing && part.Chromosomes is [{ IsUnknown: false } own]
            && points.All(p => p.Landmark != "neo")
            && crossing.Select(c => c.Chromosome).Distinct(StringComparer.Ordinal).ToList() is [var centromere] && centromere != own.Name)
        {
            yield return new StructuralFinding(
                CentromereChromosomeRule,
                own.Span,
                $"The centromere in this composition is chromosome {centromere}'s ('{crossing[0].Piece.Span.Of(input)}'), so the derivative is der({centromere}).",
                [crossing[0].Piece.Span]);
        }
    }

    private static StructuralFinding? NumbersFinding(AbnormalityPart part, List<Breakpoint> points)
    {
        var written = points.Where(p => p.ChromosomeWritten && p.Chromosome is { IsUnknown: false }).ToList();
        var names = part.Chromosomes.Where(c => !c.IsUnknown).Select(c => c.Name)
            .Concat(written.Select(p => p.Chromosome!.Name))
            .ToHashSet(StringComparer.Ordinal);
        if (names.Count < 2)
        {
            return part.Chromosomes is [{ IsUnknown: false } only] && written.Count > 0 && !points.Any(p => p.Landmark == "?")
                ? new StructuralFinding(
                    ChromosomeNumbersRule,
                    written[0].Span,
                    $"Only chromosome {only.Name} is involved, so its number is not repeated in the band composition.")
                : null;
        }

        var missing = points.FirstOrDefault(p => !p.ChromosomeWritten && (p.Landmark is null or "pter" or "qter" or "ter"));
        return missing is null
            ? null
            : new StructuralFinding(
                ChromosomeNumbersRule,
                missing.Span,
                $"Several chromosomes are involved, so every band and arm end carries its chromosome number ('{missing.Text}' has none).");
    }

    /// <summary>
    /// The composition starts on the long arm of the centromere's chromosome (<c>1qter→1p22::…</c>). An inverted centromere
    /// piece after a short-arm start is a pericentric inversion, not a reversed composition (<c>pter→p13::q21→p13::q21→qter</c>).
    /// </summary>
    private static bool StartsOnLongArm(SegmentPiece first, string chromosome) =>
        first.From.Chromosome?.Name == chromosome && BandPosition.Of(first.From) is { Arm: > 0 };

    /// <summary>A piece that passes through the centromere of its chromosome: from one arm to the other.</summary>
    private readonly record struct CentromerePiece(SegmentPiece Piece, string Chromosome, bool Reversed);

    private static CentromerePiece? Crossing(SegmentPiece piece)
    {
        if (piece.Points.Length < 2 || piece.From.Chromosome is not { IsUnknown: false } c || piece.To.Chromosome?.Name != c.Name
            || piece.Points.Any(p => p.Chromosome?.Name != c.Name)
            || BandPosition.Of(piece.From) is not { } from || BandPosition.Of(piece.To) is not { } to
            || from.Arm == 0 || to.Arm == 0 || from.Arm == to.Arm)
        {
            return null;
        }

        return new CentromerePiece(piece, c.Name, from.Arm > to.Arm);
    }
}
