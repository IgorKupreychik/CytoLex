using CytoLex.Core.Composing;
using CytoLex.Core.Semantics;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// Homogeneously staining regions inside a derivative (§9.2.8; T-068):
/// <list type="bullet">
/// <item><c>HSR.REPLACED_SEGMENT_PROXIMAL</c> (info): an <c>hsr</c> that replaces a deleted segment is assigned to the
/// deletion's proximal breakpoint (<c>der(1)del(1)(p21p33)hsr(1)(p21)</c>, not <c>hsr(1)(p33)</c>).</item>
/// <item><c>HSR.INTERFACE_TWO_CHROMOSOMES</c>: an <c>hsr(n)(x)</c> of one chromosome at a breakpoint x that an earlier
/// rearrangement of the same derivative joins to another chromosome lies at the interface, and is written with both
/// chromosomes and both breakpoints (<c>hsr(1;7)(q21;p11.2)</c>).</item>
/// </list>
/// The parser reports <c>abr</c> (<c>HSR.NO_ABR_SYMBOL</c>).
/// </summary>
internal static class Hsr
{
    public const string ProximalRule = "HSR.REPLACED_SEGMENT_PROXIMAL";
    public const string InterfaceRule = "HSR.INTERFACE_TWO_CHROMOSOMES";

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model)
    {
        foreach (var a in model.DescendantsAndSelf().OfType<Abnormality>().Where(a => !a.IsPartial && a.Parts is [{ Head: "der" }, _, ..]))
        {
            for (var i = 1; i < a.Parts.Length; i++)
            {
                var hsr = a.Parts[i];
                if (hsr.Head != "hsr" || hsr.Symbols.Length != 1 || hsr.IsDetailed
                    || hsr.Chromosomes is not [var chromosome] || hsr.Groups is not [{ Breakpoints: [var band] } group])
                {
                    continue;
                }

                var earlier = a.Parts.Skip(1).Take(i - 1).ToList();
                if (DistalDeletionBand(earlier, chromosome.Name, band) is { } proximal)
                {
                    yield return new StructuralFinding(
                        ProximalRule,
                        group.Span,
                        $"An hsr that replaces a deleted segment is assigned to the proximal breakpoint of the deletion: write ({proximal.Text}), not ({band.Text}).",
                        Fix: FixProposal.Replace(band.Span, proximal.Text));
                }

                if (InterfacePartner(earlier, chromosome.Name, band) is { } partner)
                {
                    yield return new StructuralFinding(
                        InterfaceRule,
                        hsr.HeadSpan(),
                        $"An hsr at {chromosome.Name}{band.Text}, where {partner.Part.Head}{partner.Part.ChromosomeList()} joins chromosome {partner.Other}, lies at the interface of two chromosomes: give both chromosomes and both breakpoints, e.g. hsr({chromosome.Name};{partner.Other})({band.Text};…).",
                        [partner.Part.Span]);
                }
            }
        }
    }

    /// <summary>The proximal band of an earlier <c>del(n)(a b)</c> whose distal band is <paramref name="band"/>; else <c>null</c>.</summary>
    private static Breakpoint? DistalDeletionBand(List<AbnormalityPart> earlier, string chromosome, Breakpoint band)
    {
        foreach (var del in earlier.Where(p => p.Head == "del" && p.Symbols.Length == 1 && !p.IsDetailed && p.Chromosomes is [{ } c] && c.Name == chromosome))
        {
            if (del.Groups is not [{ Breakpoints: [var first, var second] }]
                || BandPosition.Of(first) is not { } a || BandPosition.Of(second) is not { } b || a.CompareDistance(b) is not { } d || d == 0)
            {
                continue;
            }

            var (proximal, distal) = d < 0 ? (first, second) : (second, first);
            if (distal.Text == band.Text)
            {
                return proximal;
            }
        }

        return null;
    }

    private readonly record struct Partner(AbnormalityPart Part, string Other);

    /// <summary>An earlier rearrangement of several chromosomes with <paramref name="band"/> as a breakpoint of <paramref name="chromosome"/>.</summary>
    private static Partner? InterfacePartner(List<AbnormalityPart> earlier, string chromosome, Breakpoint band)
    {
        foreach (var p in earlier.Where(p => p.Chromosomes.Length >= 2 && !p.IsDetailed && p.Groups.Length == p.Chromosomes.Length))
        {
            for (var k = 0; k < p.Chromosomes.Length; k++)
            {
                if (p.Chromosomes[k].Name == chromosome && p.Groups[k].Breakpoints.Any(bp => bp.Text == band.Text && bp.Landmark is null))
                {
                    var other = p.Chromosomes.Where((_, j) => j != k).Select(c => c.Name).FirstOrDefault(n => n != chromosome);
                    if (other is not null)
                    {
                        return new Partner(p, other);
                    }
                }
            }
        }

        return null;
    }
}
