using CytoLex.Core.Text;
using CytoLex.Desktop.Highlighting;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-069: the abnormalities panel (cell line groups, cards, parts, flags, span selection).</summary>
public class AbnormalityPanelTests
{
    [Fact]
    public void NotImplementedEdition_NoGroups()
    {
        var vm = new MainWindowViewModel { Input = "46,XX.ish 5p15.2(D5S23×2)" };
        vm.SelectedEditionChoice = vm.EditionChoices.Single(c => c.Edition == "2020");
        Assert.Empty(vm.SelectedPanel!.CellLineGroups);
        Assert.False(vm.SelectedPanel.HasCellLines);
    }

    [Fact]
    public void SingleCellLine_Cards()
    {
        var vm = new MainWindowViewModel { Input = "47,XY,+8,t(9;22)(q34;q11.2)" };
        var group = Assert.Single(vm.SelectedPanel!.CellLineGroups);
        Assert.Equal("Cell line · 47,XY · 2 abnormalities", group.Header);
        Assert.Equal(["gain", "t"], group.Items.Select(i => i.KindLabel));
        var t = group.Items[1];
        Assert.Equal("t(9;22)(q34;q11.2)", t.Text);
        Assert.Equal("t(9;22)", t.ShortForm);
        Assert.True(t.ShowShortForm);
        Assert.False(group.Items[0].ShowShortForm); // "+8" is its own short form
        Assert.Equal(["chr 9", "chr 22", "9q34", "22q11.2"], t.Tags);
        Assert.False(t.HasDiagnostics);
        Assert.False(t.HasParts);
        Assert.Equal(1.0, t.Opacity);
    }

    [Fact]
    public void Mosaic_GroupsByCellLine()
    {
        var vm = new MainWindowViewModel { Input = "mos 47,XX,+21[12]/46,XX[8]" };
        Assert.Equal(
            ["Cell line 1 (mos) · 47,XX · [12] · 1 abnormality", "Cell line 2 (mos) · 46,XX · [8] · 0 abnormalities"],
            vm.SelectedPanel!.CellLineGroups.Select(g => g.Header));
        Assert.True(vm.SelectedPanel!.CellLineGroups[1].IsEmpty);
    }

    [Fact]
    public void Transplant_RecipientAndDonor()
    {
        var vm = new MainWindowViewModel { Input = "46,XX[3]//46,XY[17]" };
        Assert.Equal(["Cell line 1 (recipient) · 46,XX · [3] · 0 abnormalities", "Cell line 2 (donor) · 46,XY · [17] · 0 abnormalities"], vm.SelectedPanel!.CellLineGroups.Select(g => g.Header));
    }

    [Fact]
    public void Derivative_ExpandsToParts()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,der(1)t(1;3)(p32;q21)t(1;11)(q25;q13)" };
        var card = Assert.Single(Assert.Single(vm.SelectedPanel!.CellLineGroups).Items);
        Assert.Equal("der", card.KindLabel);
        Assert.True(card.HasParts);
        Assert.Equal("3 parts", card.PartsHeader);
        Assert.Equal(["der(1)", "t(1;3)", "t(1;11)"], card.Parts.Select(p => p.ShortForm));
        Assert.Equal("chr 1 · chr 3 · 1p32 · 3q21", card.Parts[1].TagsText);
    }

    [Fact]
    public void Flags_Shown()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,?inv(9)(p12q13)c,der(7)t(2;7)(q21;q22)mat" };
        var items = Assert.Single(vm.SelectedPanel!.CellLineGroups).Items;
        Assert.Equal(["?", "c"], items[0].Flags.Select(f => f.Glyph));
        Assert.Equal(["mat"], items[1].Flags.Select(f => f.Glyph));
        Assert.True(items[0].HasFlags);
    }

    [Fact]
    public void Partial_FadedWithDiagnosticsDot()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,del(5)(q13q33" };
        var card = Assert.Single(Assert.Single(vm.SelectedPanel!.CellLineGroups).Items);
        Assert.True(card.IsPartial);
        Assert.Equal(0.5, card.Opacity);
        Assert.Contains(card.Flags, f => f.Glyph == "partial");
        Assert.True(card.HasDiagnostics);
        Assert.Equal(SeverityBrushes.Error, card.DiagnosticsBrush);
        Assert.Contains("error:", card.DiagnosticsTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Alternatives_ReadingsAsParts()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,del(1)(p13) or dup(1)(p13)" };
        var card = Assert.Single(Assert.Single(vm.SelectedPanel!.CellLineGroups).Items);
        Assert.Equal("or", card.KindLabel);
        Assert.Equal("2 readings", card.PartsHeader);
        Assert.Equal(["del(1p)", "dup(1p)"], card.Parts.Select(p => p.ShortForm));
    }

    [Fact]
    public void SelectSpan_RaisedEveryTime()
    {
        var vm = new MainWindowViewModel { Input = "47,XY,+8" };
        var card = Assert.Single(Assert.Single(vm.SelectedPanel!.CellLineGroups).Items);
        var raised = 0;
        vm.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(MainWindowViewModel.SelectedSpan) ? 1 : 0;

        vm.SelectSpan(card.Span);
        vm.SelectSpan(card.Span);

        Assert.Equal(2, raised);
        Assert.Equal(new TextSpan(6, 2), vm.SelectedSpan);
    }

    [Fact]
    public void HoverSpan_ClearedOnNewInput()
    {
        var vm = new MainWindowViewModel { Input = "47,XY,+8" };
        vm.HoverSpan = new TextSpan(6, 2);
        vm.Input = "47,XY,+9";
        Assert.Null(vm.HoverSpan);
    }

    [Fact]
    public void Ish_NoKaryotypeItems_OwnGroup()
    {
        var vm = new MainWindowViewModel { Input = "46,XX.ish 5p15.2(D5S23×2)" };
        var groups = vm.SelectedPanel!.CellLineGroups;
        Assert.Equal(2, groups.Count);
        Assert.True(groups[0].IsEmpty);
        Assert.Equal("In situ (ish) · 1 result", groups[1].Header);
    }

    /// <summary>T-072: an arr segment is one group; each result is a card with its copy number, bands and positions.</summary>
    [Fact]
    public void Arr_GroupAndCards()
    {
        const string input = "arr[GRCh38] 4q32.2q35.1(163146681_183022312)×1 mat,(21)×3,7p11.2(54290345_55087100)amp";
        var vm = new MainWindowViewModel { Input = input };
        var group = Assert.Single(vm.SelectedPanel!.CellLineGroups);
        Assert.Equal("Microarray (arr) · GRCh38 · 3 results", group.Header);
        Assert.Equal("Abnormalities (3)", vm.SelectedPanel.AbnormalitiesHeader);
        Assert.Equal(["×1", "×3", "amp"], group.Items.Select(i => i.KindLabel));

        var region = group.Items[0];
        Assert.Equal("4q32.2q35.1×1", region.ShortForm);
        Assert.Equal(["chr 4", "4q32.2q35.1"], region.Tags);
        Assert.Equal("GRCh38 · chr4:163,146,681–183,022,312", region.PositionsText);
        Assert.Equal(["mat"], region.Flags.Select(f => f.Glyph));
        Assert.NotEqual(group.Items[0].KindBrush, group.Items[2].KindBrush);

        var whole = group.Items[1];
        Assert.Equal(["chr 21"], whole.Tags);
        Assert.False(whole.HasPositions);

        vm.SelectSpan(region.Span);
        Assert.Equal("4q32.2q35.1(163146681_183022312)×1 mat", vm.SelectedSpan!.Value.Of(input));
    }

    [Fact]
    public void Arr_CombinedWithKaryotype_FlagsAndDetailed()
    {
        var vm = new MainWindowViewModel { Input = "46,XX.arr[GRCh38] 4q32.2q35.1(163002425×2,163146681_183022312×1,184322231×2),(1–22,X)×2,(X)×1[0.6],15q11.2q26.3(23123715_101888908)×2 hmz" };
        var groups = vm.SelectedPanel!.CellLineGroups;
        Assert.Equal(["Cell line · 46,XX · 0 abnormalities", "Microarray (arr) · GRCh38 · 4 results"], groups.Select(g => g.Header));
        var items = groups[1].Items;
        Assert.Equal("GRCh38 · chr4:163,002,425 ×2, chr4:163,146,681–183,022,312 ×1, chr4:184,322,231 ×2", items[0].PositionsText);
        Assert.Equal("×1", items[0].KindLabel);
        Assert.Equal(["chr 1–22,X"], items[1].Tags);
        Assert.Equal(["[0.6]"], items[2].Flags.Select(f => f.Glyph));
        Assert.Equal(["hmz"], items[3].Flags.Select(f => f.Glyph));
    }

    /// <summary>T-092: in situ results: one group per cell line, a loci line, normal results muted, click selects the result.</summary>
    [Fact]
    public void InSitu_GroupsAndCards()
    {
        const string input = "46,XY[20].ish 9q34(ABL1×2),del(22)(q11.2q11.2)(D22S75–)[20].nuc ish(ABL1,BCR)×2(ABL1 con BCR×1)[100/400]";
        var vm = new MainWindowViewModel { Input = input };
        var groups = vm.SelectedPanel!.CellLineGroups;
        Assert.Equal(
            ["Cell line · 46,XY · [20] · 0 abnormalities", "In situ (ish) · [20] · 2 results", "In situ (nuc ish) · [100/400] · 1 result"],
            groups.Select(g => g.Header));

        var normal = groups[1].Items[0];
        Assert.Equal("ish", normal.KindLabel);
        Assert.True(normal.IsNormal);
        Assert.Equal(0.6, normal.Opacity);
        Assert.Equal("ABL1×2", normal.LociText);
        Assert.Equal(["normal"], normal.Flags.Select(f => f.Glyph));
        Assert.Equal(["chr 9", "9q34"], normal.Tags);

        var del = groups[1].Items[1];
        Assert.Equal("del", del.KindLabel);
        Assert.Equal("del(22q)", del.ShortForm);
        Assert.Equal("D22S75–", del.LociText);
        Assert.Equal(["[20]"], del.Flags.Select(f => f.Glyph));
        Assert.Equal(1.0, del.Opacity);
        Assert.NotEqual(normal.KindBrush, del.KindBrush);

        var nuc = Assert.Single(groups[2].Items);
        Assert.Equal("nuc ish", nuc.KindLabel);
        Assert.False(nuc.IsNormal);
        Assert.Equal("ABL1×2, BCR×2 · ABL1 con BCR×1", nuc.LociText);
        Assert.Equal(["[100/400]"], nuc.Flags.Select(f => f.Glyph));

        vm.SelectSpan(del.Span);
        Assert.Equal("del(22)(q11.2q11.2)(D22S75–)[20]", vm.SelectedSpan!.Value.Of(input));
    }

    [Fact]
    public void InSitu_TransplantCellLines()
    {
        var vm = new MainWindowViewModel { Input = "nuc ish(DXZ1×2)[50]//(DXZ1,DYZ3)×1[300]/(DXZ1×1)[10]" };
        Assert.Equal(
            [
                "In situ (nuc ish) · cell line 1 (recipient) · [50] · 1 result",
                "In situ (nuc ish) · cell line 2 (donor) · [300] · 1 result",
                "In situ (nuc ish) · cell line 3 (donor) · [10] · 1 result",
            ],
            vm.SelectedPanel!.CellLineGroups.Select(g => g.Header));

        vm.Input = "//nuc ish(DXZ1,DYZ3)×1[400]";
        Assert.Equal(["In situ (nuc ish) · cell line (donor) · [400] · 1 result"], vm.SelectedPanel!.CellLineGroups.Select(g => g.Header));
    }

    /// <summary>T-066: a card shows the detailed-system form when it differs from the written text.</summary>
    [Fact]
    public void DetailedForm_Shown()
    {
        var vm = new MainWindowViewModel { Input = "47,XY,+8,der(9)t(9;22)(q34;q11.2)" };
        var items = Assert.Single(vm.SelectedPanel!.CellLineGroups).Items;
        Assert.False(items[0].ShowDetailed);
        Assert.Equal("der(9)(9pter→9q34::22q11.2→22qter)", items[1].Detailed);
        Assert.True(items[1].ShowDetailed);

        vm.Input = "46,XX,der(9)(9pter→9q34::22q11.2→22qter)";
        var written = Assert.Single(Assert.Single(vm.SelectedPanel!.CellLineGroups).Items);
        Assert.False(written.ShowDetailed); // already written in the detailed system
    }
}
