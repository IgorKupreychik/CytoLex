using CytoLex.Core.Context;
using CytoLex.Core.Translation;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Translation;

/// <summary>
/// T-105: the literal translation of microarray results: technique and genome build in the summary, one sentence per
/// result with the copy number as written (no gain/loss reading), regions with bands and positions, whole chromosomes and
/// arms, the detailed system, groups, <c>or</c> readings, <c>amp</c>/<c>cx</c>/<c>cth</c>, proportion and qualifiers,
/// KB links, coordinates and untranslated parts. The ch. 14 book examples are pinned by the golden file (RendererGoldenTests).
/// </summary>
public class ArrTranslationTests
{
    private static TranslationResult Translate(string input, SampleContext? hint = null, bool coordinates = false) =>
        Validator.Default.Validate(input, new ValidationOptions
        {
            Editions = EditionSelection.One("2016"),
            ContextHint = hint,
            IncludeTranslation = true,
            IncludeCoordinates = coordinates,
        }).Results.Single().Translation!;

    [Fact]
    public void Summary_NamesTheTechniqueAndTheBuild()
    {
        Assert.Equal("Microarray (arr, GRCh38): 2 results.", Translate("arr[GRCh38] 6q21q25.1(113900000_149100000)×1,(21)×3").Summary.Text);
        Assert.Equal("Microarray (arr): 1 result.", Translate("arr(1–22,X)×2").Summary.Text);
    }

    [Fact]
    public void WholeChromosomes_GiveTheCopyNumberAsWritten()
    {
        var t = Translate("arr(1–22)×2,(X,Y)×1");
        Assert.Equal(["2 copies of chromosomes 1 to 22.", "1 copy of chromosomes X and Y."], t.Items.Select(i => i.Sentence));
        Assert.All(t.Items, i => Assert.True(i.IsTranslated));
        Assert.False(t.IsPartial);
    }

    [Fact]
    public void Arms_AreNamedAndKeptApartFromRunsOfChromosomes()
    {
        Assert.Equal("3 copies of the long arm of chromosome 16.", Assert.Single(Translate("arr(16q)×3").Items).Sentence);
        Assert.Equal(
            "1 copy of chromosomes 3 and 7, the long arm of chromosome 9, and chromosomes 13 to 17, 19, 20, 22 and X.",
            Assert.Single(Translate("arr(3,7,9q,13–17,19,20,22,X)×1").Items).Sentence);
    }

    [Theory]
    [InlineData("arr[GRCh38] Xq25(126228413_126535347)×0 mat", "No copy of band Xq25 (positions 126228413 to 126535347), inherited from the mother.")]
    [InlineData("arr[GRCh38] 11p12(37741458_39209912)×3", "3 copies of band 11p12 (positions 37741458 to 39209912).")]
    [InlineData("arr[GRCh38] 12p13.33p11.1(84917_34382567)×2~4", "2 to 4 copies of the segment from 12p13.33 to 12p11.1 (positions 84917 to 34382567).")]
    [InlineData("arr[GRCh38] 19q13.33qter(49759500_58586384)×2 hmz", "2 copies of the segment from 19q13.33 to the end of the long arm of chromosome 19 (positions 49759500 to 58586384), homozygous.")]
    public void Regions_GiveBandsPositionsAndCopies(string input, string sentence) =>
        Assert.Equal(sentence, Assert.Single(Translate(input).Items).Sentence);

    [Fact]
    public void DetailedSystem_GivesEachPositionWithItsCopies()
    {
        var item = Assert.Single(Translate("arr[GRCh38] Xq25(126023321×1,126228413_126535347×0,126556900×1)mat").Items);
        Assert.Equal(
            "Band Xq25: position 126023321, 1 copy; positions 126228413 to 126535347, no copy; position 126556900, 1 copy; inherited from the mother.",
            item.Sentence);
    }

    [Fact]
    public void RegionGroup_SharesOneResult()
    {
        var item = Assert.Single(Translate("arr[GRCh38] (15q11.2q26.3(23123715_101888908),21q11.2q22.3(14595263_48084819))×2 hmz pat").Items);
        Assert.Equal(
            "2 copies of the segment from 15q11.2 to 15q26.3 (positions 23123715 to 101888908) and the segment from 21q11.2 to 21q22.3 (positions 14595263 to 48084819), homozygous, inherited from the father.",
            item.Sentence);
    }

    [Fact]
    public void OrReadings_KeepTheirOwnPositions()
    {
        var item = Assert.Single(Translate("arr[GRCh38] Xq28 or Yq12(155790872_156030895 or 56959165_57217415)×1").Items);
        Assert.Equal("1 copy of either band Xq28 (positions 155790872 to 156030895) or band Yq12 (positions 56959165 to 57217415).", item.Sentence);
        Assert.Contains(item.Links, l => l.Ref.ToString() == "rule:UNCERTAIN.OR_ALTERNATIVE");
    }

    [Theory]
    [InlineData("arr[GRCh38] 7p11.2(54290345_55087100)amp", "Amplification of band 7p11.2 (positions 54290345 to 55087100).")]
    [InlineData("arr(1–22,X)cx", "Multiple complex rearrangements in chromosomes 1 to 22 and X.")]
    [InlineData("arr(1p)cth", "Chromothripsis (alternating copy number changes) in the short arm of chromosome 1.")]
    public void WordsInThePlaceOfTheCopyNumber_AreWorded(string input, string sentence) =>
        Assert.Equal(sentence, Assert.Single(Translate(input).Items).Sentence);

    [Fact]
    public void ProportionAndQualifiers_FollowTheCopyNumber()
    {
        Assert.Equal("1 copy of chromosome X; in a proportion of 0.6 of cells.", Assert.Single(Translate("arr(X)×1[0.6]").Items).Sentence);
        Assert.Equal(
            "2 copies of band 11p12 (positions 37741458 to 39209912), present in some cells only (mosaic), homozygous, inherited from the mother.",
            Assert.Single(Translate("arr[GRCh38] 11p12(37741458_39209912)×2 mos hmz mat").Items).Sentence);
    }

    [Fact]
    public void CombinedString_JoinsEverySegmentsSummary()
    {
        var t = Translate("47,XY,+mar.ish r(1)(p13.1q11?)(D1Z1+).arr[GRCh38] 1p12p11.2(117596421_121013236)×3 dn");
        Assert.Equal(
            "47 chromosomes; sex chromosomes XY; 1 abnormality. Metaphase FISH (ish): 1 result. Microarray (arr, GRCh38): 1 result.",
            t.Summary.Text);
        Assert.Equal(3, t.Items.Length);
        Assert.Equal("3 copies of the segment from 1p12 to 1p11.2 (positions 117596421 to 121013236), de novo (not inherited).", t.Items[^1].Sentence);
        Assert.Contains(t.Summary.Links, l => l.Ref.ToString() == "vocab:symbols/arr");
    }

    [Fact]
    public void Links_NameTheArrRules()
    {
        var item = Assert.Single(Translate("arr[GRCh38] 17p11.2(16512256_20405113)×3 dn").Items);
        string[] expected = ["rule:ARR.REGION_FORMAT", "rule:ARR.NUCLEOTIDE_FORMAT", "rule:ARR.COPY_NUMBER", "rule:ARR.INHERITANCE_SPACING", "band:17p11.2"];
        Assert.All(expected, r => Assert.Contains(item.Links, l => l.Ref.ToString() == r));
        Assert.True(item.IsDraft);
    }

    [Fact]
    public void Coordinates_GiveTheBandsOfARegion()
    {
        var item = Assert.Single(Translate("arr[GRCh38] 6q21q25.1(113900000_149100000)×1", coordinates: true).Items);
        Assert.Equal(["6q21", "6q25.1"], item.Coordinates!.Bands.Select(b => b.Band));
        Assert.NotNull(item.Coordinates.Bands[0].Grch38);
    }

    [Fact]
    public void SyntaxError_LeavesTheResultUntranslated()
    {
        var t = Translate("arr[GRCh38] 6q21q25.1(113900000_149100000×1");
        Assert.Contains(t.Items, i => !i.IsTranslated && i.Sentence.StartsWith("Not translated", StringComparison.Ordinal));
        Assert.True(t.IsPartial);
    }
}
