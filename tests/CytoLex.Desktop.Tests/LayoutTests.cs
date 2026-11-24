using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-098: the edition chip strip and the full-width view tabs of the single-string check.</summary>
public sealed class LayoutTests
{
    private static MainWindowViewModel AllEditions(string input)
    {
        var vm = new MainWindowViewModel();
        vm.SelectedEditionChoice = vm.EditionChoices.Single(c => c.Edition is null);
        vm.Input = input;
        return vm;
    }

    [Fact]
    public void AllEditions_OneChipPerEdition_SelectingAChipSelectsItsHighlights()
    {
        var vm = AllEditions("46XX");
        Assert.Equal(vm.EditionChoices.Count - 1, vm.Panels.Count);
        var p2016 = vm.Panels.Single(p => p.Edition == "2016");
        var p2020 = vm.Panels.Single(p => p.Edition == "2020");
        Assert.Same(p2016, vm.SelectedPanel); // the first implemented edition
        Assert.StartsWith("2016 ✗ ", p2016.ChipText, StringComparison.Ordinal);
        Assert.Equal("2020 n/a", p2020.ChipText);
        Assert.Equal("ISCN 2020 is not implemented.", p2020.NotImplementedText);
        Assert.NotEmpty(vm.Highlights);

        vm.SelectedPanel = p2020; // the user clicks the 2020 chip
        Assert.Empty(vm.Highlights);

        vm.SelectedPanel = p2016;
        Assert.NotEmpty(vm.Highlights);
    }

    [Fact]
    public void SelectedEdition_KeptWhileTyping()
    {
        var vm = AllEditions("46XX");
        vm.SelectedPanel = vm.Panels.Single(p => p.Edition == "2020");
        vm.Input = "46XY";
        Assert.Equal("2020", vm.SelectedPanel!.Edition);
    }

    [Theory]
    [InlineData(ResultView.Abnormalities)]
    [InlineData(ResultView.Translation)]
    [InlineData(ResultView.CytoGps)]
    public void View_KeptWhenTheEditionTheSelectorOrTheInputChanges(ResultView view)
    {
        var vm = AllEditions("46,XY,del(5)(q13q33)");
        vm.SelectedViewIndex = (int)view;

        vm.SelectedPanel = vm.Panels.Single(p => p.Edition == "2020"); // chip
        Assert.Equal(view, vm.SelectedView);

        vm.SelectedEditionChoice = vm.EditionChoices.Single(c => c.Edition == "2016"); // edition selector
        Assert.Equal(view, vm.SelectedView);

        vm.Input = "46XX"; // typing
        Assert.Equal(view, vm.SelectedView);

        vm.ApplyFix("46,XX", "insert a comma"); // Apply
        Assert.Equal(view, vm.SelectedView);
    }

    [Fact]
    public void SelectedViewIndex_IgnoresUnknownIndices()
    {
        var vm = new MainWindowViewModel { Input = "46,XY" };
        vm.SelectedViewIndex = (int)ResultView.Translation;
        vm.SelectedViewIndex = -1; // a tab control without a selection
        Assert.Equal(ResultView.Translation, vm.SelectedView);
    }

    [Fact]
    public void TranslationDiagnosticLink_ShowsTheDiagnosticsView()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,del(5)(q13q33)t(1;3)(p32;q21" };
        vm.SelectedViewIndex = (int)ResultView.Translation;
        var panel = vm.SelectedPanel!;
        var link = panel.Translation!.Items.First(i => !i.IsTranslated).Diagnostics[0];

        link.SelectCommand.Execute(null);

        Assert.Equal(ResultView.Diagnostics, vm.SelectedView);
        Assert.Same(panel.Diagnostics[link.Index], panel.SelectedDiagnostic);
        Assert.Equal(panel.Diagnostics[link.Index].Span, vm.SelectedSpan);
    }

    [Fact]
    public void HoverLinks_WorkInEveryView()
    {
        var vm = new MainWindowViewModel { Input = "46,XY,del(5)(q13q33)" };
        vm.SelectedViewIndex = (int)ResultView.Abnormalities;
        var translation = vm.SelectedPanel!.Translation!;
        var del = translation.Items[0];

        vm.HoverOffset = del.Span.Start + 1; // editor → part
        Assert.True(del.IsHighlighted);
        Assert.Equal(del.Span, vm.HoverSpan);
        vm.HoverOffset = null;
        Assert.Null(vm.HoverSpan);

        vm.HoverTranslationItem(del, hovered: true); // card → editor
        Assert.Equal(del.Span, vm.HoverSpan);

        var card = vm.SelectedPanel.CellLineGroups.Single().Items.Single();
        vm.SelectSpan(card.Span); // a tapped abnormality card selects its span
        Assert.Equal(card.Span, vm.SelectedSpan);
        Assert.Equal(ResultView.Abnormalities, vm.SelectedView);
    }

    [Fact]
    public void ViewHeaders_CountDiagnosticsAndAbnormalities()
    {
        var vm = new MainWindowViewModel { Input = "47,XY,del(5)(q13q33),+21" };
        Assert.Equal("Abnormalities (2)", vm.SelectedPanel!.AbnormalitiesHeader);
        Assert.Equal("Diagnostics", vm.SelectedPanel.DiagnosticsHeader);
    }

    [Theory]
    [InlineData(false, true, 0, "2016 ✓ valid")]
    [InlineData(false, false, 1, "2016 ✗ 1 error")]
    [InlineData(false, false, 3, "2016 ✗ 3 errors")]
    [InlineData(false, false, 0, "2016 ✗ not valid")]
    [InlineData(true, false, 0, "2016 n/a")]
    public void ChipText(bool notImplemented, bool valid, int errors, string expected) =>
        Assert.Equal(expected, EditionPanelViewModel.ChipOf("2016", notImplemented, valid, errors));

    [Fact]
    public void ChipTip_VerdictAndCounts()
    {
        var vm = new MainWindowViewModel { Input = "46XX" };
        var panel = vm.SelectedPanel!;
        Assert.Equal($"{panel.ValidityText} · {panel.CountsText}", panel.ChipTip);
    }
}
