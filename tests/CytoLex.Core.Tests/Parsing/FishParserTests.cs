using CytoLex.Core.Syntax;

namespace CytoLex.Core.Tests.Parsing;

/// <summary>T-033: the in situ grammars (ch. 13): lossless trees, probe names, group classification and recovery.</summary>
public class FishParserTests
{
    private static SyntaxTree ParseFish(string text)
    {
        var tree = Assert.Single(ParseHelper.Parse(text), t => t.Root.Kind == SyntaxKind.FishResult);
        Assert.Equal(tree.Segment.Span.Of(text), tree.ToFullString());
        return tree;
    }

    private static List<string> Texts(SyntaxTree tree, SyntaxKind kind, string text) =>
        [.. tree.Root.DescendantNodesAndSelf().Where(n => n.Kind == kind).Select(n => n.Span.Of(text))];

    private static List<string> Probes(SyntaxTree tree) =>
        [.. tree.Root.Tokens().Where(t => t.Kind == TokenKind.Probe).Select(t => t.Text)];

    [Theory]
    [InlineData("ish del(22)(q11.2q11.2)(D22S75–)", "D22S75")]
    [InlineData("ish del(14)(q21.2q21.2)(RP11-453F20–)", "RP11-453F20")]
    [InlineData("nuc ish(5′CBFB,3′CBFB)×2", "5′CBFB|3′CBFB")]
    [InlineData("nuc ish(5'KMT2A×2)", "5'KMT2A")]
    [InlineData("ish r(X)(wcpX+,DXZ1+)", "wcpX|DXZ1")]
    [InlineData("ish dup(18)(pcp18p11.2+)", "pcp18p11.2")]
    [InlineData("nuc ish(D21S259/D21S341/D21S342)×3", "D21S259|D21S341|D21S342")]
    [InlineData("ish del(15)(q11.2q11.2)(SNRPN–D15S10–)", "SNRPN|D15S10")] // the printed dash is a status, not part of the name
    [InlineData("nuc ish(ABL1conBCR×1)", "ABL1conBCR")] // glued: NUC_ISH.SIGNAL_POSITION_FORMAT reads it
    public void ProbeNames_AreOneToken(string text, string expected) =>
        Assert.Equal(expected.Split('|'), Probes(ParseFish(text)));

    [Fact]
    public void TypedX_GluedToTheName_IsTheMultiplicationSign()
    {
        const string text = "nuc ish(ABCx1,EFG3x2)[10/200]";
        var tree = ParseFish(text);
        Assert.Equal(["ABC", "EFG3"], Probes(tree));
        Assert.Equal(["x1", "x2"], Texts(tree, SyntaxKind.LocusCount, text));
        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("ish ins(2)(wcp2+)", SyntaxKind.LocusGroup, "(wcp2+)")] // breakpoints left out
    [InlineData("ish dmin(MYCN×20~50)[20]", SyntaxKind.LocusGroup, "(MYCN×20~50)")]
    [InlineData("ish dmin(MYCN×20~50)[20]", SyntaxKind.FishCellCount, "[20]")]
    [InlineData("ish subtel(41×2)", SyntaxKind.LocusCount, "×2")]
    [InlineData("ish t(13;20)(q34–,p13+;p13–,q34+)(RP11-63L17–,RP5-1103G7+;RP5-1103G7–,RP11-63L17+)", SyntaxKind.LocusGroup, "(q34–,p13+;p13–,q34+)")]
    [InlineData("ish inv(16)(p13.1)(5′CBFB+)(q22)(3′CBFB+)", SyntaxKind.BreakpointList, "(q22)")]
    [InlineData("ish 22q11.2(HIRA×2),del(22)(q13.3)(ARSA–)", SyntaxKind.FishRegion, "22q11.2(HIRA×2)")]
    [InlineData("nuc ish(ABL1,BCR)×3(ABL1 con BCR×1)(ABL1 con BCR con ABL1×1)[400]", SyntaxKind.LocusGroup, "(ABL1 con BCR con ABL1×1)")]
    [InlineData("nuc ish(MYCN×12~>50)[200]", SyntaxKind.LocusCount, "×12~>50")]
    [InlineData("nuc ish 9q34(ABL1×2),22q11.2(BCR×2)[400]", SyntaxKind.FishRegion, "22q11.2(BCR×2)")]
    [InlineData("nuc ish(PAX7×1,PAX7 amp)(PAX7 con FOXO1)amp", SyntaxKind.FishRegion, "(PAX7×1,PAX7 amp)(PAX7 con FOXO1)amp")]
    [InlineData("fib ish 15q11.2(D15S11+,SNRPN+,GABRB3+)", SyntaxKind.Band, "15q11.2")]
    [InlineData("rev ish 15q", SyntaxKind.Band, "15q")]
    [InlineData("ish cgh enh(1)(q32q43),enh(12)(q13)", SyntaxKind.Abnormality, "enh(12)(q13)")]
    public void Node_HasTheExpectedText(string text, SyntaxKind kind, string expected)
    {
        var tree = ParseFish(text);
        Assert.Contains(expected, Texts(tree, kind, text));
        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("//nuc ish(DXZ1,DYZ3)×1[400]")]
    [InlineData("nuc ish(DXZ1×2)[50]//(DXZ1,DYZ3)×1[300]/(DXZ1×1)[10]")]
    [InlineData("nuc ish(DXZ1×2)[400]//")]
    [InlineData("ish mos del(2)(q11.2q13)(RP11-11P22–)[10/30]")]
    public void CellLines_ParseWithoutErrors(string text) => Assert.Empty(ParseFish(text).Diagnostics);

    [Theory]
    [InlineData("ish del(22)(q11.2q11.2D22S75–)", "ISH.STRUCTURAL_FORMAT", "D", "ish del(22)(q11.2q11.2)(D22S75–)")]
    [InlineData("ish del(22)(q11.2q11.2)(D22S75)–", "ISH.LOCUS_STATUS", "–", "ish del(22)(q11.2q11.2)(D22S75–)")]
    [InlineData("ish cgh enh21", "CGH.FORMAT", "21", "ish cgh enh(21)")]
    public void Recovery_NamesTheFishRule(string text, string rule, string span, string fixedText)
    {
        var d = Assert.Single(ParseFish(text).Diagnostics);
        Assert.Equal((rule, span), (d.RuleId, d.Span.Of(text)));
        Assert.Equal(fixedText, ApplyEdits(text, d));
    }

    [Fact]
    public void KaryotypeWithoutPeriod_IsReportedUnderTheIshRule()
    {
        const string text = "46,XX ish 17p11.2(RAI1×2)";
        var d = Assert.Single(ParseHelper.Diagnostics(text));
        Assert.Equal(("ISH.COMBINED_WITH_KARYOTYPE", "ish"), (d.RuleId, d.Span.Of(text)));
        Assert.Equal("46,XX.ish 17p11.2(RAI1×2)", ApplyEdits(text, d));
    }

    private static string ApplyEdits(string text, SyntaxDiagnostic d)
    {
        var edits = d.Edits.IsDefaultOrEmpty ? [new Core.Validation.TextEdit(d.Span, d.Expected ?? "")] : d.Edits;
        foreach (var e in edits.OrderByDescending(e => e.Span.Start))
        {
            text = text[..e.Span.Start] + e.NewText + text[e.Span.End..];
        }

        return text;
    }
}
