using CytoLex.Core.Text;
using CytoLex.Desktop.Highlighting;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-069: the abnormalities panel (cell line groups, cards, parts, flags, span selection).</summary>
public class AbnormalityPanelTests
{
    [Fact]
    public void Blank_NoGroups()
    {
        var vm = new MainWindowViewModel();
        Assert.Empty(vm.CellLineGroups);
        Assert.False(vm.HasCellLines);
    }

    [Fact]
    public void SingleCellLine_Cards()
    {
        var vm = new MainWindowViewModel { Input = "47,XY,+8,t(9;22)(q34;q11.2)" };
        var group = Assert.Single(vm.CellLineGroups);
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
            vm.CellLineGroups.Select(g => g.Header));
        Assert.True(vm.CellLineGroups[1].IsEmpty);
    }

    [Fact]
    public void Transplant_RecipientAndDonor()
    {
        var vm = new MainWindowViewModel { Input = "46,XX[3]//46,XY[17]" };
        Assert.Equal(["Cell line 1 (recipient) · 46,XX · [3] · 0 abnormalities", "Cell line 2 (donor) · 46,XY · [17] · 0 abnormalities"], vm.CellLineGroups.Select(g => g.Header));
    }

    [Fact]
    public void Derivative_ExpandsToParts()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,der(1)t(1;3)(p32;q21)t(1;11)(q25;q13)" };
        var card = Assert.Single(Assert.Single(vm.CellLineGroups).Items);
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
        var items = Assert.Single(vm.CellLineGroups).Items;
        Assert.Equal(["?", "c"], items[0].Flags.Select(f => f.Glyph));
        Assert.Equal(["mat"], items[1].Flags.Select(f => f.Glyph));
        Assert.True(items[0].HasFlags);
    }

    [Fact]
    public void Partial_FadedWithDiagnosticsDot()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,del(5)(q13q33" };
        var card = Assert.Single(Assert.Single(vm.CellLineGroups).Items);
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
        var card = Assert.Single(Assert.Single(vm.CellLineGroups).Items);
        Assert.Equal("or", card.KindLabel);
        Assert.Equal("2 readings", card.PartsHeader);
        Assert.Equal(["del(1p)", "dup(1p)"], card.Parts.Select(p => p.ShortForm));
    }

    [Fact]
    public void SelectSpan_RaisedEveryTime()
    {
        var vm = new MainWindowViewModel { Input = "47,XY,+8" };
        var card = Assert.Single(Assert.Single(vm.CellLineGroups).Items);
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
    public void IshOnly_NoKaryotypeItems()
    {
        var vm = new MainWindowViewModel { Input = "46,XX.ish 5p15.2(D5S23×2)" };
        var group = Assert.Single(vm.CellLineGroups);
        Assert.True(group.IsEmpty);
    }
}
