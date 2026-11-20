using CytoLex.Core.Syntax;

namespace CytoLex.Core.Tests.Parsing;

/// <summary>T-034: the arr grammar (ch. 14): lossless trees, node shapes and recovery.</summary>
public class ArrParserTests
{
    private static SyntaxTree ParseArr(string text)
    {
        var tree = Assert.Single(ParseHelper.Parse(text), t => t.Root.Kind == SyntaxKind.ArrResult);
        Assert.Equal(tree.Segment.Span.Of(text), tree.ToFullString());
        return tree;
    }

    private static List<string> Texts(SyntaxTree tree, SyntaxKind kind, string text) =>
        [.. tree.Root.DescendantNodesAndSelf().Where(n => n.Kind == kind).Select(n => n.Span.Of(text))];

    [Theory]
    [InlineData("arr(1–22,X)×2", SyntaxKind.ArrChromosomeGroup, "(1–22,X)")]
    [InlineData("arr[GRCh38] 6q21q25.1(113900000_149100000)×1", SyntaxKind.GenomeBuild, "[GRCh38]")]
    [InlineData("arr[GRCh38] 6q21q25.1(113900000_149100000)×1", SyntaxKind.ArrRegion, "6q21q25.1(113900000_149100000)")]
    [InlineData("arr[GRCh38] 6q21q25.1(113900000_149100000)×1", SyntaxKind.Positions, "(113900000_149100000)")]
    [InlineData("arr[GRCh38] 12p13.33p11.1(84917_34382567)×2~4", SyntaxKind.CopyNumber, "×2~4")]
    [InlineData("arr[GRCh38] 7p11.2(54290345_55087100)amp", SyntaxKind.CopyNumber, "amp")]
    [InlineData("arr(1–22,X)cx", SyntaxKind.CopyNumber, "cx")]
    [InlineData("arr(X)×1[0.6]", SyntaxKind.Proportion, "[0.6]")]
    [InlineData("arr[GRCh38] (15q11.2q26.3(23123715_101888908),21q11.2q22.3(14595263_48084819))×2 hmz pat", SyntaxKind.ArrRegionGroup, "(15q11.2q26.3(23123715_101888908),21q11.2q22.3(14595263_48084819))")]
    [InlineData("arr[GRCh38] 13q14.2qter(49176999_115095706)×1", SyntaxKind.Landmark, "qter")]
    public void Node_HasTheExpectedText(string text, SyntaxKind kind, string expected) =>
        Assert.Contains(expected, Texts(ParseArr(text), kind, text));

    [Fact]
    public void DetailedSystem_GivesACopyNumberPerPosition()
    {
        const string text = "arr[GRCh38] 4q32.2q35.1(163002425×2,163146681_183022312×1,184322231×2)dn";
        var tree = ParseArr(text);
        Assert.Equal(["163002425×2", "163146681_183022312×1", "184322231×2"], Texts(tree, SyntaxKind.PositionSpan, text));
        Assert.Equal(["dn"], Texts(tree, SyntaxKind.ArrQualifier, text));
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void Qualifiers_AreKeptInOrder()
    {
        const string text = "arr[GRCh38] 11p12(37741458_39209912)×2 mos hmz mat";
        Assert.Equal(["mos", "hmz", "mat"], Texts(ParseArr(text), SyntaxKind.ArrQualifier, text));
    }

    [Fact]
    public void OrRegions_AreOneRegionWithAlternativePositions()
    {
        const string text = "arr[GRCh38] Xq28 or Yq12(155790872_156030895 or 56959165_57217415)×1";
        var tree = ParseArr(text);
        Assert.Single(Texts(tree, SyntaxKind.ArrRegion, text));
        Assert.Equal(2, Texts(tree, SyntaxKind.PositionSpan, text).Count);
        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("arr[GRCh38] 4q32.2q35.1(163,146,681_183,022,312)×1")] // thousands separators: a rule's fault
    [InlineData("arr[GRCh38] 6q21-q25.1(113900000_149100000)×1")] // hyphen between bands: a rule's fault
    [InlineData("arr(X)[0.6]×1")] // proportion before the copy number: a rule's fault
    [InlineData("arr[GRCh38] 12p13.33p11.1(84917_34382567)×2-4")]
    public void RuleFaults_AreKeptInTheTree_WithoutSyntaxErrors(string text) => Assert.Empty(ParseArr(text).Diagnostics);

    [Theory]
    [InlineData("arr[GRCh38 6q21q25.1(1_2)×1", "GEN.GENOME_BUILD_REQUIRED")]
    [InlineData("arr(1–22,X×2", "ARR.REGION_FORMAT")]
    [InlineData("arr[GRCh38] 6q21q25.1(1_2×1", "ARR.REGION_FORMAT")]
    [InlineData("arr(X)×1[0.6", "ARR.MOSAIC_PROPORTION")]
    [InlineData("arr(X)×1 ;", "CORE.UNPARSED")]
    [InlineData("arr ×2", "CORE.UNPARSED")]
    [InlineData("arr(8)X3", "SYM.TYPED_SYMBOL_FORMS")]
    public void Recovery_NamesTheRule(string text, string rule)
    {
        var tree = ParseArr(text);
        Assert.Contains(tree.Diagnostics, d => d.RuleId == rule);
    }
}
