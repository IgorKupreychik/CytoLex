using System.Globalization;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Text;

namespace CytoLex.Core.Tests.Corpus.Mutations;

/// <summary>The mutation operators of the corpus (T-062), each grounded in a book pair of its rule.</summary>
internal static class MutationCatalog
{
    public static IReadOnlyList<Mutation> All { get; } =
    [
        new SwapRearrangementChromosomes(),
        new ChromosomeListComma(),
        new NonexistentBand(),
        new CountPlusOne(),
        new SwapAdjacentItems(),
        new DropCommaBeforeSign(),
        new SignAfterChromosome(),
        new DropCountComma(),
        new DropBreakpointParentheses(),
        new SpaceAfterComma(),
        new SpaceBeforeParenthesis(),
        new LowerCaseSexChromosomes(),
        new CapitalizedSymbol(),
        new DropCloneCellCount(),
    ];
}

/// <summary><c>t(12;16)(q13;p11.1)</c> → <c>t(16;12)(p11.1;q13)</c>: the chromosomes out of order.</summary>
internal sealed class SwapRearrangementChromosomes : Mutation
{
    public override string Id => "swap-chromosomes";

    public override string RuleId => "STRUCT.CHROMOSOME_ORDER";

    public override BookPair Grounding => new("46,XY,t(12;16)(q13;p11.1)", "46,XY,t(16;12)(p11.1;q13)");

    public override string? Apply(MutationInput input)
    {
        var part = Items(input.Model)
            .Select(x => x.Item)
            .OfType<Abnormality>()
            .Where(a => a.Parts.Length == 1 && a.Kind == "t")
            .Select(a => a.Parts[0])
            .FirstOrDefault(p => p.Chromosomes.Length == 2 && p.Groups.Length == 2 && !p.IsDetailed
                && p.Chromosomes[0].Name != p.Chromosomes[1].Name
                && p.Chromosomes.All(c => !c.IsUnderlined && !c.IsQuestioned && !c.IsUnknown));
        if (part is null)
        {
            return null;
        }

        string Of(TextSpan s) => s.Of(input.Text);
        return Edit(
            input.Text,
            (part.Chromosomes[0].Span, Of(part.Chromosomes[1].Span)),
            (part.Chromosomes[1].Span, Of(part.Chromosomes[0].Span)),
            (part.Groups[0].Span, Of(part.Groups[1].Span)),
            (part.Groups[1].Span, Of(part.Groups[0].Span)));
    }
}

/// <summary><c>t(2;5)</c> → <c>t(2,5)</c>: a comma instead of the semicolon between chromosomes.</summary>
internal sealed class ChromosomeListComma : Mutation
{
    public override string Id => "chromosome-list-comma";

    public override string RuleId => "STRUCT.CHROMOSOME_SEPARATOR";

    public override BookPair Grounding => new("46,XY,t(2;5)(q21;q31)", "46,XY,t(2,5)(q21;q31)");

    public override string? Apply(MutationInput input)
    {
        foreach (var part in Items(input.Model).Select(x => x.Item).OfType<Abnormality>().SelectMany(a => a.Parts))
        {
            if (part.Chromosomes.Length >= 2)
            {
                var between = part.Chromosomes[0].Span.End;
                if (between < input.Text.Length && input.Text[between] == ';')
                {
                    return Edit(input.Text, (new TextSpan(between, 1), ","));
                }
            }
        }

        return null;
    }
}

/// <summary>
/// A band past the last band of its arm (<c>9q34</c> → <c>9q35</c>, the book's example of a band that doesn't exist):
/// the first plain breakpoint gets the arm's highest band number + 1.
/// </summary>
internal sealed class NonexistentBand : Mutation
{
    public override string Id => "nonexistent-band";

    public override string RuleId => "BAND.MUST_EXIST";

    // The book's invalid example is the bare band 9q35; in a karyotype it reads as below.
    public override BookPair Grounding => new("46,XX,del(9)(q34)", "46,XX,del(9)(q35)", "9q35");

    public override string? Apply(MutationInput input)
    {
        if (input.Bands is not { } bands)
        {
            return null;
        }

        foreach (var b in input.Model.DescendantsAndSelf().OfType<Breakpoint>())
        {
            if (b is not { Chromosome: { IsUnknown: false } c, Arm: { } arm, BandNumber: not null, Landmark: null, IsQuestioned: false, RangeEnd: null }
                || !b.Alternatives.IsEmpty || b.KbBand is null)
            {
                continue;
            }

            var highest = bands.Bands
                .Where(x => x.Chromosome == c.Name && x.Arm == arm && !x.Name.Contains('.', StringComparison.Ordinal))
                .Select(x => int.Parse(x.Name[(c.Name.Length + 1)..], CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0)
                .Max();
            var band = arm + (highest + 1).ToString(CultureInfo.InvariantCulture);
            if (highest == 0 || bands.Contains(c.Name + band))
            {
                continue;
            }

            var written = arm + b.BandNumber;
            var at = b.Span.Of(input.Text).IndexOf(written, StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            return Edit(input.Text, (new TextSpan(b.Span.Start + at, written.Length), band));
        }

        return null;
    }
}

/// <summary><c>47,XX,+21</c> → <c>48,XX,+21</c>: a chromosome count that doesn't match the changes.</summary>
internal sealed class CountPlusOne : Mutation
{
    public override string Id => "count-plus-one";

    public override string RuleId => "NUM.COUNT_MATCHES_CHANGES";

    public override BookPair Grounding => new("47,XX,+21", "48,XX,+21");

    public override string? Apply(MutationInput input)
    {
        // Near-diploid lines without a ploidy or uncertain parts: there the count follows from the changes. An incomplete
        // karyotype (inc) may hold unlisted changes (UNCERTAIN.INC_AT_END), so its count can't be checked.
        var line = input.Model.CellLines.FirstOrDefault(l =>
            l is { Ploidy: null, BasedOn: null, Count: { IsRange: false, IsMissing: false, From: >= 40 and <= 52 } }
            && l.CellCount is not { IsComposite: true }
            && l.Items.All(i => i is SuffixedItem { IsQuestioned: false } and not { Copies: not null } and not Abnormality { Kind: "inc" }));
        return line?.Count is { } count
            ? Edit(input.Text, (count.Span, (count.From!.Value + 1).ToString(CultureInfo.InvariantCulture)))
            : null;
    }
}

/// <summary><c>47,XY,del(5)(q13q33),+21</c> → <c>47,XY,+21,del(5)(q13q33)</c>: two items out of chromosome order.</summary>
internal sealed class SwapAdjacentItems : Mutation
{
    public override string Id => "swap-items";

    public override string RuleId => "KARYO.ABNORMALITY_ORDER";

    public override BookPair Grounding => new("47,XY,del(5)(q13q33),+21", "47,XY,+21,del(5)(q13q33)");

    public override string? Apply(MutationInput input)
    {
        foreach (var line in input.Model.CellLines.Where(l => l.BasedOn is null))
        {
            for (var i = 0; i + 1 < line.Items.Length; i++)
            {
                var (a, b) = (line.Items[i], line.Items[i + 1]);
                if (IsPlain(a) && IsPlain(b) && FirstAutosome(a) is { } x && FirstAutosome(b) is { } y && x < y)
                {
                    return Edit(input.Text, (a.Span, b.Span.Of(input.Text)), (b.Span, a.Span.Of(input.Text)));
                }
            }
        }

        return null;
    }
}

/// <summary><c>del(5)(q13q33),+21</c> → <c>del(5)(q13q33)+21</c>: the comma before a gained chromosome left out.</summary>
internal sealed class DropCommaBeforeSign : Mutation
{
    public override string Id => "drop-comma-before-sign";

    public override string RuleId => "KARYO.ABNORMALITY_ORDER";

    public override BookPair Grounding => new("47,XY,del(5)(q13q33),+21", "47,XY,del(5)(q13q33)+21");

    public override string? Apply(MutationInput input)
    {
        foreach (var line in input.Model.CellLines)
        {
            for (var i = 1; i < line.Items.Length; i++)
            {
                if (line.Items[i - 1] is Abnormality prev && IsPlain(prev) && line.Items[i] is WholeChromosomeChange { Sign: GainLoss.Gain } w
                    && IsPlain(w) && input.Text[w.Span.Start - 1] == ',' && input.Text[prev.Span.End - 1] == ')')
                {
                    return Edit(input.Text, (new TextSpan(w.Span.Start - 1, 1), ""));
                }
            }
        }

        return null;
    }
}

/// <summary><c>47,XX,+21</c> → <c>47,XX,21+</c>: the sign after the chromosome.</summary>
internal sealed class SignAfterChromosome : Mutation
{
    public override string Id => "sign-after";

    public override string RuleId => "KARYO.GAIN_LOSS_SIGN";

    public override BookPair Grounding => new("47,XX,+21", "47,XX,21+");

    public override string? Apply(MutationInput input)
    {
        var w = Items(input.Model).Select(x => x.Item).OfType<WholeChromosomeChange>()
            .FirstOrDefault(w => w.Sign == GainLoss.Gain && IsPlain(w) && w.Constitutional is null && w.Inheritance is null);
        return w is null ? null : Edit(input.Text, (w.Span, w.Chromosome.Span.Of(input.Text) + "+"));
    }
}

/// <summary><c>46,XX</c> → <c>46XX</c>: no comma between the count and the sex chromosomes.</summary>
internal sealed class DropCountComma : Mutation
{
    public override string Id => "drop-count-comma";

    public override string RuleId => "KARYO.COUNT_SEX_ORDER";

    public override BookPair Grounding => new("46,XX", "46XX");

    public override string? Apply(MutationInput input)
    {
        var line = input.Model.CellLines.FirstOrDefault(l => l is { Count: not null, Ploidy: null, Sex: not null });
        if (line is null)
        {
            return null;
        }

        var comma = line.Count!.Span.End;
        return input.Text[comma] == ',' && comma + 1 == line.Sex!.Span.Start ? Edit(input.Text, (new TextSpan(comma, 1), "")) : null;
    }
}

/// <summary><c>inv(2)(p21q31)</c> → <c>inv(2)p21q31</c>: breakpoints without their parentheses.</summary>
internal sealed class DropBreakpointParentheses : Mutation
{
    public override string Id => "drop-breakpoint-parentheses";

    public override string RuleId => "STRUCT.BREAKPOINT_GROUPS";

    public override BookPair Grounding => new("46,XX,inv(2)(p21q31)", "46,XX,inv(2)p21q31");

    public override string? Apply(MutationInput input)
    {
        foreach (var a in Items(input.Model).Select(x => x.Item).OfType<Abnormality>().Where(a => IsPlain(a) && a.Parts.Length == 1))
        {
            var part = a.Parts[0];
            if (part is { Chromosomes.Length: 1, Groups.Length: 1, IsDetailed: false } && part.Head is "inv" or "del" or "dup")
            {
                var g = part.Groups[0].Span;
                if (input.Text[g.Start - 1] == '(' && g.End < input.Text.Length && input.Text[g.End] == ')' && g.End + 1 == a.Span.End)
                {
                    return Edit(input.Text, (new TextSpan(g.End, 1), ""), (new TextSpan(g.Start - 1, 1), ""));
                }
            }
        }

        return null;
    }
}

/// <summary><c>47,XY,+21</c> → <c>47,XY, +21</c>: a space after a comma.</summary>
internal sealed class SpaceAfterComma : Mutation
{
    public override string Id => "space-after-comma";

    public override string RuleId => "SYM.NO_OTHER_SPACE";

    public override BookPair Grounding => new("47,XY,+21", "47,XY, +21");

    public override string? Apply(MutationInput input)
    {
        var item = Items(input.Model).Select(x => x.Item).FirstOrDefault(i => i.Span.Start > 0 && input.Text[i.Span.Start - 1] == ',');
        return item is null ? null : Edit(input.Text, (new TextSpan(item.Span.Start, 0), " "));
    }
}

/// <summary><c>del(5)(q13q33)</c> → <c>del (5)(q13q33)</c>: a space before a parenthesis.</summary>
internal sealed class SpaceBeforeParenthesis : Mutation
{
    public override string Id => "space-before-parenthesis";

    public override string RuleId => "SYM.NO_SPACE_AT_PARENTHESIS";

    public override BookPair Grounding => new("46,XX,del(5)(q13q33)", "46,XX,del (5)(q13q33)");

    public override string? Apply(MutationInput input)
    {
        var symbol = Items(input.Model).Select(x => x.Item).OfType<Abnormality>()
            .SelectMany(a => a.Parts).Select(p => p.Symbols.LastOrDefault())
            .FirstOrDefault(s => s is not null && s.Span.End < input.Text.Length && input.Text[s.Span.End] == '(');
        return symbol is null ? null : Edit(input.Text, (new TextSpan(symbol.Span.End, 0), " "));
    }
}

/// <summary><c>46,XY</c> → <c>46,xy</c>: lower-case sex chromosomes (the book's example is <c>XY</c>, so only that complement).</summary>
internal sealed class LowerCaseSexChromosomes : Mutation
{
    public override string Id => "lower-case-sex";

    public override string RuleId => "SYM.LETTER_CASE";

    public override BookPair Grounding => new("46,XY", "46,xy");

    public override string? Apply(MutationInput input)
    {
        var sex = input.Model.CellLines.Select(l => l.Sex).FirstOrDefault(s => s is not null && s.Span.Of(input.Text) == "XY");
        return sex is null ? null : Edit(input.Text, (sex.Span, "xy"));
    }
}

/// <summary><c>del(5)(q13q33)</c> → <c>Del(5)(q13q33)</c>: a symbol with a capital letter.</summary>
internal sealed class CapitalizedSymbol : Mutation
{
    public override string Id => "capitalized-symbol";

    public override string RuleId => "SYM.LETTER_CASE";

    public override BookPair Grounding => new("46,XX,del(5)(q13q33)", "46,XX,Del(5)(q13q33)");

    public override string? Apply(MutationInput input)
    {
        var symbol = Items(input.Model).Select(x => x.Item).OfType<Abnormality>()
            .SelectMany(a => a.Parts).SelectMany(p => p.Symbols)
            .FirstOrDefault(s => s.Symbol is "del" or "dup" or "inv" or "ins");
        return symbol is null ? null : Edit(input.Text, (new TextSpan(symbol.Span.Start, 1), symbol.Symbol[..1].ToUpperInvariant()));
    }
}

/// <summary>
/// <c>…[26]/47,idem,+21[4]</c> → <c>…[26]/47,idem,+21</c>: a clone without its cell count. The book's invalid example has
/// no corrected form, so the source adds the count the rule asks for.
/// </summary>
internal sealed class DropCloneCellCount : Mutation
{
    public override string Id => "drop-cell-count";

    public override string RuleId => "CLONE.CELL_COUNT_REQUIRED";

    public override SampleContext? RequiredContext => SampleContext.Acquired;

    public override BookPair Grounding => new("46,XY,t(8;21)(q22;q22)[26]/47,idem,+21[4]", "46,XY,t(8;21)(q22;q22)[26]/47,idem,+21");

    public override string? Apply(MutationInput input)
    {
        var lines = input.Model.CellLines;
        return lines.Length >= 2 && lines.All(l => l.CellCount is { IsComposite: false, Cells: not null })
            ? Edit(input.Text, (lines[^1].CellCount!.Span, ""))
            : null;
    }
}
