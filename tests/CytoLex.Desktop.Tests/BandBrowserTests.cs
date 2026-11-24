using CytoLex.Core.Kb;
using CytoLex.Desktop.Rules;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-084: the Bands tab (list indented by nesting, filters, detail, parent/sub-band links, <c>OpenBand</c>).</summary>
public sealed class BandBrowserTests
{
    [Fact]
    public void StartsWithEveryBandOfTheDefaultEdition_InBandOrder()
    {
        var bands = new MainWindowViewModel().Bands;
        Assert.True(bands.IsAvailable);
        Assert.Equal("2016", bands.SelectedEdition);
        var set = KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Bands!;
        Assert.Equal(set.Bands.Select(b => b.Name), bands.Items.Select(i => i.Name));
        Assert.Equal($"{set.Bands.Length} band(s)", bands.ResultText);
        Assert.Null(bands.SelectedItem);
        Assert.Null(bands.Detail);
        Assert.Equal(["Chromosome 1", "Chromosome X", "Chromosome Y"], bands.Chromosomes.Where(c => c.Chromosome is "1" or "X" or "Y").Select(c => c.Label));
        Assert.Equal([300, 400, 550, 700, 850], bands.Levels.Skip(1).Select(l => l.Level!.Value));
    }

    [Fact]
    public void Rows_AreIndentedByNesting()
    {
        var items = new MainWindowViewModel().Bands.Items;
        Assert.Equal(0, items.Single(i => i.Name == "1p36").Depth);
        Assert.Equal(1, items.Single(i => i.Name == "1p36.3").Depth);
        Assert.Equal(2, items.Single(i => i.Name == "1p36.33").Depth);
        Assert.Equal(32, items.Single(i => i.Name == "1p36.33").Indent.Left);
        Assert.Equal("group of sub-bands", items.Single(i => i.Name == "1p36").Summary);
        Assert.Equal("levels 300, 400, 550, 700", items.Single(i => i.Name == "1p36.3").Summary);
        Assert.True(items.Single(i => i.Name == "9q34.11").IsNotInBook);
        Assert.False(items.Single(i => i.Name == "9q34.1").IsNotInBook);
    }

    [Fact]
    public void Filters_ChromosomeLevelNotInBookAndName()
    {
        var bands = new MainWindowViewModel().Bands;
        var all = bands.Items.Length;

        bands.SelectedChromosome = bands.Chromosomes.Single(c => c.Chromosome == "X");
        Assert.All(bands.Items, i => Assert.Equal("X", i.Band.Chromosome));
        Assert.StartsWith($"{bands.Items.Length} of {all} ", bands.ResultText, StringComparison.Ordinal);

        bands.SelectedLevel = bands.Levels.Single(l => l.Level == 300);
        Assert.NotEmpty(bands.Items);
        Assert.All(bands.Items, i => Assert.Contains(300, i.Band.Levels));

        bands.SelectedChromosome = ChromosomeChoice.All;
        var set = KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Bands!;
        Assert.Equal(set.Bands.Count(b => b.Levels.Contains(300)), bands.Items.Length); // the 300-band set

        bands.SelectedLevel = LevelChoice.All;
        bands.NotInBookOnly = true;
        Assert.Equal(set.Bands.Where(b => !b.Book).Select(b => b.Name), bands.Items.Select(i => i.Name));
        Assert.Contains(bands.Items, i => i.Name == "9q34.11");

        bands.NotInBookOnly = false;
        bands.Query = "5Q1";
        Assert.NotEmpty(bands.Items);
        Assert.All(bands.Items, i => Assert.StartsWith("5q1", i.Name, StringComparison.Ordinal));
        Assert.Equal("5q11", bands.SelectedItem?.Name); // the first hit is selected

        bands.Query = "5q13";
        Assert.Equal("5q13", bands.SelectedItem?.Name); // the exact name wins
        Assert.Contains(bands.Items, i => i.Name == "5q13.2");

        bands.Query = "zz";
        Assert.Empty(bands.Items);
        Assert.Null(bands.Detail);
    }

    [Fact]
    public void Detail_ShowsLevelsCoordinatesFamilyAndDecisions()
    {
        var bands = new MainWindowViewModel().Bands;
        Assert.True(bands.OpenBand("2016", "5q13.2"));
        var d = bands.Detail!;
        Assert.Equal("band:5q13.2", d.Id);
        Assert.Equal("ISCN 2016 · chromosome 5, long arm (q)", d.Header);
        Assert.StartsWith("Band levels: ", d.LevelsText, StringComparison.Ordinal);
        using (var store = RuleStore.Open(RuleStore.DefaultPath))
        {
            var coords = store.Band("2016", "5q13.2")!.Coordinates;
            Assert.Equal([CoordinateText.Band(coords, CoordinateBuild.Grch38), CoordinateText.Band(coords, CoordinateBuild.Grch37)], d.Coordinates);
        }

        Assert.Equal("5q13", d.Parent?.Name);
        Assert.Equal(["5q13.21", "5q13.22", "5q13.23"], d.Children.Select(c => c.Name)); // book sub-bands (T-007)
        var decision = Assert.Single(d.Exceptions);
        Assert.Equal("Level 850 · region 5q13.2 · the book wins", decision.Title);
        Assert.StartsWith("ISCN 2016 §2.5, p.", decision.Citation, StringComparison.Ordinal);

        Assert.True(bands.OpenBand("2016", "5q13.21"));
        Assert.Contains("Book-only band", bands.Detail!.BookText, StringComparison.Ordinal);
        Assert.True(bands.OpenBand("2016", "9q34.11"));
        Assert.Contains("Not in the book", bands.Detail!.BookText, StringComparison.Ordinal);
    }

    [Fact]
    public void ParentAndSubBandLinks_Navigate()
    {
        var bands = new MainWindowViewModel().Bands;
        Assert.True(bands.OpenBand("2016", "1p36.3"));
        bands.Detail!.Parent!.OpenCommand.Execute(null);
        Assert.Equal("1p36", bands.SelectedItem?.Name);
        Assert.Contains("none of its own", bands.Detail!.LevelsText, StringComparison.Ordinal);
        bands.Detail.Children.Single(c => c.Name == "1p36.2").OpenCommand.Execute(null);
        Assert.Equal("1p36.2", bands.Detail?.Name);
    }

    [Fact]
    public void OpenBand_ClearsFiltersThatHideIt_AndRejectsUnknownBands()
    {
        var bands = new MainWindowViewModel().Bands;
        bands.SelectedChromosome = bands.Chromosomes.Single(c => c.Chromosome == "X");
        bands.SelectedLevel = bands.Levels.Single(l => l.Level == 850);
        bands.Query = "Xq";
        Assert.True(bands.OpenBand("2016", "5q13"));
        Assert.Same(ChromosomeChoice.All, bands.SelectedChromosome);
        Assert.Same(LevelChoice.All, bands.SelectedLevel);
        Assert.Equal(string.Empty, bands.Query);
        Assert.Equal("5q13", bands.SelectedItem?.Name);

        Assert.False(bands.OpenBand("2016", "5q99"));
        Assert.Contains("not a band", bands.ResultText, StringComparison.Ordinal);
        Assert.False(bands.OpenBand("1999", "5q13"));
    }

    [Fact]
    public void MissingDatabase_DisablesOnlyTheBandsTab()
    {
        var missing = Path.Combine(Path.GetTempPath(), "cytolex-no-such-" + Guid.NewGuid().ToString("N") + ".sqlite");
        var main = new MainWindowViewModel(null, missing) { Input = "46XX" };
        Assert.True(main.Bands.HasError);
        Assert.Contains("kb build", main.Bands.ErrorText, StringComparison.Ordinal);
        Assert.Empty(main.Bands.Items);
        Assert.True(main.HasResult);
        Assert.False(main.Bands.OpenBand("2016", "5q13"));
    }

    [Fact]
    public void DatabaseWithoutBandTables_SaysHowToRebuild()
    {
        using var db = NoQuotesCopy.Create("DROP TABLE bands; DROP TABLE band_exceptions;");
        using var bands = new BandBrowserViewModel(db.Path, "2016"); // releases the file before the copy is deleted
        Assert.True(bands.IsAvailable);
        Assert.Empty(bands.Items);
        Assert.Contains("kb build", bands.ResultText, StringComparison.Ordinal);
    }
}
