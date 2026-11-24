using CytoLex.Core.Validation;
using CytoLex.Desktop.Rules;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-039: the Rules tab (search, filters, detail, related links, examples) and the diagnostic → rule link.</summary>
public sealed class RuleBrowserTests
{
    [Fact]
    public void StartsWithEveryRuleOfTheDefaultEdition()
    {
        var main = new MainWindowViewModel();
        var rules = main.Rules;
        Assert.True(rules.IsAvailable);
        Assert.Equal("2016", rules.SelectedEdition);
        Assert.Same(SectionChoice.All, rules.SelectedSection);
        var ekb = CytoLex.Core.Kb.KnowledgeBase.LoadEmbedded().ForEdition("2016")!;
        Assert.Equal(ekb.Rules.Count, rules.Hits.Count(h => h.Kind == KbEntryKind.Rule));
        Assert.Equal(ekb.Vocab.Entries.Length, rules.Hits.Count(h => h.Kind == KbEntryKind.Vocab)); // T-059
        Assert.Null(rules.Detail);
        Assert.EndsWith("vocab item(s)", rules.ResultText, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_FiltersAndSelectsTheBestHit()
    {
        var rules = new MainWindowViewModel().Rules;
        rules.Query = "TRANSLOC.JUMPING";
        Assert.Equal("TRANSLOC.JUMPING", rules.Hits[0].Id);
        Assert.Equal("TRANSLOC.JUMPING", rules.Detail?.Id);
        Assert.EndsWith("match", rules.ResultText, StringComparison.Ordinal);

        rules.Query = "zzqqxx-no-such-word";
        Assert.Empty(rules.Hits);
        Assert.Null(rules.Detail);
    }

    [Fact]
    public void SectionFilter_NarrowsTheList()
    {
        var rules = new MainWindowViewModel().Rules;
        var all = rules.Hits.Count;
        rules.SelectedSection = rules.Sections.First(s => s.Section == "9.2.17.1");
        Assert.InRange(rules.Hits.Count, 1, all - 1);
        Assert.All(rules.Hits, h => Assert.Equal("9.2.17.1", h.Hit.Section));
    }

    [Fact]
    public void Detail_ShowsStatementCitationsExamplesAndQuotes()
    {
        var rules = new MainWindowViewModel().Rules;
        Assert.True(rules.OpenRule("2016", "TRANSLOC.FORMAT"));
        var detail = rules.Detail!;
        Assert.Equal("TRANSLOC.FORMAT", detail.Id);
        Assert.False(string.IsNullOrWhiteSpace(detail.Statement));
        Assert.NotEmpty(detail.Citations);
        Assert.StartsWith("ISCN 2016 §", detail.Citations[0], StringComparison.Ordinal);
        Assert.True(detail.HasValidExamples);
        Assert.True(detail.HasQuotes);
        Assert.StartsWith("Applies to: ", detail.Scope, StringComparison.Ordinal);
    }

    [Fact]
    public void RelatedLink_OpensTheRelatedRule()
    {
        var rules = new MainWindowViewModel().Rules;
        Assert.True(rules.OpenRule("2016", "BAND.REGION_LANDMARKS"));
        var related = Assert.Single(rules.Detail!.Related, r => r.Id == "BAND.REGION_NUMBERING");
        related.OpenCommand.Execute(null);
        Assert.Equal("BAND.REGION_NUMBERING", rules.Detail!.Id);
        Assert.Equal("BAND.REGION_NUMBERING", rules.SelectedHit?.Id);
    }

    [Fact]
    public void OpenRule_ClearsFiltersThatHideIt()
    {
        var rules = new MainWindowViewModel().Rules;
        rules.Query = "translocation";
        rules.SelectedSection = rules.Sections.First(s => s.Section == "9.2.17.1");
        Assert.True(rules.OpenRule("2016", "CHROM.IDENTIFIERS"));
        Assert.Equal(string.Empty, rules.Query);
        Assert.Same(SectionChoice.All, rules.SelectedSection);
        Assert.Equal("CHROM.IDENTIFIERS", rules.Detail?.Id);
    }

    [Fact]
    public void OpenRule_UnknownRule_SaysSo()
    {
        var rules = new MainWindowViewModel().Rules;
        Assert.False(rules.OpenRule("2016", "CORE.UNPARSED"));
        Assert.Contains("CORE.UNPARSED", rules.ResultText, StringComparison.Ordinal);
        Assert.False(rules.OpenRule("2024", "TRANSLOC.FORMAT"));
    }

    [Theory]
    [InlineData("46XX")]
    [InlineData("46,XX,del (5)(q13q33)")]
    [InlineData("47,XY,+X")]
    [InlineData("46,XY,t(22;9)(q11.2;q34)")]
    public void DiagnosticLink_OpensItsRuleInThePanelsEdition(string input)
    {
        var main = new MainWindowViewModel { AutoCorrect = false, Input = input }; // as written (T-097)
        var panel = main.SelectedPanel!;
        var linked = panel.Diagnostics.Where(d => d.HasKbRule).ToList();
        Assert.NotEmpty(linked);
        foreach (var diagnostic in linked)
        {
            main.SelectedMainTab = MainWindowViewModel.SingleTab;
            Assert.Equal(panel.Edition, diagnostic.Edition);
            Assert.True(main.OpenRule(diagnostic));
            Assert.Equal(MainWindowViewModel.RulesTab, main.SelectedMainTab);
            Assert.Equal(diagnostic.RuleId, main.Rules.Detail?.Id);
            Assert.Equal(panel.Edition, main.Rules.SelectedEdition);
        }
    }

    [Fact]
    public void CoreCodeDiagnostic_HasNoLink()
    {
        var main = new MainWindowViewModel { Input = "46,XX,@@@" };
        var core = main.SelectedPanel!.Diagnostics.Where(d => d.RuleId.StartsWith("CORE.", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(core);
        Assert.All(core, d =>
        {
            Assert.False(d.HasKbRule);
            Assert.True(d.IsCoreCode);
            Assert.False(main.OpenRule(d));
        });
        Assert.Equal(MainWindowViewModel.SingleTab, main.SelectedMainTab);
    }

    [Fact]
    public void ExampleCheck_OpensItInTheSingleStringCheck()
    {
        var main = new MainWindowViewModel();
        Assert.True(main.Rules.OpenRule("2016", "TRANSLOC.FORMAT"));
        main.SelectedMainTab = MainWindowViewModel.RulesTab;
        var example = main.Rules.Detail!.ValidExamples[0];
        example.CheckCommand.Execute(null);
        Assert.Equal(MainWindowViewModel.SingleTab, main.SelectedMainTab);
        Assert.Equal(example.Text, main.Input);
        Assert.Equal("2016", main.SelectedEditionChoice?.Edition);
    }

    [Fact]
    public void WithoutQuotes_TheBrowserWorks_AndHidesTheQuoteSection()
    {
        using var db = NoQuotesCopy.Create();
        var main = new MainWindowViewModel(null, db.Path);
        using var store = main.Rules; // releases the file before the copy is deleted
        using var bandStore = main.Bands; // the Bands tab (T-084) has its own read-only connection
        var rules = main.Rules;
        Assert.True(rules.IsAvailable);
        rules.Query = "decreasing length";
        Assert.True(rules.OpenRule("2016", "CHROM.IDENTIFIERS"));
        Assert.False(rules.Detail!.HasQuotes);
        Assert.Empty(rules.Detail.Quotes);
        rules.Query = "TRANSLOC";
        Assert.Contains(rules.Hits, h => h.Id == "TRANSLOC.FORMAT");
    }

    [Fact]
    public void MissingDatabase_DisablesOnlyTheRulesTab()
    {
        var missing = Path.Combine(Path.GetTempPath(), "cytolex-no-such-" + Guid.NewGuid().ToString("N") + ".sqlite");
        var main = new MainWindowViewModel(null, missing) { Input = "46XX" };
        Assert.True(main.Rules.HasError);
        Assert.False(main.Rules.IsAvailable);
        Assert.Contains("kb build", main.Rules.ErrorText, StringComparison.Ordinal);
        Assert.Empty(main.Rules.Hits);
        Assert.True(main.HasResult); // the single-string check still works
        Assert.False(main.Rules.OpenRule("2016", "TRANSLOC.FORMAT"));
    }

    [Fact]
    public void HitRow_MarksQuoteOnlyMatches()
    {
        var hit = new RuleHitViewModel(new RuleHit("2016", "X.Y", "T", "9.2", "warning", "…text…", RuleMatch.Quote));
        Assert.Equal("§9.2 · warning · matched in a quote", hit.Meta);
        Assert.True(hit.HasSnippet);
    }
}
