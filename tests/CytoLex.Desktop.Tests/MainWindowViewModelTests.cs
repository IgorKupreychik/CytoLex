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
        Assert.Empty(vm.Diagnostics);
        Assert.Empty(vm.Segments);
        Assert.Equal("2016", vm.Edition);
    }

    [Fact]
    public void Valid_Karyotype()
    {
        var vm = new MainWindowViewModel { Input = "46,XY,del(5)(q13q33)" };
        Assert.True(vm.HasResult);
        Assert.True(vm.IsValid);
        Assert.Equal("Valid in ISCN 2016", vm.ValidityText);
        var segment = Assert.Single(vm.Segments);
        Assert.Equal("karyotype", segment.Type);
        Assert.Null(segment.Note);
        Assert.Empty(vm.Diagnostics);
        Assert.Empty(vm.Highlights);
    }

    [Fact]
    public void MissingComma_DiagnosticWithCitationAndFix()
    {
        var vm = new MainWindowViewModel { Input = "46XX" };
        Assert.False(vm.IsValid);
        Assert.StartsWith("Not valid in ISCN 2016", vm.ValidityText, StringComparison.Ordinal);
        var item = Assert.Single(vm.Diagnostics, d => d.RuleId == "KARYO.COUNT_SEX_ORDER");
        Assert.Equal("error", item.SeverityLabel);
        Assert.Equal("insert at col 3", item.Location);
        Assert.NotNull(item.Citations);
        Assert.Contains("ISCN 2016 §", item.Citations, StringComparison.Ordinal);
        Assert.Contains(item.Fixes, f => f.StartsWith("Fix", StringComparison.Ordinal) && f.EndsWith("→ 46,XX", StringComparison.Ordinal));
        Assert.Contains(vm.Highlights, h => h.Kind == HighlightKind.Insert && h.InsertText == ",");
    }

    [Fact]
    public void UnknownContext_ValidAsConstitutionalOnly()
    {
        var vm = new MainWindowViewModel { Input = "47,XXX" };
        Assert.True(vm.IsValid);
        Assert.Equal("Valid in ISCN 2016 as constitutional only", vm.ValidityText);
        Assert.StartsWith("Context: unknown", vm.ContextText, StringComparison.Ordinal);
        var item = Assert.Single(vm.Diagnostics);
        Assert.Equal("no finding if constitutional; error if acquired", item.Condition);
        Assert.Contains(item.Fixes, f => f.Contains("(if acquired)", StringComparison.Ordinal) && f.EndsWith("47,XX,+X", StringComparison.Ordinal));
    }

    [Fact]
    public void IshSegment_NotImplemented()
    {
        var vm = new MainWindowViewModel { Input = "46,XX.ish 5p15.2(D5S23×2)" };
        Assert.Equal(["karyotype", "ish"], vm.Segments.Select(s => s.Type));
        Assert.Equal("ish: not implemented in the pilot", vm.Segments[1].Note);
        Assert.Equal("Valid in ISCN 2016 (as far as implemented)", vm.ValidityText);
    }

    [Fact]
    public void SelectedExample_LoadsInput()
    {
        var vm = new MainWindowViewModel { SelectedExample = "46XX" };
        Assert.Equal("46XX", vm.Input);
        Assert.NotEmpty(vm.Diagnostics);
    }

    [Fact]
    public void ExtraSpace_Location()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,del (5)(q13q33)" };
        var item = Assert.Single(vm.Diagnostics, d => d.RuleId == "SYM.NO_SPACE_AT_PARENTHESIS");
        Assert.Equal("col 10: \" \"", item.Location);
    }
}
