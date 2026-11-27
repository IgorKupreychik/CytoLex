using CytoLex.Core.Context;
using CytoLex.Core.Validation;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-110: the Associations view tab (disclaimer, cards, unreviewed badges, span links) and the batch column.</summary>
public sealed class AssociationsViewTests : IDisposable
{
    private const string _disclaimer =
        "Conditions associated with these abnormalities in the cited literature. Not a diagnosis or an interpretation of this result.";

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cytolex-assoc-vm-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static MainWindowViewModel Vm(string input, bool autoCorrect = false) => new() { AutoCorrect = autoCorrect, Input = input };

    private static AssociationsViewModel View(MainWindowViewModel vm) => vm.SelectedPanel!.Associations!;

    [Fact]
    public void Tab_AfterMap_BeforeCytoGps()
    {
        Assert.Equal(
            [ResultView.Diagnostics, ResultView.Abnormalities, ResultView.Translation, ResultView.Map, ResultView.Associations, ResultView.CytoGps],
            Enum.GetValues<ResultView>());
        var vm = Vm("47,XY,+21");
        vm.SelectedViewIndex = (int)ResultView.Associations;
        Assert.Equal(ResultView.Associations, vm.SelectedView);
        Assert.Equal("Associations (1)", vm.SelectedPanel!.AssociationsHeader);
    }

    [Theory]
    [InlineData("46,XY,t(9;22)(q34;q11.2)[20]")] // matches
    [InlineData("46,XY")] // no match
    [InlineData("46XY,+21")] // not valid
    public void Disclaimer_AlwaysThere(string input)
    {
        Assert.Equal(_disclaimer, AssociationsViewModel.DisclaimerText);
        Assert.Equal(_disclaimer, BatchViewModel.AssociationsColumnTip);
        Assert.NotNull(Vm(input).SelectedPanel!.Associations); // the tab shows the disclaimer whenever the edition is implemented
    }

    [Fact]
    public void NoMatch_EmptyText_PlainHeader()
    {
        var vm = Vm("46,XY");
        var view = View(vm);
        Assert.False(view.HasItems);
        Assert.Equal("No association rule matches this string.", AssociationsViewModel.EmptyText);
        Assert.Equal("Associations", vm.SelectedPanel!.AssociationsHeader);
        Assert.False(view.HasNote);
    }

    [Fact]
    public void NotValid_NoteSaysReadAsWritten()
    {
        var view = View(Vm("46XY,+21"));
        Assert.True(view.HasNote);
        Assert.Contains("not valid in ISCN 2016", view.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void T9_22_TwoCards_WithSourceAndUnreviewedBadge()
    {
        var vm = Vm("46,XY,t(9;22)(q34;q11.2)[20]");
        var view = View(vm);
        Assert.Equal(["ASSOC.T9_22_BALL", "ASSOC.T9_22_CML"], view.Items.Select(i => i.RuleId).Order(StringComparer.Ordinal));
        Assert.Equal("Associations (2)", vm.SelectedPanel!.AssociationsHeader);

        var cml = view.Items.Single(i => i.RuleId == "ASSOC.T9_22_CML");
        Assert.StartsWith("Chronic myeloid leukaemia", cml.Condition, StringComparison.Ordinal);
        Assert.Equal("characteristic", cml.StrengthLabel);
        Assert.Equal("neoplastic", cml.ContextLabel);
        Assert.StartsWith("WHO-HAEM5: ", cml.SourceText, StringComparison.Ordinal);
        Assert.True(cml.IsUnreviewed); // the T-108 seed is unreviewed
        Assert.Contains("WHO-HAEM5", cml.UnreviewedTip, StringComparison.Ordinal);
        Assert.Equal("recurrent", view.Items.Single(i => i.RuleId == "ASSOC.T9_22_BALL").StrengthLabel);

        // The rule is "any of (exact t(9;22); fusion 9q34+22q11.2)": one row per satisfied leaf.
        Assert.Equal(["exact t(9;22)(q34;q11.2)", "fusion 9q34+22q11.2"], cml.Evidence.Select(e => e.LeafText));
        Assert.All(cml.Evidence, e => Assert.Equal("t(9;22)(q34;q11.2)", e.Text));
        Assert.All(cml.Evidence, e => Assert.Equal(e.Text, vm.Input.Substring(e.Span.Start, e.Span.Length)));
    }

    [Fact]
    public void Trisomy21_ContextNote()
    {
        var vm = Vm("47,XY,+21");
        var card = Assert.Single(View(vm).Items);
        Assert.Equal("Down syndrome", card.Condition);
        Assert.Equal("constitutional", card.ContextLabel);
        if (vm.Report!.Context.Effective is null)
        {
            Assert.True(card.HasContextNote);
            Assert.Equal("Context of the string unknown: matches if the sample is constitutional.", card.ContextNote);
        }
        else
        {
            Assert.False(card.HasContextNote);
        }
    }

    [Fact]
    public void KnownContext_NoContextNote()
    {
        // A stemline reference (idem) marks the string as acquired (neoplastic).
        var vm = Vm("46,XX,t(15;17)(q24;q21)[10]/47,idem,+8[10]");
        Assert.Equal(SampleContext.Acquired, vm.Report!.Context.Effective);
        var card = Assert.Single(View(vm).Items, i => i.RuleId == "ASSOC.T15_17_APL");
        Assert.False(card.HasContextNote);
    }

    [Fact]
    public void CopyEvidence_ShowsOverlapAndReading()
    {
        var card = Assert.Single(View(Vm("arr[GRCh38] 22q11.21(18912231_21465672)×1")).Items, i => i.RuleId == "ASSOC.DEL22Q11_2");
        var evidence = Assert.Single(card.Evidence);
        Assert.True(evidence.HasDetail);
        Assert.Contains("covers", evidence.Detail, StringComparison.Ordinal);
        Assert.Contains("loss", evidence.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ConditionIds_Labelled()
    {
        foreach (var card in View(Vm("47,XY,+21")).Items.Where(c => c.HasConditionIds))
        {
            Assert.DoesNotContain("icd10", card.ConditionIds, StringComparison.Ordinal);
            Assert.DoesNotContain("orphanet", card.ConditionIds, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Evidence_ClickSelects_HoverMarks()
    {
        var vm = Vm("46,XY,t(9;22)(q34;q11.2)[20]");
        var view = View(vm);
        var cml = view.Items.Single(i => i.RuleId == "ASSOC.T9_22_CML").Evidence[0];
        var ball = view.Items.Single(i => i.RuleId == "ASSOC.T9_22_BALL").Evidence[0];

        vm.SelectSpan(cml.Span); // what a click on the row does (OnSpanItemTapped)
        Assert.Equal(cml.Span, vm.SelectedSpan);
        Assert.True(cml.IsSelected);
        Assert.True(ball.IsSelected); // the same abnormality
        Assert.Contains(vm.SelectedPanel!.CellLineGroups.SelectMany(g => g.Items), c => c.IsMarked);

        vm.HoverSpan = cml.Span; // what hovering the row does (OnCardPointerEntered)
        Assert.True(cml.IsHighlighted);
        vm.HoverSpan = null;
        Assert.False(cml.IsHighlighted);
        Assert.True(cml.IsSelected); // leaving the hover keeps the selection

        vm.HoverOffset = vm.Input.IndexOf("t(9", StringComparison.Ordinal) + 2; // the editor under the mouse
        Assert.True(cml.IsHighlighted);
    }

    [Fact]
    public void Autocorrected_SpanOnTheTypedText()
    {
        const string typed = "46,XY,t(9;22)(q34; q11.2)[20]";
        var vm = Vm(typed, autoCorrect: true);
        Assert.True(vm.SelectedPanel!.IsAutoCorrected);
        var evidence = View(vm).Items.Single(i => i.RuleId == "ASSOC.T9_22_CML").Evidence[0];
        Assert.Equal(evidence.Text, typed.Substring(evidence.Span.Start, evidence.Span.Length).Replace(" ", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void NotImplementedEdition_NoView()
    {
        var vm = new MainWindowViewModel { AutoCorrect = false };
        vm.SelectedEditionChoice = EditionChoice.All;
        vm.Input = "47,XY,+21";
        var panel = vm.Panels.Single(p => p.Edition == "2020");
        Assert.Null(panel.Associations);
        Assert.Equal("Associations", panel.AssociationsHeader);
    }

    [Fact]
    public async Task Batch_ConditionColumn_AndFilter()
    {
        var batch = new MainWindowViewModel().Batch;
        var path = Path.Combine(_dir.FullName, "in.csv");
        await File.WriteAllTextAsync(path, "id,iscn\ncml,\"46,XY,t(9;22)(q34;q11.2)[20]\"\nnormal,\"46,XX\"\n", TestContext.Current.CancellationToken);
        await batch.LoadAsync(path);
        await batch.RunCommand.ExecuteAsync(null);

        Assert.True(batch.HasAssociationRows);
        var cml = batch.Rows.Single(r => r.Id == "cml");
        Assert.Contains("Chronic myeloid leukaemia", cml.Associations, StringComparison.Ordinal);
        Assert.Contains("B-lymphoblastic", cml.Associations, StringComparison.Ordinal);
        Assert.Contains("unreviewed", cml.AssociationsTip, StringComparison.Ordinal);
        Assert.EndsWith(_disclaimer, cml.AssociationsTip, StringComparison.Ordinal);
        var normal = batch.Rows.Single(r => r.Id == "normal");
        Assert.False(normal.HasAssociations);
        Assert.Null(normal.AssociationsTip);

        batch.SelectedFilter = batch.Filters.Single(f => f.Filter == BatchFilter.WithAssociations);
        Assert.Equal(["cml"], batch.Rows.Select(r => r.Id));
    }
}
