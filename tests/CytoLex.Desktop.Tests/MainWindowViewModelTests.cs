using CytoLex.Desktop.Highlighting;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void Blank_NoResult()
    {
        var vm = new MainWindowViewModel();
        Assert.False(vm.HasResult);
        Assert.Empty(vm.Panels);
        Assert.Null(vm.SelectedPanel);
        Assert.Empty(vm.Highlights);
        Assert.Equal("Enter an ISCN string.", vm.Placeholder);
        Assert.Equal("2016", vm.SelectedEditionChoice?.Edition);
    }

    [Fact]
    public void Valid_Karyotype()
    {
        var vm = new MainWindowViewModel { Input = "46,XY,del(5)(q13q33)" };
        Assert.True(vm.HasResult);
        var panel = Assert.Single(vm.Panels);
        Assert.Same(panel, vm.SelectedPanel);
        Assert.True(panel.IsValid);
        Assert.Equal("Valid in ISCN 2016", panel.ValidityText);
        Assert.Equal("2016 ✓ valid", panel.ChipText);
        var segment = Assert.Single(panel.Segments);
        Assert.Equal("karyotype", segment.Type);
        Assert.Null(segment.Note);
        Assert.Empty(panel.Diagnostics);
        Assert.Empty(vm.Highlights);
    }

    [Fact]
    public void MissingComma_DiagnosticWithCitationAndFix()
    {
        var vm = new MainWindowViewModel { Input = "46XX" };
        var panel = vm.SelectedPanel!;
        Assert.False(panel.IsValid);
        Assert.True(panel.IsInvalid);
        Assert.StartsWith("2016 ✗ ", panel.ChipText, StringComparison.Ordinal);
        Assert.StartsWith("Not valid in ISCN 2016", panel.ValidityText, StringComparison.Ordinal);
        var item = Assert.Single(panel.Diagnostics, d => d.RuleId == "KARYO.COUNT_SEX_ORDER");
        Assert.Equal("error", item.SeverityLabel);
        Assert.Equal("insert at col 3", item.Location);
        Assert.NotNull(item.Citations);
        Assert.Contains("ISCN 2016 §", item.Citations, StringComparison.Ordinal);
        Assert.Contains(item.Fixes, f => f.Label.StartsWith("Fix", StringComparison.Ordinal) && f.Preview == "46,XX");
        Assert.Contains(vm.Highlights, h => h.Kind == HighlightKind.Insert && h.InsertText == ",");
    }

    [Fact]
    public void UnknownContext_ValidAsConstitutionalOnly()
    {
        var vm = new MainWindowViewModel { Input = "47,XXX" };
        var panel = vm.SelectedPanel!;
        Assert.True(panel.IsValid);
        Assert.Equal("Valid in ISCN 2016 as constitutional only", panel.ValidityText);
        Assert.StartsWith("Context: unknown", vm.ContextText, StringComparison.Ordinal);
        var item = Assert.Single(panel.Diagnostics);
        Assert.Equal("no finding if constitutional; error if acquired", item.Condition);
        Assert.Contains(item.Fixes, f => f.Label.Contains("(if acquired)", StringComparison.Ordinal) && f.Preview == "47,XX,+X");
    }

    [Fact]
    public void RsaSegment_NotImplemented()
    {
        var vm = new MainWindowViewModel { Input = "46,XX.rsa(13,18,21,X)×2" };
        var panel = vm.SelectedPanel!;
        Assert.Equal(["karyotype", "rsa"], panel.Segments.Select(s => s.Type));
        Assert.Equal("rsa: not implemented in the pilot", panel.Segments[1].Note);
        Assert.Equal("Valid in ISCN 2016 (as far as implemented)", panel.ValidityText);
    }

    [Fact]
    public void IshSegment_IsValidated()
    {
        var vm = new MainWindowViewModel { Input = "46,XX.ish 22q11.2(HIRA×2)" };
        var panel = vm.SelectedPanel!;
        Assert.Equal(["karyotype", "ish"], panel.Segments.Select(s => s.Type));
        Assert.All(panel.Segments, s => Assert.False(s.HasNote));
        Assert.Equal("Valid in ISCN 2016", panel.ValidityText);
    }

    [Fact]
    public void SelectedExample_LoadsInput()
    {
        var vm = new MainWindowViewModel { SelectedExample = "46XX" };
        Assert.Equal("46XX", vm.Input);
        Assert.NotEmpty(vm.SelectedPanel!.Diagnostics);
    }

    [Fact]
    public void ExtraSpace_Location()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,del (5)(q13q33)" };
        var item = Assert.Single(vm.SelectedPanel!.Diagnostics, d => d.RuleId == "SYM.NO_SPACE_AT_PARENTHESIS");
        Assert.Equal("col 10: \" \"", item.Location);
    }

    // Edition selector (T-037)

    [Fact]
    public void EditionChoices_KnownEditionsThenAll()
    {
        var vm = new MainWindowViewModel();
        Assert.Equal(
            ["ISCN 2016", "ISCN 2020 (not implemented)", "ISCN 2024 (not implemented)", "All editions"],
            vm.EditionChoices.Select(c => c.Label));
        Assert.Equal(["2016", "2020", "2024", null], vm.EditionChoices.Select(c => c.Edition));
    }

    [Fact]
    public void SingleUnimplementedEdition_NotImplementedPanel()
    {
        var vm = new MainWindowViewModel { Input = "46XX" };
        vm.SelectedEditionChoice = vm.EditionChoices.Single(c => c.Edition == "2020");

        var panel = Assert.Single(vm.Panels);
        Assert.Same(panel, vm.SelectedPanel);
        Assert.True(panel.IsNotImplemented);
        Assert.False(panel.IsImplemented);
        Assert.False(panel.IsValid);
        Assert.False(panel.IsInvalid);
        Assert.Equal("ISCN 2020: not implemented", panel.ValidityText);
        Assert.Equal("2020 n/a", panel.ChipText);
        Assert.Empty(panel.CountsText);
        Assert.Empty(panel.Diagnostics);
        Assert.Empty(panel.CellLineGroups);
        Assert.Empty(vm.Highlights);
        Assert.Contains("showing: ISCN 2020", vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void AllEditions_OnePanelPerEdition()
    {
        var single = new MainWindowViewModel { Input = "46XX" };
        var vm = new MainWindowViewModel { Input = "46XX" };
        vm.SelectedEditionChoice = EditionChoice.All;

        Assert.Equal(["2016", "2020", "2024"], vm.Panels.Select(p => p.Edition));
        Assert.Equal([false, true, true], vm.Panels.Select(p => p.IsNotImplemented));
        Assert.Equal("2016", vm.SelectedPanel?.Edition);
        Assert.Equal(single.SelectedPanel!.Diagnostics.Select(d => d.RuleId), vm.SelectedPanel!.Diagnostics.Select(d => d.RuleId));
        Assert.Equal(single.Highlights, vm.Highlights);
        Assert.All(vm.Panels.Skip(1), p => Assert.Equal($"ISCN {p.Edition}: not implemented", p.ValidityText));
        Assert.Contains("showing: all editions", vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void AllEditions_TabSwitchSwapsHighlights()
    {
        var vm = new MainWindowViewModel { Input = "46XX", SelectedEditionChoice = EditionChoice.All };
        Assert.NotEmpty(vm.Highlights);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectedPanel = vm.Panels[1];
        Assert.Empty(vm.Highlights);
        Assert.Contains(nameof(MainWindowViewModel.Highlights), raised);

        vm.SelectedPanel = vm.Panels[0];
        Assert.NotEmpty(vm.Highlights);
    }

    [Fact]
    public void AllEditions_RetypingKeepsSelectedTab()
    {
        var vm = new MainWindowViewModel { Input = "46XX", SelectedEditionChoice = EditionChoice.All };
        vm.SelectedPanel = vm.Panels.Single(p => p.Edition == "2024");
        vm.Input = "46,XX";
        Assert.Equal("2024", vm.SelectedPanel?.Edition);
    }

    [Fact]
    public void BackToSingleEdition_OnePanel()
    {
        var vm = new MainWindowViewModel { Input = "46XX", SelectedEditionChoice = EditionChoice.All };
        vm.SelectedEditionChoice = vm.EditionChoices[0];
        var panel = Assert.Single(vm.Panels);
        Assert.Equal("2016", panel.Edition);
        Assert.Same(panel, vm.SelectedPanel);
    }

    [Fact]
    public void SelectingDiagnostic_SelectsSpan()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,del (5)(q13q33)" };
        var item = vm.SelectedPanel!.Diagnostics[0];
        var raised = 0;
        vm.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(MainWindowViewModel.SelectedSpan) ? 1 : 0;

        vm.SelectedPanel.SelectedDiagnostic = item;
        Assert.Equal(1, raised);
        Assert.Equal(item.Span, vm.SelectedSpan);

        // A replaced panel no longer drives the editor.
        var old = vm.SelectedPanel;
        vm.Input = "46XX";
        old.SelectedDiagnostic = null;
        old.SelectedDiagnostic = old.Diagnostics[0];
        Assert.Equal(1, raised);
    }
}
