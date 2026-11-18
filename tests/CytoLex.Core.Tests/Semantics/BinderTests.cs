using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using static CytoLex.Core.Tests.Semantics.BindHelper;

namespace CytoLex.Core.Tests.Semantics;

/// <summary>T-065 spot checks of the semantic model for representative karyotypes (docs/design/core.md §5, §13).</summary>
public class BinderTests
{
    private static Abnormality Abn(string text, bool fragment = false) => One<Abnormality>(BindOne(text, fragment));

    [Fact]
    public void WorkedExample_Deletion()
    {
        const string text = "46,XY,del(5)(q13q33)";
        var model = BindOne(text);
        var line = Assert.Single(model.CellLines);
        Assert.Equal(46, line.Count!.From);
        Assert.Equal(["X", "Y"], line.Sex!.Chromosomes);

        var del = Assert.IsType<Abnormality>(Assert.Single(line.Items));
        Assert.Equal("del", del.Kind);
        Assert.Equal(new TextSpan(6, 14), del.Span);
        var part = Assert.Single(del.Parts);
        Assert.Equal("5", Assert.Single(part.Chromosomes).Name);
        Assert.False(part.IsDetailed);
        Assert.Equal(["5q13", "5q33"], Labels(del.Breakpoints));
        Assert.Equal(["vocab:symbols/del", "rule:DEL.FORMAT"], Refs(Assert.Single(part.Symbols)));
        Assert.Equal([["band:5q13"], ["band:5q33"]], del.Breakpoints.Select(b => Refs(b).Where(r => r.StartsWith("band:", StringComparison.Ordinal))));
        Assert.All(del.Breakpoints, b => Assert.NotNull(b.KbBand));
        Assert.DoesNotContain(model.DescendantsAndSelf(), n => n.IsPartial);
    }

    [Fact]
    public void Translocation_GroupsFollowTheChromosomeList()
    {
        var t = Abn("46,XY,t(2;5)(q21;q31)");
        var part = Assert.Single(t.Parts);
        Assert.Equal(["2", "5"], part.Groups.Select(g => g.Chromosome!.Name));
        Assert.Equal(["2q21", "5q31"], Labels(t.Breakpoints));
        Assert.Contains("rule:TRANSLOC.FORMAT", Refs(Assert.Single(part.Symbols)));
    }

    [Fact]
    public void Derivative_OnePartPerRearrangement_AndTheDetailedTwin()
    {
        var der = Abn("46,XX,der(1)t(1;3)(p32;q21)t(1;11)(q25;q13)");
        Assert.Equal("der", der.Kind);
        Assert.Equal(["der", "t", "t"], der.Parts.Select(p => p.Head));
        Assert.Equal("1", Assert.Single(der.Parts[0].Chromosomes).Name);
        Assert.Empty(der.Parts[0].Groups);
        Assert.Equal(["1p32", "3q21", "1q25", "11q13"], Labels(der.Breakpoints));

        var detailed = Abn("46,XX,der(1)(3qter→3q21::1p32→1q25::11q13→11qter)");
        var part = Assert.Single(detailed.Parts);
        Assert.True(part.IsDetailed);
        Assert.Contains("rule:STRUCT.DETAILED_SYSTEM", Refs(part));
        var structure = Assert.Single(part.Groups).Structure!;
        Assert.Null(structure.LeadingJoin);
        Assert.Null(structure.TrailingJoin);
        Assert.Equal([JoinKind.Junction, JoinKind.Junction], structure.Joins);
        Assert.Equal(["3qter→3q21", "1p32→1q25", "11q13→11qter"], structure.Pieces.Select(p => string.Join("→", p.Points.Select(Label))));
        Assert.Equal(["3q21", "1p32", "1q25", "11q13"], Labels(detailed.Breakpoints));
        Assert.Equal(Labels(der.Breakpoints).Order(), Labels(detailed.Breakpoints).Order());
    }

    [Theory]
    [InlineData("46,XX,del(5)(q13q33)", "46,XX,del(5)(pter→q13::q33→qter)")]
    [InlineData("46,XX,del(5)(q13)", "46,XX,del(5)(pter→q13:)")]
    [InlineData("46,XY,t(2;5)(q21;q31)", "46,XY,t(2;5)(2pter→2q21::5q31→5qter;5pter→5q31::2q21→2qter)")]
    [InlineData("46,XX,i(17)(q10)", "46,XX,i(17)(qter→q10::q10→qter)")]
    [InlineData("46,XX,dup(1)(q22q25)", "46,XX,dup(1)(pter→q25::q22→qter)")]
    [InlineData("46,XX,hsr(1)(p22)", "46,XX,hsr(1)(pter→p22::hsr::p22→qter)")]
    [InlineData("46,XY,der(9)del(9)(p12)del(9)(q31)", "46,XY,der(9)(:p12→q31:)")]
    [InlineData("46,XX,der(1)hsr(1)(p22)hsr(1)(q31)", "46,XX,der(1)(pter→p22::hsr::p22→q31::hsr::q31→qter)")]
    [InlineData("47,XY,+der(?)t(?;9)(?;q22)", "47,XY,+der(?)(?→?cen→?::9q22→9qter)")]
    [InlineData("46,XX,r(7)(p22q36)", "46,XX,r(7)(::p22→q36::)")]
    [InlineData("46,XX,ins(5;2)(p14;q22q32)", "46,XX,ins(5;2)(5pter→5p14::2q32→2q22::5p14→5qter;2pter→2q22::2q32→2qter)")]
    public void ShortAndDetailed_GiveTheSameBreakpoints(string shortForm, string detailedForm)
    {
        var a = Labels(Abn(shortForm).Breakpoints).Distinct().Order();
        var b = Labels(Abn(detailedForm).Breakpoints).Distinct().Order();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Ring_StartsAndEndsWithJunctions()
    {
        var structure = One<DerivativeStructure>(BindOne("46,XX,r(7)(::p22→q36::)"));
        Assert.Equal(JoinKind.Junction, structure.LeadingJoin);
        Assert.Equal(JoinKind.Junction, structure.TrailingJoin);
        Assert.Equal(["7p22→7q36"], structure.Pieces.Select(p => string.Join("→", p.Points.Select(Label))));
    }

    [Fact]
    public void Recombinant_ArmInChromosomeList_AndInheritance()
    {
        var rec = Abn("46,XX,rec(2)dup(2p)inv(2)(p21q31)mat");
        Assert.Equal(["rec", "dup", "inv"], rec.Parts.Select(p => p.Head));
        var dupChrom = Assert.Single(rec.Parts[1].Chromosomes);
        Assert.Equal(("2", "p"), (dupChrom.Name, dupChrom.Arm));
        Assert.Equal(["2p21", "2q31"], Labels(rec.Breakpoints));
        Assert.Equal("mat", rec.Inheritance!.Symbol);
        Assert.Contains("rule:KARYO.PARENTAL_ORIGIN", Refs(rec.Inheritance));
        Assert.Contains("rule:REC.FORMAT", Refs(rec.Parts[0].Symbols[0]));
    }

    [Theory]
    [InlineData("mos 47,XXX[25]/46,XX[5]", CellLineRelation.Mosaic)]
    [InlineData("chi 46,XX[25]/46,XY[10]", CellLineRelation.Chimera)]
    [InlineData("46,XY[3]//46,XX[17]", CellLineRelation.Transplant)]
    [InlineData("45,X[25]/46,X,i(X)(q10)[25]", CellLineRelation.None)]
    public void CellLines_Relation_AndCounts(string text, CellLineRelation relation)
    {
        var model = BindOne(text);
        Assert.Equal(relation, model.Relation);
        Assert.Equal(2, model.CellLines.Length);
        Assert.All(model.CellLines, l => Assert.NotNull(l.CellCount?.Cells));
        Assert.Equal(CellLineSeparator.None, model.CellLines[0].SeparatorBefore);
        Assert.Equal(relation == CellLineRelation.Transplant ? CellLineSeparator.DoubleSlash : CellLineSeparator.Slash, model.CellLines[1].SeparatorBefore);
    }

    [Fact]
    public void MosaicPrefix_CarriesItsRule()
    {
        var model = BindOne("mos 47,XXX[25]/46,XX[5]");
        var prefix = Assert.Single(model.Prefixes);
        Assert.Equal("mos", prefix.Symbol);
        Assert.Equal(["vocab:symbols/mos", "rule:KARYO.MOSAIC_CHIMERA_PREFIX"], Refs(prefix));
        Assert.Equal([25, 5], model.CellLines.Select(l => l.CellCount!.Cells!.Value));
    }

    [Fact]
    public void VariantRegions()
    {
        var region = One<ChromosomeRegion>(BindOne("16qh+", fragment: true));
        Assert.Equal("16", region.Chromosome!.Name);
        Assert.Equal([RegionElementKind.Band, RegionElementKind.Feature, RegionElementKind.Increase], region.Elements.Select(e => e.Kind));
        Assert.Contains("rule:VAR.LENGTH_SIGN", Refs(region));

        var complex = One<ChromosomeRegion>(BindOne("14cenh+pstk+ps+", fragment: true));
        Assert.Equal(["cen", "h", "+", "p", "stk", "+", "ps", "+"], complex.Elements.Select(e => e.Text));

        var band = One<ChromosomeRegion>(BindOne("1p31", fragment: true));
        var bp = Assert.Single(band.Elements).Band!;
        Assert.Equal("1p31", bp.Name);
        Assert.Contains("band:1p31", Refs(bp));
    }

    [Fact]
    public void Copies()
    {
        var del = Abn("46,XX,del(6)(q13q23)×2");
        Assert.Equal(2, del.Copies!.Count);
        Assert.Equal(["6q13", "6q23"], Labels(del.Breakpoints));
    }

    [Fact]
    public void Uncertainty_QuestionMarks()
    {
        Assert.True(Abn("46,XY,?del(1)(q23)").IsQuestioned);
        Assert.Contains("rule:UNCERTAIN.QUESTIONABLE_ITEM", Refs(Abn("46,XY,?del(1)(q23)")));
        Assert.True(Abn("46,XX,der(1)?t(1;3)(p22;q13)").Parts[1].IsQuestioned);

        var b = Assert.Single(Abn("46,XY,del(1)(q2?3)").Breakpoints);
        Assert.True(b.IsQuestioned);
        Assert.Equal("q2?3", b.Text);
        Assert.Null(b.Name);
        Assert.Null(b.KbBand);
        Assert.Contains("rule:UNCERTAIN.QUESTION_MARK_DESIGNATION", Refs(b));

        Assert.Equal("q?", Assert.Single(Abn("46,XX,del(1)(q?)").Breakpoints).Text);

        var minus = One<WholeChromosomeChange>(BindOne("45,XX,–?21"));
        Assert.Equal((GainLoss.Loss, true, "21"), (minus.Sign, minus.IsQuestioned, minus.Chromosome.Name));
    }

    [Fact]
    public void Uncertainty_Intervals()
    {
        var b = Assert.Single(Abn("46,XX,del(1)(q21~24)").Breakpoints);
        Assert.Equal(("q21~24", "1q21", "1q24"), (b.Text, b.Name, b.RangeEnd!.Name));
        Assert.Contains("rule:UNCERTAIN.APPROX_INTERVAL", Refs(b));
        Assert.Contains("band:1q24", Refs(b.RangeEnd));

        Assert.Equal(["1q22", "1q44"], Labels(Abn("46,XY,dup(1)(q22~24q44)").Breakpoints));
        var count = BindOne("43~47,XX,…", fragment: true).CellLines[0].Count!;
        Assert.Equal((43, 47, true), (count.From, count.To, count.IsRange));
    }

    [Fact]
    public void Uncertainty_Alternatives()
    {
        var b = Assert.Single(Abn("46,XX,add(19)(p13 or q13)").Breakpoints);
        Assert.Equal("19p13", b.Name);
        Assert.Equal(["19q13"], Labels(b.Alternatives));
        Assert.Contains("rule:UNCERTAIN.OR_ALTERNATIVE", Refs(b));

        var alt = One<AlternativeItem>(BindOne("46,XY,del(8)(q21.1) or i(8)(p10)"));
        Assert.Equal(["del", "i"], alt.Readings.Cast<Abnormality>().Select(a => a.Kind));
        Assert.Contains("rule:UNCERTAIN.OR_ALTERNATIVE", Refs(alt));

        var p10 = Assert.Single(((Abnormality)alt.Readings[1]).Breakpoints);
        Assert.Equal("8p10", p10.Name);
        Assert.Null(p10.KbBand); // p10/q10 are not bands of the band set (BAND.CENTROMERE_10)
    }

    [Fact]
    public void LeadingCounts_AndNumberedMarkers()
    {
        var model = BindOne("49,XY,…,+3mar,~14dmin", fragment: true);
        var abnormalities = All<Abnormality>(model);
        Assert.Equal(["mar", "dmin"], abnormalities.Select(a => a.Kind));
        Assert.Equal((3, (int?)null, false), (abnormalities[0].LeadingCount!.From, abnormalities[0].LeadingCount!.To, abnormalities[0].LeadingCount!.IsRange));
        Assert.Equal(GainLoss.Gain, abnormalities[0].Sign);
        Assert.Equal(((int?)null, 14, true), (abnormalities[1].LeadingCount!.From, abnormalities[1].LeadingCount!.To, abnormalities[1].LeadingCount!.IsRange));
        Assert.Single(All<OmittedItems>(model));

        var several = All<Abnormality>(BindOne("52,XX,…,+r,+mar,12~20dmin", fragment: true));
        Assert.Equal(["r", "mar", "dmin"], several.Select(a => a.Kind));
        var dmin = several[^1];
        Assert.Equal((12, 20), (dmin.LeadingCount!.From!.Value, dmin.LeadingCount!.To!.Value));

        var mar = All<Abnormality>(BindOne("48,XX,i(17)(q10),+mar1,+mar2[17]/51,XX,i(17)(q10),+mar1×3,+mar2,+mar3[13]"))
            .Where(a => a.Kind == "mar").ToList();
        Assert.Equal([1, 2, 1, 2, 3], mar.Select(a => a.Parts[0].SymbolNumber!.Value));
        Assert.Equal(3, mar[2].Copies!.Count);
    }

    [Fact]
    public void SymbolCombinations()
    {
        var psu = Abn("45,XX,psu dic(15;13)(q12;q12)");
        Assert.Equal(["psu", "dic"], psu.Parts[0].Symbols.Select(s => s.Symbol));
        Assert.Equal("dic", psu.Kind);
        Assert.Equal(["15q12", "13q12"], Labels(psu.Breakpoints));

        var upd = Abn("45,XY,upd der(13;13)(q10;q10)pat");
        Assert.Equal(["upd", "der"], upd.Parts[0].Symbols.Select(s => s.Symbol));
        Assert.Contains("rule:UPD.FORMAT", Refs(upd.Parts[0].Symbols[0]));
        Assert.Contains("rule:DER.DEFINITION", Refs(upd.Parts[0].Symbols[1]));
        Assert.Equal("pat", upd.Inheritance!.Symbol);

        var trc = Abn("47,XX,+trc r(1;3;12)(p36.1q32;q26.3p24;p12q23)");
        Assert.Equal("r", trc.Kind);
        Assert.Equal(["1", "3", "12"], trc.Parts[0].Groups.Select(g => g.Chromosome!.Name));
        Assert.Equal(6, trc.Breakpoints.Count());
    }

    [Fact]
    public void Ploidy_AndConstitutionalMarks()
    {
        var line = BindOne("76~102<4n>,XXXX,…", fragment: true).CellLines[0];
        Assert.Equal((76, 102), (line.Count!.From!.Value, line.Count.To!.Value));
        Assert.Equal(("4n", 4, true), (line.Ploidy!.Text, line.Ploidy.Level, line.Ploidy.InAngleBrackets));
        Assert.Equal(4, line.Sex!.Chromosomes.Length);

        var sex = BindOne("47,XXX?c").CellLines[0].Sex!;
        Assert.Equal(["X", "X", "X"], sex.Chromosomes);
        Assert.True(sex.Constitutional!.IsQuestioned);
        Assert.Contains("rule:KARYO.CONSTITUTIONAL_C", Refs(sex.Constitutional));

        var plus = One<WholeChromosomeChange>(BindOne("46,XY,+21c"));
        Assert.NotNull(plus.Constitutional);
        Assert.False(plus.Constitutional.IsQuestioned);
    }

    [Fact]
    public void Underlined_Homologue()
    {
        var der = Abn("46,XX,der(1̲)t(1;1̲)(p31;q32)");
        var chrom = Assert.Single(der.Parts[0].Chromosomes);
        Assert.Equal(("1", true), (chrom.Name, chrom.IsUnderlined));
        Assert.Equal(["1p31", "1q32"], Labels(der.Breakpoints));
    }

    [Fact]
    public void BandComposition_Fragment()
    {
        var item = One<BandComposition>(BindOne("(pter→q22::q25→q22::q22→qter)", fragment: true));
        var structure = Assert.Single(item.Groups).Structure!;
        Assert.Equal(3, structure.Pieces.Length);
        Assert.Null(Assert.Single(item.Groups).Chromosome);
    }

    [Fact]
    public void KbRef_RoundTrips()
    {
        foreach (var text in new[] { "rule:DEL.FORMAT", "vocab:symbols/del", "band:5q13" })
        {
            Assert.Equal(text, KbRef.Parse(text).ToString());
        }

        Assert.Throws<FormatException>(() => KbRef.Parse("del"));
    }
}
