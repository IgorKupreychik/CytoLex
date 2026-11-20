using System.Globalization;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// The order of the two breakpoints of a two-break rearrangement of one chromosome (§4.3.1.1): short arm before long
/// arm (<c>STRUCT.SHORT_ARM_BREAKPOINT_FIRST</c>), and on one arm the proximal band first
/// (<c>STRUCT.PROXIMAL_BREAKPOINT_FIRST</c>). Checked for <c>inv</c>, <c>del</c> and a ring of one chromosome in the short system,
/// also inside derivatives. Not checked where the band order shows orientation (T-013): <c>dup</c>, <c>trp</c>, <c>qdp</c>,
/// inserted segments, segments of translocations and of rings of several chromosomes, <c>tas</c>, <c>trc</c>. Uncertain bands
/// and bands of which one contains the other are skipped (<see cref="BandPosition"/>).
/// </summary>
internal static class BreakpointOrder
{
    public const string ShortArmFirstRule = "STRUCT.SHORT_ARM_BREAKPOINT_FIRST";
    public const string ProximalFirstRule = "STRUCT.PROXIMAL_BREAKPOINT_FIRST";

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var r in Structure.Parts(model))
        {
            var part = r.Part;
            if (part.Head is not ("inv" or "del" or "r") || part.Has("dic", "trc") || part.IsDetailed
                || part.Chromosomes.Length != 1 || part.Groups is not [{ Breakpoints: [var first, var second] } group]
                || BandPosition.Of(first) is not { } a || BandPosition.Of(second) is not { } b || a.Arm == 0 || b.Arm == 0)
            {
                continue;
            }

            var bands = $"{first.Text}{second.Text}";
            var swapped = $"{second.Text}{first.Text}";
            var swap = new FixProposal([new TextEdit(first.Span, second.Span.Of(input)), new TextEdit(second.Span, first.Span.Of(input))]);
            if (a.Arm > b.Arm)
            {
                yield return new StructuralFinding(
                    ShortArmFirstRule,
                    group.Span,
                    $"The short-arm breakpoint {second.Text} is given before the long-arm one: write ({swapped}), not ({bands}).",
                    Fix: swap);
            }
            else if (a.CompareDistance(b) > 0)
            {
                yield return new StructuralFinding(
                    ProximalFirstRule,
                    group.Span,
                    $"{second.Text} is nearer the centromere than {first.Text} and is given first: write ({swapped}), not ({bands}).",
                    Fix: swap);
            }
        }
    }
}

/// <summary>
/// The breakpoint groups of a rearrangement in the short system (§4.3.1): one group per chromosome, groups separated by
/// semicolons. A part whose number of groups differs from its number of chromosomes (<c>inv(3)(q21;q26.2)</c>,
/// <c>t(2;5)(q21)</c>, <c>t(5;6)(q13;q23;q15;q23)</c>) is reported once: by the symbol's own format rule (<c>inv</c>, <c>r</c>,
/// <c>qdp</c>, <c>trp</c>, <c>t</c>), else by <c>STRUCT.BREAKPOINT_GROUPS</c> (decided with the user in T-031). Breakpoints
/// without parentheses are the parser's (also <c>STRUCT.BREAKPOINT_GROUPS</c>). The detailed system writes one band
/// composition per derived chromosome and is not counted.
/// </summary>
internal static class BreakpointGroups
{
    public const string GeneralRule = "STRUCT.BREAKPOINT_GROUPS";

    private static readonly Dictionary<string, string> _symbolRules = new(StringComparer.Ordinal)
    {
        ["inv"] = "INV.FORMAT",
        ["r"] = "RING.FORMAT",
        ["qdp"] = "QDP.FORMAT",
        ["trp"] = "TRP.FORMAT",
        ["t"] = "TRANSLOC.FORMAT",
    };

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var r in Structure.Parts(model))
        {
            var part = r.Part;
            if (part.Groups.IsEmpty || part.Chromosomes.IsEmpty || part.IsDetailed || part.Head is not { } head
                || part.Groups.Length == part.Chromosomes.Length)
            {
                continue;
            }

            var rule = _symbolRules.GetValueOrDefault(head, GeneralRule);
            var span = TextSpan.FromBounds(part.Groups[0].Span.Start, part.Groups[^1].Span.End);
            var detail = part.Chromosomes.Length == 1
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"All breakpoints of a rearrangement of one chromosome form one group, without semicolons; '{span.Of(input)}' has {part.Groups.Length}.")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{head}{part.ChromosomeList()} has {part.Chromosomes.Length} chromosomes but {part.Groups.Length} breakpoint group(s): one group per chromosome, separated by semicolons; the bands of one chromosome are written together.");
            yield return new StructuralFinding(rule, span, detail, Fix: Regroup(part, span, input));
        }
    }

    /// <summary>
    /// The breakpoints regrouped one group per chromosome (T-035), when the split is certain: one chromosome → one group
    /// (<c>inv(3)(q21;q26.2)</c> → <c>(q21q26.2)</c>); an insertion of two chromosomes with three breakpoints → the receiving
    /// chromosome's one, then the donor's two (<c>ins(5;2)(p14q22q32)</c> → <c>(p14;q22q32)</c>); otherwise an even split
    /// (<c>t(5;6)(q13;q23;q15;q23)</c> → <c>(q13q23;q15q23)</c>). <c>null</c> when a group holds more than its breakpoints.
    /// </summary>
    private static FixProposal? Regroup(AbnormalityPart part, TextSpan span, string input)
    {
        if (part.Groups.Any(g => string.Concat(g.Breakpoints.Select(b => b.Span.Of(input))) != g.Span.Of(input)))
        {
            return null;
        }

        var points = part.Groups.SelectMany(g => g.Breakpoints).Select(b => b.Span.Of(input)).ToList();
        var n = part.Chromosomes.Length;
        int[]? sizes = n == 1 ? [points.Count]
            : part.Head == "ins" && n == 2 && points.Count == 3 ? [1, 2]
            : points.Count % n == 0 ? [.. Enumerable.Repeat(points.Count / n, n)]
            : null;
        if (sizes is null)
        {
            return null;
        }

        var groups = new List<string>();
        var at = 0;
        foreach (var size in sizes)
        {
            groups.Add(string.Concat(points.Skip(at).Take(size)));
            at += size;
        }

        return FixProposal.Replace(span, string.Join(";", groups));
    }
}
