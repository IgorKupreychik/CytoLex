using CytoLex.Core.Kb;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Ideograms;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Tests;

/// <summary>T-101: the Map view (ideograms, markers, locus list, unplaced probes) and its links with the editor and the cards.</summary>
public class MapViewTests
{
    private static readonly BandSet _bands = KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Bands!;

    private static MainWindowViewModel Vm(string input, bool autoCorrect = false) => new() { AutoCorrect = autoCorrect, Input = input };

    private static MapViewModel Map(MainWindowViewModel vm) => vm.SelectedPanel!.Map!;

    [Fact]
    public void MapTab_BeforeCytoGps()
    {
        Assert.Equal([ResultView.Diagnostics, ResultView.Abnormalities, ResultView.Translation, ResultView.Map, ResultView.Associations, ResultView.CytoGps], Enum.GetValues<ResultView>());
        var vm = Vm("47,XY,+8");
        vm.SelectedViewIndex = (int)ResultView.Map;
        Assert.Equal(ResultView.Map, vm.SelectedView);
        Assert.Equal("Map (1)", vm.SelectedPanel!.MapHeader);
    }

    [Fact]
    public void AllChromosomes_850Bands()
    {
        var map = Map(Vm("47,XY,+8"));
        Assert.Equal([.. Enumerable.Range(1, 22).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)), "X", "Y"], map.Chromosomes.Select(c => c.Name));
        var chr1 = map.Chromosomes[0];
        var expected = _bands.OfChromosome("1").Count(b => b.Levels.Contains(850) && b.Grch38 is not null); // chromosome 1 has no book-only 850 parent
        Assert.Equal(expected, chr1.Bands.Length);
        Assert.Equal(1.0, chr1.RelativeLength);
        Assert.Contains(chr1.Bands, b => b.Stain == "acen");
        Assert.InRange(chr1.Centromere!.Value, 0.4, 0.55); // chr1 is metacentric
        Assert.True(map.Chromosomes.Single(c => c.Name == "21").RelativeLength < 0.25);
    }

    [Fact]
    public void Markers_OnTheirChromosome()
    {
        var map = Map(Vm("46,XY,t(9;22)(q34;q11.2),del(5)(q13q33)"));
        Assert.Equal(["9", "22", "5"], map.Loci.Select(l => l.Locus.Chromosome));
        var chr5 = map.Chromosomes.Single(c => c.Name == "5");
        var del = Assert.Single(chr5.Markers);
        Assert.Equal("loss", del.Locus.EffectLabel);
        Assert.Equal("5q13→5q33", del.Locus.Place);
        var whole = _bands.ChromosomeRange("5", ReferenceBuild.Grch38)!;
        Assert.Equal((67_400_000.0 / whole.End), del.Span.From, 6);
        Assert.True(map.Chromosomes.Single(c => c.Name == "9").Markers.Single().Locus.IsPoint);
        Assert.Empty(map.Chromosomes.Single(c => c.Name == "1").Markers);
    }

    [Fact]
    public void OnlyInvolved_Toggle()
    {
        var map = Map(Vm("46,XY,t(9;22)(q34;q11.2)"));
        Assert.Equal(24, map.Chromosomes.Count);
        map.ShowAllChromosomes = false;
        Assert.Equal(["9", "22"], map.Chromosomes.Select(c => c.Name));
    }

    [Fact]
    public void ProbeTableProbes_Placed_UnreviewedBadge()
    {
        var map = Map(Vm("nuc ish(ABL1,BCR)×2(ABL1 con BCR×1)[200]"));
        Assert.Equal(["ABL1", "BCR", "ABL1", "BCR"], map.Loci.Select(u => u.Locus.Probe));
        Assert.Empty(map.Unplaced);
        Assert.All(map.Loci, l => Assert.Equal(l.Locus.ProbeRow!.Reviewed, !l.IsUnreviewed));
        var abl1 = map.Loci[0];
        Assert.Equal("9q34.12", abl1.Place);
        Assert.Contains("probe table", abl1.Details, StringComparison.Ordinal);
        Assert.Contains(abl1.Locus.ProbeRow!.Source, abl1.Tip, StringComparison.Ordinal);
        Assert.Contains(abl1.Locus.ProbeRow!.Source, abl1.ProbeSource, StringComparison.Ordinal);
        Assert.Equal(2, map.Chromosomes.Single(c => c.Name == "9").Markers.Length);
        Assert.Equal(2, map.Chromosomes.Single(c => c.Name == "22").Markers.Length);
        Assert.Equal("Map (4)", map.Header);
    }

    [Fact]
    public void UnplacedProbes_Listed()
    {
        var map = Map(Vm("nuc ish(GLTSCR×1,ZNF443×2)[105/200]"));
        var unplaced = Assert.Single(map.Unplaced);
        Assert.Equal("GLTSCR", unplaced.Locus.Probe);
        Assert.False(unplaced.IsUnreviewed);
        Assert.Null(unplaced.ProbeSource);
        Assert.Contains("probe table", unplaced.Tip, StringComparison.Ordinal);
        Assert.Contains("probe table", map.UnplacedHeader, StringComparison.Ordinal);
        Assert.Equal("ZNF443", Assert.Single(map.Loci).Locus.Probe);
    }

    [Fact]
    public void DNumber_Placed_ArrAsWritten()
    {
        var vm = Vm("47,XX,+21.nuc ish(D21Z1×3)[200].arr[GRCh38] 21q11.2q22.3(13000000_46000000)×3");
        var map = Map(vm);
        var chr21 = map.Chromosomes.Single(c => c.Name == "21");
        Assert.Equal(["gain", "signal", "×3"], chr21.Markers.Select(m => m.Locus.EffectLabel));
        Assert.Equal([0, 1, 2], chr21.Markers.Select(m => m.Span.Lane)); // overlapping: one lane each
        Assert.Contains("not read as a gain or loss", chr21.Markers[2].Locus.Tip, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSwitch_MovesMarkers()
    {
        var vm = Vm("46,XX,del(5)(q13q33)");
        var marker = Map(vm).Chromosomes.Single(c => c.Name == "5").Markers.Single();
        vm.SelectedCoordinateBuild = MainWindowViewModel.CoordinateBuilds.Single(b => b.Build == CoordinateBuild.Grch37);
        var chr5 = Map(vm).Chromosomes.Single(c => c.Name == "5");
        Assert.Equal(CoordinateBuild.Grch37, chr5.Build);
        var whole37 = _bands.ChromosomeRange("5", ReferenceBuild.Grch37)!;
        Assert.Equal(66_700_000.0 / whole37.End, chr5.Markers.Single().Span.From, 6);
        Assert.NotEqual(marker.Span.From, chr5.Markers.Single().Span.From);
    }

    [Fact]
    public void EditorHover_MarksTheLoci()
    {
        const string input = "46,XY,t(9;22)(q34;q11.2),del(5)(q13q33)";
        var vm = Vm(input);
        var map = Map(vm);
        vm.HoverOffset = input.IndexOf("q34", StringComparison.Ordinal) + 1;
        Assert.True(map.Loci[0].IsHighlighted); // the translation part t(9;22)… holds both breakpoints
        Assert.True(map.Loci[1].IsHighlighted);
        Assert.False(map.Loci[2].IsHighlighted);
        vm.HoverOffset = null;
        Assert.All(map.All, l => Assert.False(l.IsHighlighted));
    }

    [Fact]
    public void MarkerHover_MarksItsSpan()
    {
        const string input = "46,XY,t(9;22)(q34;q11.2)";
        var vm = Vm(input);
        var locus = Map(vm).Loci[1];
        vm.HoverLocus(locus);
        Assert.Equal("q11.2", vm.HoverSpan!.Value.Of(input));
        Assert.True(locus.IsHighlighted);
        Assert.False(Map(vm).Loci[0].IsHighlighted);
        vm.HoverLocus(null);
        Assert.Null(vm.HoverSpan);
        Assert.False(locus.IsHighlighted);
    }

    [Fact]
    public void MarkerClick_SelectsSpan_MarksTheCard()
    {
        const string input = "47,XY,+8,t(9;22)(q34;q11.2)";
        var vm = Vm(input);
        var locus = Map(vm).Loci.Single(l => l.Locus.From == "22q11.2");
        vm.SelectLocus(locus);
        Assert.Equal("q11.2", vm.SelectedSpan!.Value.Of(input));
        Assert.True(locus.IsSelected);
        var cards = vm.SelectedPanel!.CellLineGroups.Single().Items;
        Assert.Equal([false, true], cards.Select(c => c.IsMarked));
    }

    [Fact]
    public void CardSelection_MarksItsLoci_ShowOnMap()
    {
        const string input = "47,XY,+8,t(9;22)(q34;q11.2)";
        var vm = Vm(input);
        var cards = vm.SelectedPanel!.CellLineGroups.Single().Items;
        Assert.All(cards, c => Assert.True(c.IsOnMap));
        vm.ShowOnMap(cards[1]);
        Assert.Equal(ResultView.Map, vm.SelectedView);
        Assert.Equal([false, true, true], Map(vm).Loci.Select(l => l.IsSelected));
        Assert.True(cards[1].IsMarked);

        vm.HoverSpan = cards[0].Span; // hovering the +8 card
        Assert.Equal([true, false, false], Map(vm).Loci.Select(l => l.IsHighlighted));
    }

    [Fact]
    public void Selection_ScrollsToItsChromosome()
    {
        const string input = "47,XY,+8,t(9;22)(q34;q11.2)";
        var vm = Vm(input);
        var map = Map(vm);
        Assert.DoesNotContain(map.AllChromosomes, c => c.IsScrollTarget);

        vm.SelectLocus(map.Loci.Single(l => l.Locus.From == "22q11.2"));
        Assert.Equal(["22"], map.AllChromosomes.Where(c => c.IsScrollTarget).Select(c => c.Name));

        // A card: the chromosome of its first locus.
        vm.ShowOnMap(vm.SelectedPanel!.CellLineGroups.Single().Items[1]);
        Assert.Equal(["9"], map.AllChromosomes.Where(c => c.IsScrollTarget).Select(c => c.Name));

        // Selecting on the same chromosome again sets it again (false, then true), so the view scrolls back.
        var changes = new List<bool>();
        var chr9 = map.AllChromosomes.Single(c => c.Name == "9");
        chr9.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IdeogramChromosomeViewModel.IsScrollTarget))
            {
                changes.Add(chr9.IsScrollTarget);
            }
        };
        vm.SelectLocus(map.Loci.Single(l => l.Locus.From == "9q34"));
        Assert.Equal([false, true], changes);

        // A span without loci (the count "47"): no target.
        vm.SelectSpan(new CytoLex.Core.Text.TextSpan(0, 2));
        Assert.DoesNotContain(map.AllChromosomes, c => c.IsScrollTarget);
    }

    [Fact]
    public void CardsWithoutLoci_NoMapButton()
    {
        var vm = Vm("47,XX,+mar");
        Assert.False(vm.SelectedPanel!.CellLineGroups.Single().Items.Single().IsOnMap);
        Assert.False(Map(vm).HasLoci);
    }

    [Fact]
    public void AutoCorrection_SpansOnTheTypedText()
    {
        const string input = "46,XX,del(5)(q13 q33)";
        var vm = Vm(input, autoCorrect: true);
        var locus = Map(vm).Loci.Single();
        Assert.Equal("del(5)(q13 q33)", locus.Span.Of(input));
    }

    [Fact]
    public void NotImplementedEdition_NoMap()
    {
        var vm = Vm("47,XY,+8");
        vm.SelectedEditionChoice = vm.EditionChoices.Single(c => c.Edition == "2020");
        Assert.Null(vm.SelectedPanel!.Map);
        Assert.Equal("Map", vm.SelectedPanel.MapHeader);
    }

    // --- layout ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Lanes_OverlapsSpreadOut()
    {
        Assert.Equal([0, 1, 0, 2], IdeogramLayout.Lanes([(0.1, 0.5), (0.4, 0.6), (0.55, 0.7), (0.45, 0.46)]));
        Assert.Empty(IdeogramLayout.Lanes([]));
    }

    [Fact]
    public void Place_MinimumLength_Clamped()
    {
        var whole = new GenomicRange(1, 1000);
        var (from, to) = IdeogramLayout.Place(new GenomicRange(500, 500), whole)!.Value;
        Assert.Equal(IdeogramLayout.MinMarker, to - from, 9);
        Assert.Equal((0.0, 0.5), IdeogramLayout.Place(new GenomicRange(1, 500), whole));
        Assert.Null(IdeogramLayout.Place(null, whole));
    }

    [Fact]
    public void HitTest_ShortestInTheLane()
    {
        IdeogramSpan[] spans = [new(0.1, 0.9, 0), new(0.4, 0.5, 0), new(0.4, 0.5, 1)];
        Assert.Equal(1, IdeogramLayout.HitTest(spans, 0.45, 0, 0.001));
        Assert.Equal(0, IdeogramLayout.HitTest(spans, 0.2, 0, 0.001));
        Assert.Equal(2, IdeogramLayout.HitTest(spans, 0.45, 1, 0.001));
        Assert.Equal(-1, IdeogramLayout.HitTest(spans, 0.95, 0, 0.001));
    }

    [Fact]
    public void Bands_PterToQter_Contiguous()
    {
        foreach (var chromosome in _bands.Chromosomes)
        {
            var bands = IdeogramLayout.Bands(_bands, chromosome, ReferenceBuild.Grch38);
            Assert.NotEmpty(bands);
            Assert.Equal(0, bands[0].From, 6);
            Assert.Equal(1, bands[^1].To, 6);
            for (var i = 1; i < bands.Length; i++)
            {
                Assert.True(bands[i].From >= bands[i - 1].To - 1e-9, $"{bands[i].Name} after {bands[i - 1].Name}");
            }
        }
    }
}
