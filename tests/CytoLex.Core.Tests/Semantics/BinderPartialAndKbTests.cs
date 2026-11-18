using CytoLex.Core.Semantics;
using static CytoLex.Core.Tests.Semantics.BindHelper;

namespace CytoLex.Core.Tests.Semantics;

/// <summary>T-065: recovered input gives partial nodes naming the parser diagnostics; the binder's rule tables trace to the KB.</summary>
public class BinderPartialAndKbTests
{
    /// <summary>The innermost partial nodes and the rules of their issues, e.g. <c>Abnormality 'del4(p14)' KARYO.CHROMOSOME_IN_PARENTHESES</c>.</summary>
    private static List<string> Partial<T>(string text, bool fragment = false)
        where T : MeaningNode =>
        [.. All<T>(BindOne(text, fragment)).Where(n => n.IsPartial).Select(n => $"'{n.Span.Of(text)}' {string.Join(",", n.Issues.Select(i => i.RuleId))}")];

    [Fact]
    public void MissingComma_MarksTheCellLineAndCount()
    {
        const string text = "46XX";
        var model = BindOne(text);
        var line = Assert.Single(model.CellLines);
        Assert.True(line.IsPartial);
        Assert.Equal("KARYO.COUNT_SEX_ORDER", Assert.Single(line.Issues).RuleId);
        Assert.False(line.Sex!.IsPartial);
        Assert.Equal(["X", "X"], line.Sex.Chromosomes);
        Assert.Equal(46, line.Count!.From);
    }

    [Fact]
    public void ChromosomeWithoutParentheses_IsBoundButPartial()
    {
        const string text = "46,XX,del4(p14)";
        Assert.Equal(["'del4(p14)' KARYO.CHROMOSOME_IN_PARENTHESES"], Partial<Abnormality>(text));
        var del = One<Abnormality>(BindOne(text));
        var part = Assert.Single(del.Parts);
        Assert.False(part.HasChromosomeList);
        Assert.Equal("4", Assert.Single(part.Chromosomes).Name);
        Assert.Equal(["4p14"], Labels(del.Breakpoints));
    }

    [Fact]
    public void ArmLengthSign_MarksTheRegion()
    {
        Assert.Equal(["'5q–' KARYO.NO_ARM_LENGTH_SIGN"], Partial<ChromosomeRegion>("46,XX,5q–"));
    }

    [Fact]
    public void CommaBetweenChromosomes_MarksTheChromosomeList()
    {
        const string text = "46,XY,t(2,5)(q21;q31)";
        Assert.Equal(["'t(2,5)(q21;q31)' STRUCT.CHROMOSOME_SEPARATOR"], Partial<Abnormality>(text));
        Assert.Empty(Partial<Breakpoint>(text)); // the breakpoints themselves are intact
        Assert.Equal(["2q21", "5q31"], Labels(One<Abnormality>(BindOne(text)).Breakpoints));
    }

    [Fact]
    public void UnparsedText_IsAnUnboundItem()
    {
        const string text = "46,XX,%%";
        var item = One<UnboundItem>(BindOne(text));
        Assert.True(item.IsPartial);
        Assert.Equal("%%", item.Text);
        Assert.Equal("CORE.UNPARSED", Assert.Single(item.Issues).RuleId);
    }

    [Fact]
    public void UnlistedSymbol_IsNotListed()
    {
        var symbol = One<SymbolRef>(BindOne("46,XX,dl(5)(q13q33)"));
        Assert.False(symbol.IsListed);
        Assert.True(symbol.IsPartial);
        Assert.Empty(symbol.Kb);
    }

    [Fact]
    public void CommaBetweenCellLines_MarksTheFollowingCellLine()
    {
        var model = BindOne("47,XX,+21[10],46,XX[10]");
        Assert.Equal(CellLineSeparator.Comma, model.CellLines[1].SeparatorBefore);
        Assert.True(model.CellLines[1].IsPartial);
        Assert.False(model.CellLines[0].IsPartial);
    }

    [Fact]
    public void SymbolRules_ExistAndMatchTheSymbolsVocabSections()
    {
        var kb = Kb2016;
        foreach (var (symbol, ids) in Binder.SymbolRules)
        {
            var entry = kb.Vocab.Find(symbol);
            Assert.True(entry is not null, $"'{symbol}' is not a vocab item");
            foreach (var id in ids)
            {
                var rule = kb.Rule(id);
                Assert.True(rule is not null, $"{id} is not a KB rule");
                Assert.True(
                    entry.Sections.Any(s => rule.Section == s || rule.Section.StartsWith(s + ".", StringComparison.Ordinal)),
                    $"{id} (§{rule.Section}) is not in a section of '{symbol}' ({string.Join(", ", entry.Sections)})");
            }
        }
    }

    [Fact]
    public void ConstructRules_ExistInTheKb()
    {
        Assert.Empty(Binder.ConstructRuleIds.Where(id => Kb2016.Rule(id) is null));
    }
}
