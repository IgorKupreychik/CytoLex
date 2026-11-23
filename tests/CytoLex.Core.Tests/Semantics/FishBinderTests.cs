using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Tests.Semantics;

/// <summary>T-033: binding of in situ segments to <see cref="FishModel"/>.</summary>
public class FishBinderTests
{
    [Fact]
    public void IshAbnormality_KeepsItsGroupsInOrder()
    {
        var model = BindHelper.BindFish("ish inv(16)(p13.1)(5′CBFB+)(q22)(3′CBFB+)[20]");
        Assert.Equal(NotationType.Ish, model.Type);
        var item = Assert.Single(Assert.Single(model.CellLines).Items);
        Assert.IsType<Abnormality>(item.Abnormality);
        Assert.Equal(
            [FishGroupKind.Chromosomes, FishGroupKind.Breakpoints, FishGroupKind.Loci, FishGroupKind.Breakpoints, FishGroupKind.Loci],
            item.Groups.Select(g => g.Kind));
        Assert.Equal(["5′CBFB", "3′CBFB"], item.LocusGroups.SelectMany(g => g.Loci).Select(l => l.Name));
        Assert.Equal(20, item.Cells!.Count);
    }

    [Fact]
    public void Loci_HaveStatusCountsAndQualifiers()
    {
        var model = BindHelper.BindFish("ish del(X)(p11.2p11.4)(RP1-112K5 dim,RP11-265P11 dim),idic(Y)(q11)(DYZ3++,DYZ1–)");
        var loci = model.LocusGroups.SelectMany(g => g.Loci).ToList();
        Assert.Equal(["RP1-112K5", "RP11-265P11", "DYZ3", "DYZ1"], loci.Select(l => l.Name));
        Assert.Equal(["", "", "++", "–"], loci.Select(l => l.Status));
        Assert.All(loci.Take(2), l => Assert.Equal("dim", Assert.Single(l.Qualifiers).Word));
    }

    [Fact]
    public void NucIsh_HybridizationsAndCellLines()
    {
        var model = BindHelper.BindFish("nuc ish(D13S319×0,LAMP1×2)[100/400]/(ABL1,BCR)×3(ABL1 con BCR×1)[50/400]");
        Assert.Equal(2, model.CellLines.Length);
        Assert.Equal(CellLineSeparator.Slash, model.CellLines[1].Separator);
        var second = Assert.Single(model.CellLines[1].Items).Region!;
        Assert.Equal(2, second.Groups.Length);
        Assert.Equal("3", second.Groups[0].SharedCount!.Text);
        Assert.True(second.Groups[1].IsPosition);
        Assert.Equal([LocusSeparator.Con], second.Groups[1].Separators);
        Assert.Equal((50, 400), (model.CellLines[1].Cells!.Count, model.CellLines[1].Cells!.Total));
    }

    [Fact]
    public void DetailedNucIsh_HasTheBand()
    {
        var region = BindHelper.BindFish("nuc ish 21q22(D21S65×2)").CellLines[0].Items[0].Region!;
        Assert.Equal("21q22", region.Band!.Name);
        Assert.Equal("21", region.Chromosome!.Name);
        Assert.Equal(2, region.Groups[0].Loci[0].Count!.Low);
    }

    [Fact]
    public void Transplant_FromLeadingDoubleSlash()
    {
        var model = BindHelper.BindFish("//nuc ish(DXZ1,DYZ3)×1[400]");
        Assert.True(model.StartsWithDoubleSlash);
        Assert.Equal(CellLineRelation.Transplant, model.Relation);
    }

    [Fact]
    public void Count_RangeAndOpenEnd()
    {
        var count = BindHelper.BindFish("nuc ish(MYCN×12~>50)[200]").LocusGroups.Single().Loci[0].Count!;
        Assert.Equal((12, 50, true), (count.Low, count.High, count.IsOpenEnded));
    }
}
