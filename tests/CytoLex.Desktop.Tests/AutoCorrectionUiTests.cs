using CytoLex.Core.Batch;
using CytoLex.Core.Kb;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;
using CytoLex.Desktop.Rules;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>
/// T-097: autocorrection in the desktop — the checkbox, the "Corrected" line, highlights and fixes on the typed text,
/// the verdict, Use corrected, the Batch tab and the rule browser badge.
/// </summary>
public sealed class AutoCorrectionUiTests : IDisposable
{
    private static readonly IKnowledgeBase _kb = KnowledgeBase.LoadEmbedded();
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cytolex-autocorrect-vm-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static bool IsAutoRule(string ruleId) => _kb.ForEdition("2016")?.Rule(ruleId)?.Fix?.Auto == true;

    // Checkbox and verdict

    [Fact]
    public void Checkbox_IsOnByDefault_AndTurnsAutocorrectionOff()
    {
        var vm = new MainWindowViewModel { Input = "47,XY, +21" };
        Assert.True(vm.AutoCorrect);
        var panel = vm.SelectedPanel!;
        Assert.True(panel.IsValid);
        Assert.True(panel.IsAutoCorrected);
        Assert.False(panel.IsValidAsWritten);
        Assert.Equal("Valid in ISCN 2016 after autocorrection", panel.ValidityText);
        Assert.Equal("2016 ✓ valid after autocorrection", panel.ChipText);
        Assert.Empty(panel.Diagnostics);

        vm.AutoCorrect = false;
        panel = vm.SelectedPanel!;
        Assert.False(panel.IsValid);
        Assert.False(panel.IsAutoCorrected);
        Assert.Equal("Not valid in ISCN 2016", panel.ValidityText);
        Assert.Contains(panel.Diagnostics, d => d.RuleId == "SYM.NO_OTHER_SPACE");
        Assert.False(panel.HasCorrection);
    }

    [Fact]
    public void ValidAsWritten_KeepsThePlainVerdict()
    {
        var panel = new MainWindowViewModel { Input = "46,XX,del(5)(q13q33)" }.SelectedPanel!;
        Assert.True(panel.IsValidAsWritten);
        Assert.False(panel.IsAutoCorrected);
        Assert.Equal("2016 ✓ valid", panel.ChipText);
        Assert.Equal("Valid in ISCN 2016", panel.ValidityText);
    }

    // Corrected line

    [Theory]
    [InlineData("46,xy", "46,XY", "SYM.LETTER_CASE")]
    [InlineData("46,XY,T(9;22)(q34;q11.2)", "46,XY,t(9;22)(q34;q11.2)", "SYM.LETTER_CASE")]
    [InlineData("47,XY, +21", "47,XY,+21", "SYM.NO_OTHER_SPACE")]
    public void CorrectedLine_ShowsTheStringAndOneNotePerCorrection(string input, string corrected, string ruleId)
    {
        var panel = new MainWindowViewModel { Input = input }.SelectedPanel!;
        Assert.True(panel.HasCorrection);
        Assert.Equal(corrected, panel.Corrected);
        var note = Assert.Single(panel.Corrections);
        Assert.Equal(ruleId, note.RuleId);
        Assert.True(IsAutoRule(note.RuleId));
        Assert.True(note.HasCitation);
        Assert.StartsWith("ISCN 2016", note.Citation, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrectedLine_IsHidden_ForAValidString_AndWithTheCheckboxOff()
    {
        var vm = new MainWindowViewModel { Input = "46,XX" };
        Assert.False(vm.SelectedPanel!.HasCorrection);
        Assert.Null(vm.SelectedPanel.Corrected);
        Assert.Empty(vm.SelectedPanel.Corrections);

        vm.Input = "46,xy";
        Assert.True(vm.SelectedPanel!.HasCorrection);
        vm.AutoCorrect = false;
        Assert.False(vm.SelectedPanel!.HasCorrection);
    }

    [Fact]
    public void NoteRuleLink_OpensTheRuleBrowser()
    {
        var vm = new MainWindowViewModel { Input = "46,xy" };
        var note = Assert.Single(vm.SelectedPanel!.Corrections);

        note.OpenRuleCommand.Execute(null);

        Assert.Equal(MainWindowViewModel.RulesTab, vm.SelectedMainTab);
        Assert.Equal("SYM.LETTER_CASE", vm.Rules.Detail?.Id);
    }

    [Fact]
    public void UseCorrected_SetsTheInput_AsAnAppliedChange()
    {
        var vm = new MainWindowViewModel { Input = "46,xy" };

        vm.UseCorrectedCommand.Execute(null);

        // The editor takes Input through a document replace, so Ctrl+Z undoes it (checked by hand).
        Assert.Equal("46,XY", vm.Input);
        Assert.Equal("Applied: the autocorrected string", vm.LastApplied);
        Assert.False(vm.SelectedPanel!.HasCorrection);
        Assert.Equal("2016 ✓ valid", vm.SelectedPanel.ChipText);
    }

    // Highlights, spans and tooltip on the typed text

    [Fact]
    public void Highlights_PointIntoTheTypedText()
    {
        const string input = "46, xy,inv(3)(q27p21)";
        var vm = new MainWindowViewModel { Input = input };
        var panel = vm.SelectedPanel!;
        Assert.Equal("46,XY,inv(3)(q27p21)", panel.Corrected);

        // Two soft highlights: the stray space and the letter case.
        var auto = panel.Highlights.Where(h => h.Kind == HighlightKind.AutoCorrected).ToList();
        Assert.Equal(2, auto.Count);
        Assert.Contains(auto, h => h.Span.Of(input) == " " && h.Note!.Contains("SYM.NO_OTHER_SPACE", StringComparison.Ordinal));
        Assert.Contains(auto, h => h.Span.Of(input) == "xy" && h.Note!.Contains("SYM.LETTER_CASE", StringComparison.Ordinal));
        Assert.All(auto, h => Assert.Equal(-1, h.DiagnosticIndex));

        // The remaining error is highlighted where it is in the typed text, and its diagnostic selects that span.
        var error = Assert.Single(panel.Diagnostics, d => d.SeverityLabel == "error");
        Assert.Equal("q27p21", error.Span.Of(input));
        var own = panel.Highlights.Where(h => h.DiagnosticIndex == error.Index).Select(h => h.Span.Of(input)).ToList();
        Assert.Equal(["q27", "p21"], own); // the reorder fix: one edit per breakpoint
        Assert.Contains("q27p21", error.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorToolTip_NamesTheRule_AtAnAutocorrectedSpot()
    {
        const string input = "46, xy,inv(3)(q27p21)";
        var vm = new MainWindowViewModel { Input = input };

        vm.HoverOffset = input.IndexOf('x', StringComparison.Ordinal);
        Assert.StartsWith("Autocorrected (SYM.LETTER_CASE)", vm.EditorToolTip, StringComparison.Ordinal);

        vm.HoverOffset = input.IndexOf("q27", StringComparison.Ordinal);
        Assert.Contains("error (", vm.EditorToolTip, StringComparison.Ordinal);

        vm.HoverOffset = 0;
        Assert.Null(vm.EditorToolTip);
        vm.HoverOffset = null;
        Assert.Null(vm.EditorToolTip);
    }

    [Fact]
    public void AbnormalityAndTranslationSpans_PointIntoTheTypedText()
    {
        const string input = "46, xy,inv(3)(q27p21)";
        var vm = new MainWindowViewModel { Input = input };
        var panel = vm.SelectedPanel!;

        var card = Assert.Single(panel.CellLineGroups.SelectMany(g => g.Items));
        Assert.Equal("inv(3)(q27p21)", card.Span.Of(input));

        var offset = input.IndexOf("inv", StringComparison.Ordinal);
        vm.HoverOffset = offset;
        Assert.Equal("inv(3)(q27p21)", vm.HoverSpan?.Of(input));
    }

    // Fixes of the remaining diagnostics apply onto the typed text

    [Fact]
    public void RemainingFix_AppliesOntoTheTypedText()
    {
        const string input = "46, xy,inv(3)(q27p21)";
        var vm = new MainWindowViewModel { Input = input };
        var fix = vm.SelectedPanel!.Diagnostics.SelectMany(d => d.Fixes).First(f => f.IsEdit);

        Assert.Equal("46, xy,inv(3)(p21q27)", fix.Preview);
        Assert.DoesNotContain(EditTarget.WritesCorrectionsNote, fix.Label, StringComparison.Ordinal);
        fix.ApplyCommand.Execute(null);

        // The typed faults stay (they are autocorrected again); only the fix's own edit was applied.
        Assert.Equal("46, xy,inv(3)(p21q27)", vm.Input);
        Assert.Equal(EditionVerdict.ValidAfterAutoCorrection, vm.SelectedPanel!.Result.Verdict);
    }

    [Fact]
    public void FixTouchingAutocorrectedText_FallsBackToTheCorrectedString()
    {
        var report = Validator.Default.Validate("46,xy", new ValidationOptions { Editions = EditionSelection.One("2016"), AutoCorrect = true });
        var text = EditionText.Of(report, report.Results.Single());
        var edit = new TextEdit(new TextSpan(3, 2), "XX"); // "XY" of the corrected string: written by autocorrection
        var fix = new Fix(FixLevel.Deterministic, null, [edit], "Write XX", null, "46,XX", []);

        var target = text.Target(fix.Edits);
        Assert.False(target.OnTypedText);
        Assert.Equal("46,XY", target.Base);

        string? applied = null;
        var item = new FixItemViewModel(text, fix, (newText, _) => applied = newText);
        Assert.EndsWith($"({EditTarget.WritesCorrectionsNote})", item.Label, StringComparison.Ordinal);
        Assert.Equal("46,XX", item.Preview);
        item.ApplyCommand.Execute(null);
        Assert.Equal("46,XX", applied);
    }

    // The core result keeps its own spans (the corrected string)

    [Fact]
    public void Panels_KeepTheResultSpansOfTheCorrectedString()
    {
        var vm = new MainWindowViewModel { Input = "46,xy" };
        Assert.Equal("46,XY", vm.SelectedPanel!.Result.TextOf(vm.Input));
    }

    // Batch tab

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir.FullName, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task Batch_CorrectedColumn_Filter_Summary()
    {
        var batch = new MainWindowViewModel().Batch;
        Assert.True(batch.AutoCorrect);
        await batch.LoadAsync(Write("in.csv", "id,iscn\nok,\"46,XX\"\nlower,\"46,xy\"\nbad,46XX\n"));

        await batch.RunCommand.ExecuteAsync(null);

        Assert.True(batch.HasCorrectedRows);
        var lower = batch.Rows.Single(r => r.Id == "lower");
        Assert.True(lower.IsAutoCorrected);
        Assert.Equal("46,XY", lower.Corrected);
        Assert.Contains("SYM.LETTER_CASE", lower.CorrectedTip, StringComparison.Ordinal);
        Assert.Equal("✓ auto", Assert.Single(lower.Cells).Text);
        Assert.Contains("after autocorrection", lower.Cells[0].Tip, StringComparison.Ordinal);
        Assert.True(lower.IsValid);
        Assert.Equal(string.Empty, batch.Rows.Single(r => r.Id == "ok").Corrected);
        Assert.StartsWith("ISCN 2016: 1 valid, 1 valid after autocorrection, 1 not valid of 3", Assert.Single(batch.Summaries), StringComparison.Ordinal);

        batch.SelectedFilter = batch.Filters.Single(f => f.Filter == BatchFilter.AutoCorrected);
        Assert.Equal(["lower"], batch.Rows.Select(r => r.Id));

        // The export carries the corrected string (T-096).
        var csv = Path.Combine(_dir.FullName, "out.csv");
        await batch.ExportAsync(csv);
        Assert.Contains("46,XY", await File.ReadAllTextAsync(csv, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Batch_CheckboxOff_ValidatesAsWritten_AndDropsTheResult()
    {
        var batch = new MainWindowViewModel().Batch;
        await batch.LoadAsync(Write("in.txt", "46,xy\n"));
        await batch.RunCommand.ExecuteAsync(null);
        Assert.True(batch.HasResult);

        batch.AutoCorrect = false;
        Assert.False(batch.HasResult);
        Assert.False(batch.HasCorrectedRows);

        await batch.RunCommand.ExecuteAsync(null);
        var row = Assert.Single(batch.Rows);
        Assert.False(row.IsAutoCorrected);
        Assert.True(row.IsNotValid);
        Assert.Equal(string.Empty, row.Corrected);
        Assert.Null(row.CorrectedTip);
    }

    // Rule browser badge

    [Fact]
    public void RuleBrowser_ShowsTheAutocorrectionBadge()
    {
        using var store = RuleStore.Open(RuleStore.DefaultPath);
        Assert.True(store.Rule("2016", "SYM.LETTER_CASE")!.FixAuto);
        Assert.False(store.Rule("2016", "KARYO.COUNT_SEX_ORDER")!.FixAuto);

        var rules = new MainWindowViewModel().Rules;
        Assert.True(rules.OpenRule("2016", "SYM.LETTER_CASE"));
        Assert.True(rules.Detail!.IsAutoCorrected);
    }
}
