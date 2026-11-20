using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// How a ch. 9 symbol is written (T-068):
/// <list type="bullet">
/// <item>An arm inside the chromosome list (<c>del(5q)</c>, <c>i(17q)</c>, <c>fra(Xq27.3)</c>) is the running-text form; the
/// karyotype gives the breakpoints in a second pair of parentheses: <c>DEL.FORMAT</c>, <c>ISO.FORMAT</c>, <c>FRA.FORMAT</c>.
/// Aberrations of a derivative or recombinant may carry an arm there (<c>rec(5)del(1q)ins(1;5)</c>, §4.5).</item>
/// <item>An arrow between two bands with no joins (<c>del(5)(q13→q33)</c>) is the short system written with an arrow:
/// <c>DEL.FORMAT</c>, <c>DUP.FORMAT</c>. Band compositions of the detailed system always have a join or a terminal end.</item>
/// <item>Symbols not used in a karyotype: <c>Ph</c> (<c>DER.NO_PH_SYMBOL</c>), <c>ace</c> (<c>ACE.NOT_IN_KARYOTYPE</c>), <c>der dic</c>
/// (<c>DIC.NO_DER_DIC</c>), a size or shape term before <c>mar</c> (<c>min mar</c>, <c>MAR.NO_SIZE_MORPHOLOGY</c>; unlisted terms
/// such as <c>acro</c> are reported by the parser under the same rule).</item>
/// <item><c>t(n;?)(band;?)</c> on its own: unknown material attached at one band is recommended as <c>add(n)(band)</c>
/// (<c>ADD.FORMAT</c>).</item>
/// </list>
/// </summary>
internal static class SymbolForms
{
    public const string AddFormatRule = "ADD.FORMAT";
    public const string DelFormatRule = "DEL.FORMAT";
    public const string DupFormatRule = "DUP.FORMAT";
    public const string IsoFormatRule = "ISO.FORMAT";
    public const string FraFormatRule = "FRA.FORMAT";
    public const string NoPhSymbolRule = "DER.NO_PH_SYMBOL";
    public const string AceRule = "ACE.NOT_IN_KARYOTYPE";
    public const string NoDerDicRule = "DIC.NO_DER_DIC";
    public const string MarSizeRule = "MAR.NO_SIZE_MORPHOLOGY";

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var r in Structure.Parts(model))
        {
            var part = r.Part;
            if (!r.InDerivative && ArmInList(part, input) is { } armFinding)
            {
                yield return armFinding;
            }

            if (ShortSystemArrow(part) is { } arrowFinding)
            {
                yield return arrowFinding;
            }

            if (part.Symbols.FirstOrDefault(s => s.Symbol == "Ph") is { } ph)
            {
                yield return new StructuralFinding(
                    NoPhSymbolRule,
                    ph.Span,
                    "Ph is a term for running text; in a karyotype the Philadelphia chromosome is written der(22)t(9;22)(q34;q11.2).",
                    Fix: PhFix(model, r.Item, ph));
            }

            if (part.Head == "ace")
            {
                yield return new StructuralFinding(
                    AceRule,
                    r.Item.Span,
                    "Acentric fragments other than double minutes are not written in a karyotype; record them in a breakage study.",
                    Fix: model.LineOf(r.Item) is { } line ? FixHelpers.Remove(line, r.Item) : null);
            }

            if (part.Has("der") && part.Has("dic"))
            {
                yield return new StructuralFinding(
                    NoDerDicRule,
                    TextSpan.FromBounds(part.Symbols[0].Span.Start, part.Symbols[^1].Span.End),
                    $"der and dic are alternatives for a dicentric chromosome: write dic{part.ChromosomeList()} or der{part.ChromosomeList()}, not der dic.");
            }

            if (part.Head == "mar" && part.Symbols.Length > 1)
            {
                var terms = TextSpan.FromBounds(part.Symbols[0].Span.Start, part.Symbols[^2].Span.End);
                yield return new StructuralFinding(
                    MarSizeRule,
                    terms,
                    $"The size or shape of a marker ('{terms.Of(input)}') is not described in the karyotype; give it in the text.",
                    Fix: FixProposal.Delete(TextSpan.FromBounds(part.Symbols[0].Span.Start, part.Symbols[^1].Span.Start)));
            }

            if (UnknownPartnerTranslocation(r) is { } addFinding)
            {
                yield return addFinding;
            }
        }
    }

    private static StructuralFinding? ArmInList(AbnormalityPart part, string input)
    {
        if (part.Chromosomes.FirstOrDefault(c => c.Arm is not null) is not { } c || part.Symbols.Length != 1)
        {
            return null;
        }

        var (rule, form) = part.Head switch
        {
            "del" => (DelFormatRule, "del(5)(q13q33)"),
            "i" => (IsoFormatRule, "i(17)(q10)"),
            "fra" => (FraFormatRule, "fra(X)(q27.3)"),
            _ => (null, null),
        };
        return rule is null
            ? null
            : new StructuralFinding(
                rule,
                c.Span,
                $"'{part.HeadSpan().Of(input)}' with the arm inside the parentheses is the running-text form; a karyotype gives the chromosome and then the breakpoints in their own parentheses, e.g. {form}.",
                Fix: ArmInListFix(part, c, input));
    }

    /// <summary>
    /// <c>i(17q)</c> → <c>i(17)(q10)</c>, <c>fra(Xq27.3)</c> → <c>fra(X)(q27.3)</c>: the arm (and band) leave the chromosome list for
    /// a breakpoint list. A deletion given by its arm only (<c>del(5q)</c>) has no single fix: its breakpoints are unknown.
    /// </summary>
    private static FixProposal? ArmInListFix(AbnormalityPart part, ChromosomeRef c, string input)
    {
        if (!part.Groups.IsEmpty || part.Chromosomes.Length != 1 || c.Span.Length <= c.Name.Length)
        {
            return null;
        }

        var rest = input.Substring(c.Span.Start + c.Name.Length, c.Span.Length - c.Name.Length);
        var breakpoint = part.Head switch
        {
            "i" when rest is "p" or "q" => rest + "10",
            "fra" when rest.Length > 1 => rest,
            _ => null,
        };
        return breakpoint is null ? null : FixProposal.Replace(c.Span, $"{c.Name})({breakpoint}");
    }

    /// <summary>
    /// <c>+Ph</c> → <c>+der(22)t(9;22)</c> when the line writes the t(9;22) with its breakpoints (the book's short form,
    /// §9.2.3 E35), else <c>+der(22)t(9;22)(q34;q11.2)</c>.
    /// </summary>
    private static FixProposal? PhFix(KaryotypeModel model, Abnormality item, SymbolRef ph)
    {
        if (item.Parts.Length != 1 || item.Parts[0].Symbols.Length != 1 || item.Parts[0].Span != ph.Span)
        {
            return null;
        }

        var line = model.LineOf(item);
        var translocationWritten = line is not null && line.Items.OfType<Abnormality>().Any(a =>
            a.Parts.Any(p => p.Head == "t" && p.Chromosomes is [{ Name: "9" }, { Name: "22" }] && !p.Groups.IsEmpty));
        return FixProposal.Replace(ph.Span, translocationWritten ? "der(22)t(9;22)" : "der(22)t(9;22)(q34;q11.2)");
    }

    private static StructuralFinding? ShortSystemArrow(AbnormalityPart part)
    {
        if (part.Head is not ("del" or "dup") || part.Symbols.Length != 1 || part.Chromosomes.Length != 1
            || part.Groups is not [{ Structure: { LeadingJoin: null, TrailingJoin: null, Joins.IsEmpty: true, Pieces: [{ Points: [var from, var to] }] } } group]
            || from.Landmark is not null || to.Landmark is not null)
        {
            return null;
        }

        return new StructuralFinding(
            part.Head == "del" ? DelFormatRule : DupFormatRule,
            group.Span,
            $"The short system uses no arrow between the breakpoints: write ({from.Text}{to.Text}).",
            Fix: FixProposal.Delete(TextSpan.FromBounds(from.Span.End, to.Span.Start)));
    }

    private static StructuralFinding? UnknownPartnerTranslocation(Structure.PartRef r)
    {
        var part = r.Part;
        if (part.Head != "t" || part.Symbols.Length != 1 || r.Item.Parts.Length != 1 || part.IsDetailed
            || part.Chromosomes.Length != 2 || part.Groups.Length != 2 || part.Chromosomes.Count(c => c.IsUnknown) != 1)
        {
            return null;
        }

        var known = part.Chromosomes[0].IsUnknown ? 1 : 0;
        var unknownGroup = part.Groups[1 - known];
        if (unknownGroup.Breakpoints is not [{ Landmark: "?" }] || part.Groups[known].Breakpoints is not [{ Landmark: null } band])
        {
            return null;
        }

        var name = part.Chromosomes[known].Name;
        return new StructuralFinding(
            AddFormatRule,
            part.Span,
            $"Material of unknown origin attached at {name}{band.Text} is recommended as add({name})({band.Text}), which implies no mechanism.",
            Fix: FixProposal.Replace(part.Span, $"add({name})({band.Text})"));
    }
}
