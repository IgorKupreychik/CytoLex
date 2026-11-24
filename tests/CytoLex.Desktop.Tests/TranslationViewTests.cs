using System.Text.Json;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Desktop.Rules;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-059: the translation view (summary, parts, banner, draft note, readings, hover ↔ span, KB links, coordinates).</summary>
public sealed class TranslationViewTests
{
    [Fact]
    public void BookExamples_ShowTheirTranslation()
    {
        // Every golden entry of a book example without a context hint (the desktop has no hint) and not a fragment.
        var vm = new MainWindowViewModel();
        var checkedCount = 0;
        foreach (var entry in GoldenBookEntries())
        {
            vm.Input = entry.Input;
            var translation = vm.SelectedPanel?.Translation;
            Assert.True(translation is not null, $"no translation for {entry.Input}");
            Assert.Equal(entry.Summary, translation.Summary.Sentence);
            Assert.Equal(entry.Items.Select(i => (i.Start, i.Text, i.Sentence)), translation.Items.Select(i => (i.Span.Start, i.Text, i.Sentence)));
            Assert.All(translation.Items, i => Assert.Equal(i.Text, i.Span.Of(entry.Input)));
            Assert.False(translation.HasBanner, entry.Input);
            checkedCount++;
        }

        Assert.True(checkedCount > 200, $"only {checkedCount} book examples checked");
    }

    [Fact]
    public void DiagnosticsView_IsTheDefault_TranslationKeptAcrossTyping()
    {
        var vm = new MainWindowViewModel { Input = "46,XY,del(5)(q13q33)" };
        Assert.Equal(ResultView.Diagnostics, vm.SelectedView);
        vm.SelectedViewIndex = (int)ResultView.Translation; // the user clicks "Translation"
        vm.Input = "46,XY,del(5)(q13q34)";
        Assert.Equal(ResultView.Translation, vm.SelectedView);
    }

    [Fact]
    public void ValidString_SummaryPartsLinksAndDraftNote()
    {
        var vm = new MainWindowViewModel { Input = "46,XY,del(5)(q13q33)" };
        var t = vm.SelectedPanel!.Translation!;
        Assert.False(t.HasBanner);
        Assert.True(t.HasDraftNote); // every template is a draft until T-082
        Assert.Equal(TranslationViewModel.DraftNoteText, t.DraftNote);
        Assert.True(t.Summary.IsSummary);
        Assert.False(t.Summary.HasText);
        var item = Assert.Single(t.Items);
        Assert.Equal("del(5)(q13q33)", item.Text);
        Assert.Equal(new TextSpan(6, 14), item.Span);
        Assert.True(item.IsTranslated);
        Assert.Equal(1.0, item.Opacity);
        Assert.False(item.HasDiagnostics);
        Assert.False(item.HasCoordinates); // off by default
        var del = item.Links.First(l => l.Ref == KbRef.Vocab("symbols", "del"));
        Assert.True(del.IsNavigable);
        Assert.StartsWith("vocab:symbols/del", del.Tip, StringComparison.Ordinal);
        var band = item.Links.First(l => l.Ref.Kind == KbRefKind.Band);
        Assert.False(band.IsNavigable);
        Assert.True(band.IsBand);
    }

    [Fact]
    public void InvalidString_BannerAndUntranslatedPartWithDiagnosticLinks()
    {
        // A syntax-damaged part (missing comma) is not translated; its diagnostic links select the diagnostic.
        var vm = new MainWindowViewModel { Input = "46,XX,del(5)(q13q33)t(1;3)(p32;q21" };
        var panel = vm.SelectedPanel!;
        var t = panel.Translation!;
        Assert.True(t.HasBanner);
        Assert.Equal("Not valid ISCN 2016; partial translation.", t.Banner);
        var untranslated = t.Items.First(i => !i.IsTranslated);
        Assert.True(untranslated.Opacity < 1.0);
        Assert.True(untranslated.HasDiagnostics);
        var link = untranslated.Diagnostics[0];
        link.SelectCommand.Execute(null);
        Assert.Same(panel.Diagnostics[link.Index], panel.SelectedDiagnostic);
        Assert.Equal(panel.Diagnostics[link.Index].Span, vm.SelectedSpan);

        // Clicking it again still selects the span again.
        vm.SelectSpan(new TextSpan(0, 1));
        link.SelectCommand.Execute(null);
        Assert.Equal(panel.Diagnostics[link.Index].Span, vm.SelectedSpan);
    }

    [Fact]
    public void UnknownContext_ShowsBothReadings()
    {
        var vm = new MainWindowViewModel { Input = "49,XX,+1,+3,r(7),+8" };
        var t = vm.SelectedPanel!.Translation!;
        Assert.True(t.HasReadingBasisText);
        var plus1 = t.Items[0];
        Assert.True(plus1.HasReadings);
        Assert.Equal("If constitutional: An extra chromosome 1 (constitutional).", plus1.ConstitutionalReading);
        Assert.Equal("If acquired: Gain of chromosome 1 (acquired).", plus1.AcquiredReading);
    }

    [Fact]
    public void EditorHover_MarksThePartAtTheOffset_AndItsSpan()
    {
        var vm = new MainWindowViewModel { Input = "47,XY,+8,t(9;22)(q34;q11.2)" };
        var t = vm.SelectedPanel!.Translation!;
        var plus8 = t.Items.Single(i => i.Text == "+8");
        var trans = t.Items.Single(i => i.Text == "t(9;22)(q34;q11.2)");

        vm.HoverOffset = trans.Span.Start + 3;
        Assert.True(trans.IsHighlighted);
        Assert.False(plus8.IsHighlighted);
        Assert.Equal(trans.Span, vm.HoverSpan);

        vm.HoverOffset = plus8.Span.Start;
        Assert.True(plus8.IsHighlighted);
        Assert.False(trans.IsHighlighted);
        Assert.Equal(plus8.Span, vm.HoverSpan);

        vm.HoverOffset = 1; // in "47": no part there
        Assert.All(t.Items, i => Assert.False(i.IsHighlighted));
        Assert.Null(vm.HoverSpan);

        vm.HoverOffset = trans.Span.Start;
        vm.HoverOffset = null;
        Assert.All(t.Items, i => Assert.False(i.IsHighlighted));
        Assert.Null(vm.HoverSpan);
    }

    [Fact]
    public void PartHover_MarksItsSpanInTheEditor()
    {
        var vm = new MainWindowViewModel { Input = "47,XY,+8,t(9;22)(q34;q11.2)" };
        var t = vm.SelectedPanel!.Translation!;
        var trans = t.Items.Single(i => i.Text == "t(9;22)(q34;q11.2)");
        vm.HoverTranslationItem(trans, hovered: true);
        Assert.True(trans.IsHighlighted);
        Assert.Equal(trans.Span, vm.HoverSpan);
        vm.HoverTranslationItem(trans, hovered: false);
        Assert.False(trans.IsHighlighted);
        Assert.Null(vm.HoverSpan);

        vm.HoverTranslationItem(t.Summary, hovered: true); // the summary has no span
        Assert.Null(vm.HoverSpan);
    }

    [Fact]
    public void MultipleCellLines_HoverPicksTheNarrowestPart()
    {
        var vm = new MainWindowViewModel { Input = "mos 47,XX,+21[12]/46,XX[8]" };
        var t = vm.SelectedPanel!.Translation!;
        var plus21 = t.Items.Single(i => i.Text == "+21");
        vm.HoverOffset = plus21.Span.Start + 1;
        Assert.True(plus21.IsHighlighted);
        Assert.Single(t.Items, i => i.IsHighlighted);
    }

    [Fact]
    public void RuleLink_OpensTheRuleInTheBrowser()
    {
        var vm = new MainWindowViewModel { Input = "46,XY,del(5)(q13q33)" };
        var link = vm.SelectedPanel!.Translation!.Items[0].Links.First(l => l.Ref.Kind == KbRefKind.Rule);
        link.OpenCommand.Execute(null);
        Assert.Equal(MainWindowViewModel.RulesTab, vm.SelectedMainTab);
        Assert.Equal(link.Ref.Id, vm.Rules.Detail?.Id);
        Assert.Null(vm.Rules.VocabDetail);
    }

    [Fact]
    public void VocabLink_OpensTheVocabItemInTheBrowser()
    {
        var vm = new MainWindowViewModel { Input = "46,XY,del(5)(q13q33)" };
        vm.Rules.Query = "zzqqxx-no-such-word"; // a filter that hides the item is cleared
        var link = vm.SelectedPanel!.Translation!.Items[0].Links.First(l => l.Ref == KbRef.Vocab("symbols", "del"));
        link.OpenCommand.Execute(null);
        Assert.Equal(MainWindowViewModel.RulesTab, vm.SelectedMainTab);
        Assert.Null(vm.Rules.Detail);
        var detail = vm.Rules.VocabDetail;
        Assert.NotNull(detail);
        Assert.Equal("vocab:symbols/del", detail.Id);
        Assert.Equal("del", detail.Label);
        Assert.Equal(link.Title, detail.Title);
    }

    [Fact]
    public void BandLink_IsNotNavigable()
    {
        var vm = new MainWindowViewModel { Input = "46,XY,del(5)(q13q33)" };
        var band = vm.SelectedPanel!.Translation!.Items[0].Links.First(l => l.Ref.Kind == KbRefKind.Band);
        band.OpenCommand.Execute(null);
        Assert.Equal(MainWindowViewModel.SingleTab, vm.SelectedMainTab);
        Assert.False(vm.OpenKbEntry("2016", band.Ref));
        Assert.Contains("\"Genomic coordinates\"", band.Tip, StringComparison.Ordinal); // a hint until coordinates are on
    }

    [Fact]
    public void Coordinates_OffByDefault_ToggleAndBuild()
    {
        var vm = new MainWindowViewModel { Input = "46,XY,del(5)(q13q33)" };
        Assert.False(vm.ShowCoordinates);
        Assert.Null(vm.SelectedPanel!.Result.Translation!.Items[0].Coordinates);

        vm.ShowCoordinates = true; // re-validates with coordinates
        var item = vm.SelectedPanel!.Translation!.Items[0];
        Assert.NotNull(vm.SelectedPanel.Result.Translation!.Items[0].Coordinates);
        Assert.True(item.HasCoordinates);
        Assert.Contains(item.CoordinateLines, l => l.StartsWith("5q13: chr5:", StringComparison.Ordinal) && l.EndsWith("(GRCh38)", StringComparison.Ordinal));
        Assert.Contains(item.CoordinateLines, l => l.StartsWith("5q13→5q33: ≈ ", StringComparison.Ordinal) && l.Contains(" Mb", StringComparison.Ordinal));
        var band = item.Links.First(l => l.Ref == KbRef.Band("5q13"));
        Assert.Contains("chr5:", band.Tip, StringComparison.Ordinal);

        var report = vm.Report;
        var grch38 = item.CoordinateLines;
        vm.SelectedCoordinateBuild = MainWindowViewModel.CoordinateBuilds.Single(b => b.Build == CoordinateBuild.Grch37);
        Assert.Same(report, vm.Report); // no re-validation
        Assert.NotEqual(grch38, item.CoordinateLines);
        Assert.All(item.CoordinateLines, l => Assert.Contains("GRCh37", l, StringComparison.Ordinal));
        Assert.Contains("GRCh37", band.Tip, StringComparison.Ordinal);

        vm.ShowCoordinates = false;
        Assert.False(vm.SelectedPanel!.Translation!.Items[0].HasCoordinates);
    }

    [Fact]
    public void CoordinateText_Formats()
    {
        Assert.Equal("78.5 Mb", CoordinateText.Size(78_500_000));
        Assert.Equal("850 kb", CoordinateText.Size(850_000));
        Assert.Equal("120 bp", CoordinateText.Size(120));
        var bookOnly = new CytoLex.Core.Translation.BandCoordinates("1p36.3", null, null, IsBookOnly: true);
        Assert.Equal("1p36.3: no coordinates (book-only band)", CoordinateText.Band(bookOnly, CoordinateBuild.Grch38));
        var x = new CytoLex.Core.Translation.BandCoordinates("Xp22.3", new(1, 9_000_000), new(1, 9_100_000), IsBookOnly: false);
        Assert.Equal("Xp22.3: chrX:1–9,100,000 (GRCh38)", CoordinateText.Band(x, CoordinateBuild.Grch38));
        var segment = new CytoLex.Core.Translation.SegmentSize("5q13", "5q33", null, 1_000);
        Assert.Equal("5q13→5q33: size unknown (a band has no coordinates)", CoordinateText.Segment(segment, CoordinateBuild.Grch37));
    }

    [Fact]
    public void NotImplementedEdition_HasNoTranslation()
    {
        var vm = new MainWindowViewModel { Input = "46,XX" };
        vm.SelectedEditionChoice = vm.EditionChoices.Single(c => c.Edition == "2020");
        Assert.False(vm.SelectedPanel!.HasTranslation);
        vm.HoverOffset = 1; // no translation: nothing happens
        Assert.Null(vm.HoverSpan);
    }

    [Fact]
    public void RuleBrowser_EveryVocabItemOpens_WithTheRuntimeMeanings()
    {
        var kb = CytoLex.Core.Kb.KnowledgeBase.LoadEmbedded().ForEdition("2016")!;
        using var store = RuleStore.Open(RuleStore.DefaultPath);
        foreach (var entry in kb.Vocab.Entries)
        {
            var detail = store.Vocab("2016", entry.Vocabulary, entry.Label);
            Assert.True(detail is not null, entry.KbRef);
            Assert.Equal(entry.KbRef, "vocab:" + detail.Id);
            Assert.Equal(entry.Title, detail.Title);
            Assert.Equal(entry.Meanings, detail.Meanings.Select(m => m.Text));
        }
    }

    [Fact]
    public void RuleBrowser_SymbolSearch_PutsTheVocabItemFirst()
    {
        using var store = RuleStore.Open(RuleStore.DefaultPath);
        var del = store.Search("2016", "del");
        Assert.Equal((KbEntryKind.Vocab, "symbols/del"), (del[0].Kind, del[0].Id));
        Assert.Contains(del, h => h.Kind == KbEntryKind.Rule); // rules still follow

        var arrow = store.Search("2016", "→"); // no FTS words: exact symbol first, then the whole list
        Assert.Equal(KbEntryKind.Vocab, arrow[0].Kind);
        Assert.Contains("→", store.Vocab("2016", "symbols", arrow[0].Id["symbols/".Length..])!.Tokens);

        var meaning = store.Search("2016", "deletion");
        Assert.Contains(meaning, h => h.Kind == KbEntryKind.Vocab && h.Id == "symbols/del");

        // The section filter applies to vocab items too: §2.4 lists its rules, then the banding codes of Table 2.
        var kb = CytoLex.Core.Kb.KnowledgeBase.LoadEmbedded().ForEdition("2016")!;
        var section = store.Search("2016", null, "2.4");
        Assert.Equal(
            kb.Vocab.Entries.Where(e => e.Vocabulary == "banding_codes").Select(e => e.Label).Order(StringComparer.Ordinal),
            section.Where(h => h.Kind == KbEntryKind.Vocab).Select(h => h.Id["banding_codes/".Length..]).Order(StringComparer.Ordinal));
        Assert.All(section, h => Assert.Equal("2.4", h.Section));
        Assert.Contains("2.4", store.Sections("2016"));
    }

    private sealed record GoldenItem(int Start, string Text, string Sentence);

    private sealed record GoldenEntry(string Input, string Summary, List<GoldenItem> Items);

    private static List<GoldenEntry> GoldenBookEntries()
    {
        using var stream = typeof(TranslationViewTests).Assembly.GetManifestResourceStream("CytoLex.Tests.kb.json")!;
        using var doc = JsonDocument.Parse(stream);
        var golden = doc.RootElement.GetProperty("editions").GetProperty("2016").GetProperty("translation").GetProperty("golden");
        var entries = new List<GoldenEntry>();
        foreach (var g in golden.EnumerateArray())
        {
            var isBook = g.TryGetProperty("cite", out _);
            var isFragment = g.TryGetProperty("fragment", out var f) && f.GetBoolean();
            if (!isBook || isFragment || g.GetProperty("context").GetString() != "unknown")
            {
                continue;
            }

            var items = g.GetProperty("items").EnumerateArray()
                .Select(i => new GoldenItem(
                    i.GetProperty("span").GetProperty("start").GetInt32(),
                    i.GetProperty("span").GetProperty("text").GetString()!,
                    i.GetProperty("sentence").GetString()!))
                .ToList();
            entries.Add(new GoldenEntry(g.GetProperty("input").GetString()!, g.GetProperty("summary").GetString()!, items));
        }

        return entries;
    }
}
