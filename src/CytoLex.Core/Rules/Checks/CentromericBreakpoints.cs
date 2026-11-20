using System.Collections.Frozen;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// Breakpoints at the centromere and whole-arm rearrangements (§9.2.6, §9.2.3, §9.2.11, §9.2.17.2–3; T-068). Short system
/// only; questioned, interval and <c>or</c> bands are skipped.
/// <list type="bullet">
/// <item>The single breakpoint of <c>i</c> (<c>ISO.FORMAT</c>), <c>ider</c> (<c>DER.ISODERIVATIVE</c>) and <c>fis</c>
/// (<c>FIS.FORMAT</c>) is p10 or q10. <c>idic</c> breaks outside the centromere and is not checked.</item>
/// <item><c>TRANSLOC.WHOLE_ARM</c>: a balanced whole-arm exchange (a two-chromosome <c>t</c> on its own, both breakpoints
/// p10/q10) gives the first chromosome p10. Breakpoints at p11.1/q11.1 are not taken for a whole-arm translocation: the
/// book prints <c>t(X;18)(p11.1;q11.1)</c> as an ordinary one (§4.3.1.1 E3; decided with the user in T-068).</item>
/// <item><c>DER.WHOLE_ARM_TWO_CHROMOSOMES</c>: <c>der(n)t(n;m)(p10/q10;p10/q10)</c> with no other aberration is written
/// <c>der(n;m)</c>.</item>
/// <item><c>ROB.FORMAT</c>: <c>rob</c> joins the acrocentric chromosomes 13–15 and 21–22. <c>ROB.NOT_ACQUIRED</c>: any
/// <c>rob</c> (the KB context, neoplastic, makes it conditional). <c>ROB.DICENTRIC_USE_DIC</c>: <c>rob(a;b)</c> or
/// <c>der(a;b)</c> on its own with a breakpoint other than p10/q10 is dicentric and written <c>dic</c>.</item>
/// </list>
/// </summary>
internal static class CentromericBreakpoints
{
    public const string IsoFormatRule = "ISO.FORMAT";
    public const string IsoderivativeRule = "DER.ISODERIVATIVE";
    public const string FisFormatRule = "FIS.FORMAT";
    public const string WholeArmRule = "TRANSLOC.WHOLE_ARM";
    public const string DerWholeArmRule = "DER.WHOLE_ARM_TWO_CHROMOSOMES";
    public const string RobFormatRule = "ROB.FORMAT";
    public const string RobNotAcquiredRule = "ROB.NOT_ACQUIRED";
    public const string RobDicentricRule = "ROB.DICENTRIC_USE_DIC";

    /// <summary>The acrocentric chromosomes that Robertsonian translocations join (§9.2.17.3 ¶1, <c>ROB.FORMAT</c>).</summary>
    private static readonly FrozenSet<string> _acrocentric = FrozenSet.ToFrozenSet(["13", "14", "15", "21", "22"], StringComparer.Ordinal);

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var r in Structure.Parts(model))
        {
            var part = r.Part;
            if (part.IsDetailed || part.Symbols.Length != 1)
            {
                continue;
            }

            if (SingleCentromericBreakpoint(r) is { } single)
            {
                yield return single;
            }

            if (part.Head == "t" && !r.InDerivative && WholeArm(r, input) is { } wholeArm)
            {
                yield return wholeArm;
            }

            if (part.Head == "der" && r.Index == 0 && r.Item.Parts is [_, { Head: "t", Symbols.Length: 1, IsDetailed: false } t]
                && part.Chromosomes is [var own] && part.Groups.IsEmpty && t.Chromosomes.Length == 2
                && Single(t) is [var a, var b] && IsCentromere(a) && IsCentromere(b))
            {
                var other = t.Chromosomes.Select(c => c.Name).ToArray();
                var tList = TextSpan.FromBounds(t.Symbols[0].Span.End, t.HeadSpan().End);
                yield return new StructuralFinding(
                    DerWholeArmRule,
                    r.Item.Parts[0].Span,
                    $"The derivative of an unbalanced whole-arm translocation is named after both chromosomes: write der({string.Join(";", other)})({a.Text};{b.Text}), not der({own.Name})t{t.ChromosomeList()}.",
                    Fix: FixProposal.Replace(TextSpan.FromBounds(part.Span.Start, t.HeadSpan().End), "der" + tList.Of(input)));
            }

            if (part.Head == "rob")
            {
                var asDer = FixProposal.Replace(part.Symbols[0].Span, "der");
                yield return new StructuralFinding(RobNotAcquiredRule, part.HeadSpan(), "rob is not used for acquired abnormalities; write the whole-arm derivative with der.", Fix: asDer);
                if (part.Chromosomes.FirstOrDefault(c => !c.IsUnknown && !_acrocentric.Contains(c.Name)) is { } notAcrocentric)
                {
                    yield return new StructuralFinding(
                        RobFormatRule,
                        part.HeadSpan(),
                        $"Chromosome {notAcrocentric.Name} is not acrocentric; rob joins chromosomes 13–15 and 21–22. Write der{part.ChromosomeList()}.",
                        Fix: asDer);
                }
            }

            if ((part.Head == "rob" || (part.Head == "der" && r.Item.Parts.Length == 1 && part.Chromosomes.Length == 2))
                && Single(part) is { Length: > 0 } bps && bps.All(IsCertainBand) && bps.Any(bp => !IsCentromere(bp)))
            {
                yield return new StructuralFinding(
                    RobDicentricRule,
                    part.HeadSpan(),
                    $"Breakpoints outside the centromere ({string.Join(";", bps.Select(bp => bp.Text))}) make the chromosome dicentric: write dic{part.ChromosomeList()}.",
                    Fix: FixProposal.Replace(part.Symbols[0].Span, "dic"));
            }
        }
    }

    private static StructuralFinding? SingleCentromericBreakpoint(Structure.PartRef r)
    {
        var part = r.Part;
        var rule = part.Head switch
        {
            "i" => IsoFormatRule,
            "fis" => FisFormatRule,
            "ider" when r.Index == 0 => IsoderivativeRule,
            _ => null,
        };
        if (rule is null || part.Chromosomes.Length != 1 || part.Groups is not [{ Breakpoints: [var bp] } group]
            || !IsCertainBand(bp) || IsCentromere(bp))
        {
            return null;
        }

        var what = part.Head switch
        {
            "i" => "an isochromosome",
            "fis" => "a centric fission",
            _ => "an isoderivative chromosome",
        };
        return new StructuralFinding(
            rule,
            group.Span,
            $"The breakpoint of {what} is the centromeric band p10 or q10, not {bp.Text}.",
            Fix: bp.Arm is "p" or "q" ? FixProposal.Replace(bp.Span, bp.Arm + "10") : null);
    }

    private static StructuralFinding? WholeArm(Structure.PartRef r, string input)
    {
        var part = r.Part;
        if (part.Chromosomes.Length != 2 || Single(part) is not [var a, var b])
        {
            return null;
        }

        if (r.Item.Sign == GainLoss.None && IsCentromere(a) && IsCentromere(b) && a.Arm == "q")
        {
            var swapped = $"(p10;{(b.Arm == "p" ? "q" : "p")}10)";
            return new StructuralFinding(
                WholeArmRule,
                GroupsSpan(part),
                $"In a balanced whole-arm exchange the first chromosome's breakpoint is written p10: '{GroupsSpan(part).Of(input)}' is the same exchange as {swapped}.",
                Fix: FixProposal.Replace(GroupsSpan(part), swapped[1..^1]));
        }

        return null;
    }

    private static TextSpan GroupsSpan(AbnormalityPart part) =>
        TextSpan.FromBounds(part.Groups[0].Span.Start, part.Groups[^1].Span.End);

    /// <summary>The breakpoints of a part whose every group has exactly one breakpoint; <c>[]</c> otherwise.</summary>
    private static Breakpoint[] Single(AbnormalityPart part) =>
        part.Groups.Length == part.Chromosomes.Length && part.Groups.All(g => g.Breakpoints.Length == 1)
            ? [.. part.Groups.Select(g => g.Breakpoints[0])]
            : [];

    /// <summary><c>p10</c> or <c>q10</c>, certain.</summary>
    public static bool IsCentromere(Breakpoint bp) => IsCertainBand(bp) && bp.BandNumber == "10";

    /// <summary>A certain band on an arm (not a landmark, <c>?</c>, interval or <c>or</c> reading).</summary>
    public static bool IsCertainBand(Breakpoint bp) => bp.Landmark is null && BandPosition.Of(bp) is { Arm: not 0 };
}
