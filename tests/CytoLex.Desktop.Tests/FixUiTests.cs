using CytoLex.Core.Validation;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-040: fix diffs, Apply → re-validate, and the one-round "Fix all".</summary>
public class FixUiTests
{
    private static string Render(IEnumerable<DiffPartViewModel> parts) =>
        string.Concat(parts.Select(p => p.Kind switch
        {
            DiffKind.Removed => $"[-{p.Text}]",
            DiffKind.Added => $"[+{p.Text}]",
            _ => p.Text,
        }));

    [Fact]
    public void MissingComma_DiffAndApply_Revalidates()
    {
        var vm = new MainWindowViewModel { Input = "46XX" };
        var fix = Assert.Single(vm.SelectedPanel!.Diagnostics.Single(d => d.RuleId == "KARYO.COUNT_SEX_ORDER").Fixes);
        Assert.True(fix.IsEdit);
        Assert.True(fix.CanApply);
        Assert.StartsWith("Fix: ", fix.Label, StringComparison.Ordinal);
        Assert.Equal("46[+,]XX", Render(fix.DiffParts));

        fix.ApplyCommand.Execute(null);

        Assert.Equal("46,XX", vm.Input);
        Assert.True(vm.SelectedPanel!.IsValid);
        Assert.True(vm.HasLastApplied);
        Assert.StartsWith("Applied: ", vm.LastApplied, StringComparison.Ordinal);

        // Typing clears the note.
        vm.Input = "46,XY";
        Assert.False(vm.HasLastApplied);
    }

    [Fact]
    public void RemovedSpace_IsShownAsVisibleDot()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,del (5)(q13q33)" };
        var fix = vm.SelectedPanel!.Diagnostics.Single(d => d.RuleId == "SYM.NO_SPACE_AT_PARENTHESIS").Fixes.First(f => f.IsEdit);
        var removed = Assert.Single(fix.DiffParts, p => p.IsRemoved);
        Assert.Equal("·", removed.Text);
        Assert.Equal(" ", removed.Part.Text);
    }

    [Fact]
    public void FixAll_OneRound_ThenGone()
    {
        var vm = new MainWindowViewModel { Input = "46XX,del (5)(q13q33)" };
        var panel = vm.SelectedPanel!;
        Assert.True(panel.HasFixAll);
        Assert.Equal(2, panel.FixAll!.Count);
        Assert.Equal("Fix all (2)", panel.FixAll.Header);
        Assert.Equal("46[+,]XX,del[-·](5)(q13q33)", Render(panel.FixAll.DiffParts));

        panel.FixAll.ApplyCommand.Execute(null);

        Assert.Equal("46,XX,del(5)(q13q33)", vm.Input);
        Assert.True(vm.SelectedPanel!.IsValid);
        Assert.False(vm.SelectedPanel.HasFixAll);
    }

    [Fact]
    public void FixAll_NeedsTwoFixes()
    {
        var vm = new MainWindowViewModel { Input = "46XX" };
        Assert.False(vm.SelectedPanel!.HasFixAll);
    }

    [Fact]
    public void ConditionalFix_LabelledWithContext_NotInFixAll()
    {
        var vm = new MainWindowViewModel { Input = "47,XXX" };
        var fix = vm.SelectedPanel!.Diagnostics.Single().Fixes.Single(f => f.IsEdit);
        Assert.StartsWith("Fix (if acquired): ", fix.Label, StringComparison.Ordinal);
        Assert.Equal("47,XX,+X", fix.Preview);
        Assert.False(vm.SelectedPanel.HasFixAll);
    }

    [Fact]
    public void Explanation_HasNoDiffOrApply()
    {
        var vm = new MainWindowViewModel { Input = "46,XX,del(5q)" };
        var fix = vm.SelectedPanel!.Diagnostics.Single(d => d.RuleId == "DEL.FORMAT").Fixes.Single();
        Assert.False(fix.IsEdit);
        Assert.False(fix.CanApply);
        Assert.Empty(fix.DiffParts);
        Assert.Null(fix.Preview);
        Assert.StartsWith("Explanation: ", fix.Label, StringComparison.Ordinal);
        Assert.False(fix.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void NotImplementedEdition_HasNoFixAll()
    {
        var vm = new MainWindowViewModel { Input = "46XX,del (5)(q13q33)" };
        vm.SelectedEditionChoice = vm.EditionChoices.Single(c => c.Edition is null);
        Assert.Contains(vm.Panels, p => p.IsImplemented && p.HasFixAll);
        Assert.All(vm.Panels.Where(p => p.IsNotImplemented), p => Assert.False(p.HasFixAll));
    }

    [Fact]
    public void DiagnosticWithoutWindow_HasNoApply()
    {
        // Built without an apply handler (e.g. elsewhere than the main window): the diff is shown, Apply is not.
        var report = Validator.Default.Validate("46XX");
        var item = new DiagnosticItemViewModel(report.Input, report.Results[0].Diagnostics[0], 0, "2016");
        var fix = item.Fixes.Single(f => f.IsEdit);
        Assert.False(fix.CanApply);
        Assert.NotEmpty(fix.DiffParts);
    }
}
