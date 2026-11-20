using CytoLex.Core.Semantics;

namespace CytoLex.Core.Tests.Semantics;

/// <summary>T-034: binding arr trees to <see cref="ArrModel"/>.</summary>
public class ArrBinderTests
{
    [Fact]
    public void Region_HasChromosomeBandsPositionsAndCopyNumber()
    {
        var model = BindHelper.BindArr("arr[GRCh38] 6q21q25.1(113900000_149100000)×1 dn");
        Assert.Equal("GRCh38", model.Build!.Text);
        Assert.True(model.Build.FollowedBySpace);
        var a = Assert.Single(model.Aberrations);
        var region = Assert.Single(a.Regions);
        Assert.Equal("6", region.Chromosome!.Name);
        Assert.Equal(["6q21", "6q25.1"], BindHelper.Labels(region.Bands));
        Assert.All(region.Bands, b => Assert.NotNull(b.KbBand));
        var item = Assert.Single(region.Positions!.Items);
        Assert.Equal((113900000L, 149100000L, "_"), (item.Start!.Value, item.End!.Value, item.Joiner!));
        Assert.Equal((ArrResultKind.CopyNumber, 1, 1), (a.Copy!.Kind, a.Copy.Min, a.Copy.Max));
        Assert.Equal("dn", Assert.Single(a.Qualifiers).Word);
        Assert.Contains("rule:ARR.REGION_FORMAT", BindHelper.Refs(region));
    }

    [Fact]
    public void ChromosomeRanges_AreExpanded()
    {
        var a = Assert.Single(BindHelper.BindArr("arr(1–19,21,22,X)×3").Aberrations);
        Assert.Equal(22, a.ChromosomeNames.Count());
        Assert.DoesNotContain("20", a.ChromosomeNames);
        Assert.Equal("X", a.ChromosomeNames.Last());
    }

    [Fact]
    public void ArmGroup_KeepsTheArm()
    {
        var entry = Assert.Single(Assert.Single(BindHelper.BindArr("arr(16q)×3").Aberrations).Chromosomes);
        Assert.Equal(("16", "q"), (entry.First.Name, entry.First.Arm!));
    }

    [Fact]
    public void ThousandsSeparators_AreReadIntoThePosition()
    {
        var item = BindHelper.BindArr("arr[GRCh38] 4q32.2q35.1(163,146,681_183,022,312)×1").DescendantsAndSelf().OfType<ArrPositionItem>().Single();
        Assert.Equal((163146681L, 183022312L), (item.Start!.Value, item.End!.Value));
        Assert.Equal(4, item.GroupSeparators.Length);
    }

    [Fact]
    public void DetailedSystem_HasACopyNumberPerItem()
    {
        var a = Assert.Single(BindHelper.BindArr("arr[GRCh38] Xq25(126023321×2,126228413_126535347×1,126556900×2)mat").Aberrations);
        Assert.True(a.IsDetailed);
        Assert.Null(a.Copy);
        Assert.Equal([2, 1, 2], a.Regions[0].Positions!.Items.Select(i => i.Copy!.Min!.Value));
        Assert.True(Assert.Single(a.Qualifiers).AfterParenthesis);
    }

    [Fact]
    public void OrReadings_AreAlternatives()
    {
        var region = Assert.Single(Assert.Single(BindHelper.BindArr("arr[GRCh38] Xq28 or Yq12(155790872_156030895 or 56959165_57217415)×1").Aberrations).Regions);
        Assert.Equal("Y", Assert.Single(region.Alternatives).Chromosome!.Name);
        Assert.Single(Assert.Single(region.Positions!.Items).Alternatives);
    }

    [Fact]
    public void ProportionAndRange_AreRead()
    {
        var a = Assert.Single(BindHelper.BindArr("arr(13)×0~1[0.3]").Aberrations);
        Assert.Equal((0, 1, "~"), (a.Copy!.Min!.Value, a.Copy.Max!.Value, a.Copy.RangeSign!));
        Assert.True(a.Proportion!.IsFraction);
        Assert.False(a.Proportion.BeforeCopyNumber);
    }

    [Theory]
    [InlineData("arr(1–22,X)cx", ArrResultKind.Complex)]
    [InlineData("arr(1p)cth", ArrResultKind.Chromothripsis)]
    [InlineData("arr[GRCh38] 7p11.2(54290345_55087100)amp", ArrResultKind.Amplification)]
    public void ResultWords_AreKinds(string text, ArrResultKind kind) =>
        Assert.Equal(kind, Assert.Single(BindHelper.BindArr(text).Aberrations).Copy!.Kind);
}
