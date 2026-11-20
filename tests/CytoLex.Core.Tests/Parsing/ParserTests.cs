using CytoLex.Core.Segmentation;
using CytoLex.Core.Syntax;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Text;

namespace CytoLex.Core.Tests.Parsing;

public class ParserTests
{
    private static SyntaxTree Parse(string text, bool fragment = false) => Assert.Single(ParseHelper.Parse(text, fragment));

    private static List<string> Diagnostics(string text, bool fragment = false) =>
        [.. ParseHelper.Diagnostics(text, fragment).Select(d => ParseHelper.Describe(text, d))];

    [Fact]
    public void Tree_ForASimpleKaryotype()
    {
        var tree = Parse("46,XY,del(5)(q13q33)");
        Assert.Equal(SyntaxKind.Karyotype, tree.Root.Kind);
        var line = Assert.Single(tree.Root.ChildNodes(SyntaxKind.CellLine));
        Assert.Equal("46", Assert.Single(line.ChildNodes(SyntaxKind.ChromosomeCount)).ToFullString());
        Assert.Equal("XY", Assert.Single(line.ChildNodes(SyntaxKind.SexChromosomes)).ToFullString());

        var abnormality = Assert.Single(tree.Root.DescendantNodesAndSelf(), n => n.Kind == SyntaxKind.Abnormality);
        var part = Assert.Single(abnormality.ChildNodes(SyntaxKind.AbnormalityPart));
        Assert.Equal("del", Assert.Single(part.ChildNodes(SyntaxKind.Symbols)).ToFullString());
        Assert.Equal("(5)", Assert.Single(part.ChildNodes(SyntaxKind.ChromosomeList)).ToFullString());
        var breakpoints = Assert.Single(part.ChildNodes(SyntaxKind.BreakpointList));
        Assert.Equal(["q13", "q33"], breakpoints.DescendantNodesAndSelf().Where(n => n.Kind == SyntaxKind.Band).Select(b => b.ToFullString()));
        Assert.Equal(new TextSpan(6, 14), abnormality.Span);
        Assert.Empty(tree.Diagnostics);
        Assert.Empty(tree.Constructs);
    }

    [Fact]
    public void Derivative_HasOnePartPerRearrangement()
    {
        var tree = Parse("46,XX,der(1)t(1;3)(p32;q21)t(1;11)(q25;q13)");
        var parts = tree.Root.DescendantNodesAndSelf().Where(n => n.Kind == SyntaxKind.AbnormalityPart).Select(p => p.ToFullString());
        Assert.Equal(["der(1)", "t(1;3)(p32;q21)", "t(1;11)(q25;q13)"], parts);
    }

    [Fact]
    public void DetailedSystem_BandsAndLandmarks()
    {
        var tree = Parse("46,XY,t(2;5)(2pter→2q21::5q31→5qter;5pter→5q31::2q21→2qter)");
        var nodes = tree.Root.DescendantNodesAndSelf().ToList();
        Assert.Equal(["2q21", "5q31", "5q31", "2q21"], nodes.Where(n => n.Kind == SyntaxKind.Band).Select(n => n.ToFullString()));
        Assert.Equal(["2pter", "5qter", "5pter", "2qter"], nodes.Where(n => n.Kind == SyntaxKind.Landmark).Select(n => n.ToFullString()));
        Assert.Equal(2, nodes.Count(n => n.Kind == SyntaxKind.BreakpointGroup));
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void Alternatives_JoinedByOr()
    {
        var tree = Parse("46,XY,del(8)(q21.1) or i(8)(p10)");
        var alternative = Assert.Single(tree.Root.DescendantNodesAndSelf(), n => n.Kind == SyntaxKind.Alternative);
        Assert.Equal(2, alternative.ChildNodes(SyntaxKind.Abnormality).Count());

        var fragment = Parse("1q23 or q24", fragment: true);
        var bands = Assert.Single(fragment.Root.DescendantNodesAndSelf(), n => n.Kind == SyntaxKind.Alternative);
        Assert.Equal(["1q23", "q24"], bands.ChildNodes(SyntaxKind.ChromosomeRegion).Select(n => n.Span.Of("1q23 or q24")));
    }

    [Fact]
    public void SeveralErrors_AreAllReported()
    {
        Assert.Equal(
            ["KARYO.COUNT_SEX_ORDER ''@2", "KARYO.CHROMOSOME_IN_PARENTHESES '4'@8", "KARYO.GAIN_LOSS_SIGN '21+'@15"],
            Diagnostics("46XX,del4(p14),21+"));
    }

    [Fact]
    public void MissingComma_IsAZeroWidthToken()
    {
        var tree = Parse("46XX");
        var missing = Assert.Single(tree.Root.Tokens(), t => t.IsMissing);
        Assert.Equal(TokenKind.Comma, missing.Kind);
        Assert.Equal(new TextSpan(2, 0), missing.Span);
        var d = Assert.Single(tree.Diagnostics);
        Assert.Equal(SyntaxDiagnosticKind.Missing, d.Kind);
        Assert.Equal(",", d.Expected);
        Assert.Equal("46XX", tree.ToFullString());
    }

    [Fact]
    public void UnparseableText_IsSkippedAndKept()
    {
        const string text = "46,XX,del(5)(q13q33),=;=,+21";
        var tree = Parse(text);
        Assert.Equal(text, tree.ToFullString());
        Assert.Contains(tree.Diagnostics, d => d.RuleId == CoreCodes.Unparsed && d.Kind == SyntaxDiagnosticKind.Skipped);
        Assert.Contains(tree.Root.DescendantNodesAndSelf(), n => n.Kind == SyntaxKind.WholeChromosome && n.ToFullString() == "+21");
    }

    [Fact]
    public void WholeMode_RequiresTheCount()
    {
        Assert.Equal(["KARYO.COUNT_SEX_ORDER ''@0"], Diagnostics("inv(2)(p21q31)"));
        Assert.Empty(Diagnostics("inv(2)(p21q31)", fragment: true));
        Assert.Equal(["KARYO.COUNT_SEX_ORDER ''@0"], Diagnostics(""));
    }

    [Fact]
    public void TransplantSeparator_IsInScope()
    {
        var tree = Parse("46,XY[3]//46,XX[17]");
        var separator = Assert.Single(tree.Root.ChildNodes(SyntaxKind.CellLineSeparator));
        Assert.Equal("//", separator.ToFullString());
        Assert.Equal(2, tree.Root.ChildNodes(SyntaxKind.CellLine).Count());
        Assert.Empty(tree.Constructs);
        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]")]
    [InlineData("46,XX,t(9;22)(q34;q11.2)[3]/47,sl,+8[10]/48,sdl1,+9[7]")]
    [InlineData("47~51,XY,+1~5mar[cp10]")]
    public void CloneNotation_IsImplemented(string text)
    {
        var tree = Parse(text);
        Assert.Empty(tree.Constructs);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void CellCountAfterAbnormality_BelongsToTheItem()
    {
        const string text = "45~48,XX,del(3)(p12)[2],–5[4],+8[2],+11[3][cp7]";
        var tree = Parse(text);
        Assert.Empty(tree.Diagnostics);
        var line = Assert.Single(tree.Root.ChildNodes(SyntaxKind.CellLine));
        var items = line.ChildNodes(SyntaxKind.Item).ToList();
        Assert.Equal(4, items.Count);
        Assert.All(items, i => Assert.Single(i.ChildNodes(SyntaxKind.ItemCellCount)));
        Assert.Equal("[cp7]", Assert.Single(line.ChildNodes(SyntaxKind.CellCount)).ToFullString());
        Assert.Equal(text, tree.ToFullString());
    }

    [Fact]
    public void CommaBeforeMultiplySign_AfterCloneReference_IsPolyploidSubclone()
    {
        const string text = "46,XY,t(9;22)(q34;q11.2)[3]/92,idem,×2[5]";
        var tree = Parse(text);
        var d = Assert.Single(tree.Diagnostics);
        Assert.Equal("CLONE.POLYPLOID_SUBCLONE", d.RuleId);
        Assert.Equal(",", d.Span.Of(text));
        Assert.Equal(text, tree.ToFullString());
    }

    [Theory]
    [InlineData("46,XX,der(1̲)t(1;3)(p34.3;q21)", ConstructKind.Underlining, "1̲")]
    [InlineData("46,XX,chr(2)(q21)", ConstructKind.Breakage, "chr")]
    public void NotImplementedConstructs_AreReportedWithTheirSpan(string text, ConstructKind kind, string spanText)
    {
        var tree = Parse(text);
        var construct = tree.Constructs.First(c => c.Kind == kind);
        Assert.Equal(spanText, construct.Span.Of(text));
        Assert.Contains("not implemented", construct.Reason, StringComparison.Ordinal);
        Assert.Equal(text, tree.ToFullString());
    }

    [Fact]
    public void Spans_AreOffsetsInTheWholeInput()
    {
        // "end" is no technique keyword, so the second segment is a karyotype too.
        const string text = "46,XX.end 47XY";
        var trees = ParseHelper.Parse(text);
        Assert.Equal(2, trees.Count);
        Assert.Empty(trees[0].Diagnostics);
        var d = Assert.Single(trees[1].Diagnostics);
        Assert.Equal(new TextSpan(12, 0), d.Span);
        Assert.Equal("end 47XY", trees[1].ToFullString());
    }

    [Fact]
    public void ParserRuleIds_ExistInTheKb()
    {
        var ids = KbCatalog.Default.Rules.Where(r => r.Edition == "2016").Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(Parser.RuleIds, id => !ids.Contains(id));
    }

    [Fact]
    public void ParserWordClasses_AreListedSymbols()
    {
        var vocab = ParseHelper.Parser2016.Vocabulary;
        string[] words =
        [
            .. Parser.PrefixWords, .. Parser.InheritanceWords, .. Parser.ArmWords, .. Parser.LandmarkWords,
            .. Parser.FeatureWords, .. Parser.NumberedSymbols, .. Parser.NonSymbolWords,
        ];
        Assert.DoesNotContain(words, w => vocab.Find(w)?.Vocabulary != "symbols");
    }

    [Fact]
    public void Segmenter_AndParser_AgreeOnSegmentText()
    {
        const string text = "46,XX,del(5)(q13q33).ish del(5)(q13q33)(D5S23–)";
        var segments = Segmenter.Split(text);
        var tree = ParseHelper.Parser2016.ParseKaryotype(text, segments[0]);
        Assert.Equal("46,XX,del(5)(q13q33)", tree.ToFullString());
        Assert.Empty(tree.Diagnostics);
    }
}
