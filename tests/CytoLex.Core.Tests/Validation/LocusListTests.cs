using System.Text.Json;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Parsing;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>T-101: the locus list of a string (karyotype, arr, in situ) with coordinates from the band data.</summary>
public class LocusListTests
{
    private static readonly BandSet _bands = KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Bands!;

    private static EditionResult Result(string input, bool fragment = false) =>
        Assert.Single(Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), IsFragment = fragment }).Results);

    private static List<LocusEntry> Loci(string input) => [.. Result(input).Loci];

    private static GenomicRange Band38(string name) => _bands.Find(name)!.Grch38!;

    private static GenomicRange From(string first, string last, ReferenceBuild build = ReferenceBuild.Grch38) =>
        new(_bands.Find(first)!.In(build)!.Start, _bands.Find(last)!.In(build)!.End);

    // --- karyotype ---------------------------------------------------------------------------------------------------

    [Fact]
    public void WholeChromosomeGain()
    {
        var locus = Assert.Single(Loci("47,XY,+8"));
        Assert.Equal((LocusEffect.Gain, LocusExtent.Chromosome, "8", "+8"), (locus.Effect, locus.Extent, locus.Chromosome, locus.Text));
        Assert.Equal(_bands.ChromosomeRange("8", ReferenceBuild.Grch38), locus.Grch38);
        Assert.Equal(new GenomicRange(1, 145_138_636), locus.Grch38); // hand-checked: chr8 length in GRCh38
        Assert.Equal(0, locus.Abnormality);
        Assert.Equal(LocusSource.Written, locus.Source);
    }

    [Fact]
    public void WholeChromosomeLoss_WithCopies()
    {
        var loss = Assert.Single(Loci("45,XX,-7"));
        Assert.Equal((LocusEffect.Loss, "7"), (loss.Effect, loss.Chromosome));
        Assert.Equal("×2", Assert.Single(Loci("48,XY,+8×2")).Copy);
    }

    [Fact]
    public void InterstitialDeletion_LossFromFirstToLastBand()
    {
        var locus = Assert.Single(Loci("46,XX,del(5)(q13q33)"));
        Assert.Equal((LocusEffect.Loss, LocusExtent.BandRange, "5q13", "5q33"), (locus.Effect, locus.Extent, locus.From, locus.To));
        Assert.Equal("del(5)(q13q33)", locus.Text);
        Assert.Equal(From("5q13", "5q33"), locus.Grch38);
        Assert.Equal(From("5q13", "5q33", ReferenceBuild.Grch37), locus.Grch37);
        Assert.Equal(new GenomicRange(67_400_001, 160_500_000), locus.Grch38); // hand-checked against UCSC cytoBand
    }

    [Fact]
    public void TerminalDeletion_ToTheArmEnd()
    {
        var q = Assert.Single(Loci("46,XX,del(1)(q21)"));
        Assert.Equal(("1q21", "1qter"), (q.From, q.To));
        Assert.Equal(new GenomicRange(Band38("1q21").Start, _bands.ChromosomeRange("1", ReferenceBuild.Grch38)!.End), q.Grch38);

        var p = Assert.Single(Loci("46,XY,del(9)(p1)"));
        Assert.Equal(("9p1", "9pter"), (p.From, p.To));
        Assert.Equal(1, p.Grch38!.Start);
    }

    [Fact]
    public void DetailedSystemDeletion_SameSegment()
    {
        var locus = Assert.Single(Loci("46,XX,del(1)(pter→q21::q31→qter)"));
        Assert.Equal((LocusEffect.Loss, "1q21", "1q31"), (locus.Effect, locus.From, locus.To));
        Assert.Equal(From("1q21", "1q31"), locus.Grch38);
    }

    [Fact]
    public void Duplication_Gain()
    {
        var locus = Assert.Single(Loci("46,XX,dup(1)(q22q25)"));
        Assert.Equal((LocusEffect.Gain, "1q22", "1q25"), (locus.Effect, locus.From, locus.To));
    }

    [Fact]
    public void Translocation_OneBreakPerBreakpoint()
    {
        var loci = Loci("46,XY,t(9;22)(q34;q11.2)");
        Assert.Equal(["9q34", "22q11.2"], loci.Select(l => l.From));
        Assert.All(loci, l => Assert.Equal((LocusEffect.Break, LocusExtent.Band), (l.Effect, l.Extent)));
        Assert.Equal(["q34", "q11.2"], loci.Select(l => l.Text));
        Assert.Equal(Band38("22q11.2"), loci[1].Grch38);
        Assert.Equal(new GenomicRange(17_400_001, 25_500_000), loci[1].Grch38); // hand-checked
    }

    [Fact]
    public void Derivative_BreaksOfItsRearrangement()
    {
        var loci = Loci("46,XX,der(1)t(1;3)(p32;q21)");
        Assert.Equal(["1p32", "3q21"], loci.Select(l => l.From));
        Assert.All(loci, l => Assert.Equal(LocusEffect.Break, l.Effect));
    }

    [Fact]
    public void CentromericBreakpoints_AtTheArmEdge()
    {
        var loci = Loci("45,XX,rob(13;21)(q10;q10)");
        Assert.Equal(["13", "21"], loci.Select(l => l.Chromosome));
        var q = _bands.ArmRange("13", "q", ReferenceBuild.Grch38)!;
        Assert.Equal(new GenomicRange(q.Start, q.Start), loci[0].Grch38);
    }

    [Fact]
    public void UncertainBreakpoints()
    {
        var ranged = Loci("46,XX,inv(3)(q21~24q26)");
        Assert.Equal((LocusExtent.BandRange, "3q21", "3q24", true), (ranged[0].Extent, ranged[0].From, ranged[0].To, ranged[0].IsUncertain));
        Assert.Equal(From("3q21", "3q24"), ranged[0].Grch38);

        var questioned = Assert.Single(Loci("46,XY,del(1)(q2?)"));
        Assert.True(questioned.IsUncertain);
        Assert.Equal(_bands.ArmRange("1", "q", ReferenceBuild.Grch38)!.End, questioned.Grch38!.End);
    }

    [Fact]
    public void Alternatives_EveryReadingFlagged()
    {
        var loci = Loci("46,XX,t(1;3)(p32;q21) or t(1;3)(p31;q21)");
        Assert.Equal(["1p32", "3q21", "1p31", "3q21"], loci.Select(l => l.From));
        Assert.All(loci, l => Assert.True(l.IsAlternative));
        Assert.All(loci, l => Assert.Equal(0, l.Abnormality));
    }

    [Fact]
    public void Variant_OnTheArm()
    {
        var locus = Assert.Single(Loci("46,XY,16qh+"));
        Assert.Equal((LocusEffect.Variant, LocusExtent.Arm, "16", "q", false), (locus.Effect, locus.Extent, locus.Chromosome, locus.From, locus.IsUncertain));
    }

    [Fact]
    public void CloneReference_OwnItemsOnly_WithCells()
    {
        var result = Result("46,XX,del(5)(q13q33)[10]/47,idem,+8[5]");
        Assert.Equal(["del(5)(q13q33)", "+8"], result.Loci.Select(l => l.Text));
        Assert.Equal(["[10]", "[5]"], result.Loci.Select(l => l.Cells));
        Assert.Equal([0, 1], result.Loci.Select(l => l.CellLine));
        Assert.Equal(result.Abnormalities.Single(a => a.Text == "+8").Index, result.Loci[1].Abnormality);
    }

    [Fact]
    public void NoLoci_ForMarkersAndRefs()
    {
        Assert.Empty(Loci("47,XX,+mar"));
        Assert.Empty(Loci("46,XX"));
    }

    // --- arr ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void ArrPositions_WrittenForTheirBuild_BandsForTheOther()
    {
        var locus = Assert.Single(Loci("arr[GRCh38] 4q32.2q35.1(163146681_190957460)×1"));
        Assert.Equal((LocusEffect.CopyNumber, LocusExtent.Positions, "×1", "GRCh38"), (locus.Effect, locus.Extent, locus.Copy, locus.PositionsBuild));
        Assert.Equal(new GenomicRange(163_146_681, 190_957_460), locus.Grch38);
        Assert.Equal(From("4q32.2", "4q35.1", ReferenceBuild.Grch37), locus.Grch37);
        Assert.Equal(("4q32.2", "4q35.1"), (locus.From, locus.To));
        Assert.Equal(SegmentTechnique.Arr, locus.Technique);
    }

    [Fact]
    public void ArrWholeChromosomes_OnePerChromosome()
    {
        var loci = Loci("arr(1-22)×2,(X)×1");
        Assert.Equal(23, loci.Count);
        Assert.Equal([.. Enumerable.Range(1, 22).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)), "X"], loci.Select(l => l.Chromosome));
        Assert.Equal("×1", loci[^1].Copy);
        Assert.All(loci, l => Assert.Equal(LocusExtent.Chromosome, l.Extent));
    }

    [Fact]
    public void ArrBandsOnly_FromTheBandData()
    {
        var locus = Assert.Single(Loci("arr 4q32.2q35.1×1"));
        Assert.Equal((LocusExtent.BandRange, (string?)null), (locus.Extent, locus.PositionsBuild));
        Assert.Equal(From("4q32.2", "4q35.1"), locus.Grch38);
    }

    // --- in situ -----------------------------------------------------------------------------------------------------

    [Fact]
    public void DNumber_OnItsChromosome()
    {
        var locus = Assert.Single(Loci("nuc ish(D21Z1×3)[200]"));
        Assert.Equal((LocusEffect.Signal, LocusExtent.Chromosome, "21", "D21Z1", "×3", "[200]"), (locus.Effect, locus.Extent, locus.Chromosome, locus.Probe, locus.Copy, locus.Cells));
        Assert.Contains(KbRef.Rule(LocusList.LocusDesignation), locus.Kb);
        Assert.Equal(SegmentTechnique.InSitu, locus.Technique);
    }

    [Fact]
    public void NormalResult_MarkedNormal()
    {
        Assert.Equal(LocusEffect.Normal, Assert.Single(Loci("nuc ish(DXZ1×2)[400]")).Effect);
        Assert.Empty(Loci("46,XX.ish subtel(41×2)"));
    }

    [Fact]
    public void Paints_SAndFNumbers_ByName()
    {
        var loci = Loci("46,XX.ish t(4;11)(p16.3;p15)(wcp11+,D4F26–,D4S96+,D4Z1+;D4F26+,wcp11+)");
        Assert.Equal(["4p16.3", "11p15"], loci.Where(l => l.Effect == LocusEffect.Break).Select(l => l.From));
        var probes = loci.Where(l => l.Probe is not null).ToList();
        Assert.Equal(["wcp11", "D4F26", "D4S96", "D4Z1", "D4F26", "wcp11"], probes.Select(p => p.Probe));
        Assert.Equal(["11", "4", "4", "4", "4", "11"], probes.Select(p => p.Chromosome));
        Assert.Equal(["+", "–", "+", "+", "+", "+"], probes.Select(p => p.Copy));
    }

    [Fact]
    public void CombinedCentromereProbes_OnePerName()
    {
        var loci = Loci("45,XY,der(14;21)(q10;q10).ish der(14;21)(D14Z1/D22Z1+,D13Z1/D21Z1+)");
        Assert.Equal(["D14Z1", "D22Z1", "D13Z1", "D21Z1"], loci.Where(l => l.Probe is not null).Select(l => l.Probe));
        Assert.Equal(["14", "22", "13", "21"], loci.Where(l => l.Probe is not null).Select(l => l.Chromosome));
    }

    [Fact]
    public void RegionResult_ProbesOnTheRegion()
    {
        // The ish normal-chromosome form (ISH.NORMAL_CHROMOSOME_FORMAT) is a normal result (T-092).
        var locus = Assert.Single(Loci("46,XX.ish 22q11.2(HIRA×2)"));
        Assert.Equal((LocusExtent.Band, "22q11.2", "HIRA", "×2", LocusEffect.Normal), (locus.Extent, locus.From, locus.Probe, locus.Copy, locus.Effect));
        Assert.Equal(Band38("22q11.2"), locus.Grch38);
    }

    [Fact]
    public void IshDeletion_SegmentAndUnplacedProbe()
    {
        var loci = Loci("46,XY.ish del(22)(q11.2q11.2)(HIRA–)");
        Assert.Equal((LocusEffect.Loss, LocusExtent.Band, "22q11.2"), (loci[0].Effect, loci[0].Extent, loci[0].From));
        Assert.Equal((LocusExtent.Unplaced, "HIRA", "–"), (loci[1].Extent, loci[1].Probe, loci[1].Copy));
        Assert.NotNull(loci[1].UnplacedReason);
    }

    [Fact]
    public void GeneProbes_Unplaced()
    {
        var loci = Loci("nuc ish(ABL1,BCR)×2(ABL1 con BCR×1)[200]");
        Assert.Equal(["ABL1", "BCR", "ABL1", "BCR"], loci.Select(l => l.Probe));
        Assert.All(loci, l => Assert.Equal(LocusExtent.Unplaced, l.Extent));
        Assert.All(loci, l => Assert.Contains("T-103", l.UnplacedReason, StringComparison.Ordinal));
        Assert.Equal(["×2", "×2", null, "×1"], loci.Select(l => l.Copy));
    }

    [Theory]
    [InlineData("wcp11", "11", "")]
    [InlineData("wcpX", "X", "")]
    [InlineData("pcp1q", "1", "q")]
    [InlineData("D21Z1", "21", "")]
    [InlineData("DXZ1", "X", "")]
    [InlineData("D4S96", "4", "")]
    [InlineData("D4F26", "4", "")]
    [InlineData("D15S11", "15", "")]
    public void LocusNames_Placed(string name, string chromosome, string after) =>
        Assert.Equal((chromosome, after), LocusNames.Chromosome(name));

    [Theory]
    [InlineData("ABL1")]
    [InlineData("RP11-1076P8")]
    [InlineData("D23Z1")]
    [InlineData("DZ1")]
    [InlineData("KAL")]
    public void LocusNames_NotPlaced(string name) => Assert.Null(LocusNames.Chromosome(name));

    // --- every KB example --------------------------------------------------------------------------------------------

    public static IEnumerable<TheoryDataRow<string, bool>> KbValidCases() =>
        ParseHelper.KbExamples()
            .Where(x => x.Kind == ExampleKind.Valid)
            .Where(x => Segmenter.Split(x.Text).Any(s => s.Type is NotationType.Karyotype or NotationType.Arr or NotationType.Ish or NotationType.NucIsh or NotationType.FibIsh or NotationType.RevIsh or NotationType.Cgh))
            .Select(x => new TheoryDataRow<string, bool>(x.Text, x.Fragment) { TestDisplayName = $"{x.RuleId}: {x.Text}" });

    /// <summary>
    /// Acceptance: every valid karyotype, arr and FISH KB example gives loci whose spans lie in the input, whose
    /// chromosomes are those of the band data, whose coordinates lie on their chromosome, and every probe is listed.
    /// </summary>
    [Theory]
    [MemberData(nameof(KbValidCases))]
    public void KbExample_LociConsistent(string text, bool fragment)
    {
        var result = Result(text, fragment);
        Assert.Equal([.. Enumerable.Range(0, result.Loci.Length)], result.Loci.Select(l => l.Index));
        foreach (var locus in result.Loci)
        {
            Assert.True(locus.Span.End <= text.Length, $"span of {locus.Text}");
            Assert.Equal(locus.Span.Of(text), locus.Text);
            Assert.InRange(locus.Abnormality, 0, result.Abnormalities.Length - 1);
            var entry = result.Abnormalities[locus.Abnormality].Span;
            Assert.True(locus.Span.Start >= entry.Start && locus.Span.End <= entry.End, $"{locus.Text} inside its entry");
            if (!locus.IsPlaced)
            {
                Assert.Null(locus.Chromosome);
                Assert.NotNull(locus.UnplacedReason);
                continue;
            }

            Assert.Contains(locus.Chromosome!, _bands.Chromosomes);
            foreach (var build in new[] { ReferenceBuild.Grch37, ReferenceBuild.Grch38 })
            {
                if (locus.In(build) is { } range && locus.PositionsBuild is null)
                {
                    Assert.True(range.Start <= range.End, $"{locus.Text}: {range}");
                    Assert.True(_bands.ChromosomeRange(locus.Chromosome!, build)!.Contains(range), $"{locus.Text} on chromosome {locus.Chromosome} in {build}");
                }
            }
        }

        var probes = result.Abnormalities
            .Where(a => a.InSitu is not null)
            .SelectMany(a => a.InSitu!.Groups.SelectMany(g => g.Loci))
            .Where(l => !l.Name.All(char.IsAsciiDigit))
            .Select(l => l.Span)
            .ToList();
        Assert.All(probes, span => Assert.Contains(result.Loci, l => l.Span == span && l.Probe is not null));
    }

    [Fact]
    public void Json_RoundTrip()
    {
        var report = Validator.Default.Validate("46,XX,del(5)(q13q33).nuc ish(D21Z1×3,ABL1×2)[200].arr[GRCh38] 4q32.2q35.1(163146681_190957460)×1");
        var json = JsonSerializer.Serialize(report, Validator.JsonOptions);
        Assert.Contains("\"Loci\"", json, StringComparison.Ordinal);
        var back = JsonSerializer.Deserialize<ValidationReport>(json, Validator.JsonOptions)!;
        Assert.Equal(json, JsonSerializer.Serialize(back, Validator.JsonOptions));
        Assert.Equal(report.Results[0].Loci.Length, back.Results[0].Loci.Length);
    }

    [Fact]
    public void AutoCorrection_SpansReferToTheCorrectedText()
    {
        var result = Assert.Single(Validator.Default.Validate("46,XX,del(5)(q13 q33)", new ValidationOptions { Editions = EditionSelection.One("2016"), AutoCorrect = true }).Results);
        var locus = Assert.Single(result.Loci);
        Assert.Equal("del(5)(q13q33)", locus.Span.Of(result.AutoCorrection!.Corrected));
    }

    [Fact]
    public void NotImplementedEdition_NoLoci()
    {
        var report = Validator.Default.Validate("47,XY,+8", new ValidationOptions { Editions = EditionSelection.All });
        Assert.All(report.Results.Where(r => r.Status == EditionStatus.NotImplemented), r => Assert.Empty(r.Loci));
    }
}
