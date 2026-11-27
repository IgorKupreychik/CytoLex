using CytoLex.Core.Context;
using CytoLex.Core.Translation;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Translation;

/// <summary>
/// T-093: the literal translation of in situ results (ish, nuc ish, fib ish, rev ish, cgh): the technique in the summary,
/// one sentence per result with its probe results, signal counts and positions, cells and cell lines, KB links,
/// coordinates and untranslated parts. The ch. 13 book examples are pinned by the golden file (RendererGoldenTests).
/// </summary>
public class InSituTranslationTests
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
    public void IshAbnormality_IsTheKaryotypeSentenceWithItsProbeResults()
    {
        var t = Translate("46,XX.ish del(22)(q11.2q11.2)(D22S75–)");
        Assert.Equal("46 chromosomes; sex chromosomes XX; no abnormality. Metaphase FISH (ish): 1 result.", t.Summary.Text);
        var item = Assert.Single(t.Items);
        Assert.True(item.IsTranslated);
        Assert.Equal("del(22)(q11.2q11.2)(D22S75–)", item.Text);
        Assert.Equal("Interstitial deletion of a small segment within band 22q11.2; probe D22S75 absent.", item.Sentence);
        Assert.False(t.IsPartial);
        Assert.Null(t.Banner);
    }

    [Fact]
    public void ProbeRuns_ListLociOfOneStatusTogether()
    {
        var item = Assert.Single(Translate("46,XY.ish del(15)(q11.2q12)(D15S11+,SNRPN–,D15S10–,GABRB3+)").Items);
        Assert.EndsWith("; probes D15S11 present, SNRPN and D15S10 absent, GABRB3 present.", item.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void SemicolonLists_AreGivenPerDerivative_AndPaintsAreSpelledOut()
    {
        var item = Assert.Single(Translate("46,XX.ish t(4;11)(p16.3;p15)(wcp11+,D4F26–,D4S96+,D4Z1+;D4F26+,wcp11+)").Items);
        Assert.EndsWith(
            "; on the derivative chromosome 4: whole chromosome paint 11 present, D4F26 absent, D4S96 and D4Z1 present; "
            + "on the derivative chromosome 11: D4F26 and whole chromosome paint 11 present.",
            item.Sentence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LociAfterEachBreakpoint_AreGivenPerBand()
    {
        var item = Assert.Single(Translate("46,XX.ish inv(21)(q11.2q22.1)(q11.2)(RUNX1+)(q22.1)(RUNX1–)").Items);
        Assert.EndsWith("; at 21q11.2: RUNX1 present; at 21q22.1: RUNX1 absent.", item.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalChromosomeAndBand_LeadTheSignalCounts()
    {
        var t = Translate("46,XX.ish X(DXZ1×2,SRY×0),17p11.2(RAI1×2)");
        Assert.Equal(
            ["Chromosome X: 2 signals for DXZ1 and no signal for SRY.", "At 17p11.2: 2 signals for RAI1."],
            t.Items.Select(i => i.Sentence));
    }

    [Theory]
    [InlineData("nuc ish(D21S65×3)", "3 signals for D21S65.")]
    [InlineData("nuc ish(KAL1,GK,DMD)×1", "1 signal each for KAL1, GK and DMD.")]
    [InlineData("nuc ish(D17Z1,ERBB2) ×4~5[100/200]", "4 to 5 signals each for D17Z1 and ERBB2; in 100 of 200 cells.")]
    [InlineData("nuc ish(MYCN×12~>50)[200]", "12 to more than 50 signals for MYCN; in 200 cells.")]
    [InlineData("nuc ish(MYCN amp)[200]", "MYCN amplified; in 200 cells.")]
    [InlineData("nuc ish(D13S319×1,D13S319 dim×1,LAMP1×2)[197/200]", "1 signal for D13S319, 1 signal for D13S319 of diminished intensity and 2 signals for LAMP1; in 197 of 200 cells.")]
    [InlineData("nuc ish(ABL1,BCR)×3(ABL1 con BCR×2)[400]", "3 signals each for ABL1 and BCR; 2 connected (fusion) signals of ABL1 and BCR; in 400 cells.")]
    [InlineData("nuc ish(MLL×2)(5′MLL sep 3′MLL×1)[200]", "2 signals for MLL; 1 pair of separated signals of 5′MLL and 3′MLL; in 200 cells.")]
    [InlineData("nuc ish(PAX7×1,PAX7 amp,FOXO1×1,FOXO1 amp)(PAX7 con FOXO1)amp", "1 signal for PAX7, PAX7 amplified, 1 signal for FOXO1 and FOXO1 amplified; connected (fusion) signals of PAX7 and FOXO1, amplified.")]
    [InlineData("nuc ish(RB1,D21S259/D21S341/D21S342)×3", "3 signals each for RB1 and D21S259/D21S341/D21S342.")]
    public void NucIsh_CountsPositionsAndQualifiers(string input, string sentence)
    {
        var t = Translate(input);
        Assert.Equal("Interphase FISH (nuc ish): 1 result.", t.Summary.Text);
        Assert.Equal(sentence, Assert.Single(t.Items).Sentence);
    }

    [Fact]
    public void TransplantChimera_NamesRecipientAndDonor()
    {
        var t = Translate("nuc ish(DXZ1×2)[50]//(DXZ1,DYZ3)×1[350]");
        Assert.Equal("Interphase FISH (nuc ish) of a transplant chimera with 2 cell lines: 2 results.", t.Summary.Text);
        Assert.Equal(
            ["Recipient: 2 signals for DXZ1; in 50 cells.", "Donor: 1 signal each for DXZ1 and DYZ3; in 350 cells."],
            t.Items.Select(i => i.Sentence));
    }

    [Theory]
    [InlineData("//nuc ish(DXZ1,DYZ3)×1[400]", "Interphase FISH (nuc ish), donor cells only: 1 result.")]
    [InlineData("nuc ish(DXZ1×2)[400]//", "Interphase FISH (nuc ish), recipient cells only: 1 result.")]
    public void OneSideOfATransplant_IsInTheSummary(string input, string summary)
    {
        Assert.Equal(summary, Translate(input).Summary.Text);
    }

    [Fact]
    public void CellLines_PrefixTheirResults()
    {
        var t = Translate("46,X,+r.ish r(X)(wcpX+,DXZ1+)[15]/r(X)(wcpX+,DXZ1++)[10]");
        Assert.Equal("46 chromosomes; sex chromosome X; 1 abnormality. Metaphase FISH (ish) with 2 cell lines: 2 results.", t.Summary.Text);
        Assert.Equal(
            [
                "An additional ring chromosome of unknown origin.",
                "Cell line 1: Ring chromosome X; breakpoints not given; probes whole chromosome paint X and DXZ1 present; in 15 cells.",
                "Cell line 2: Ring chromosome X; breakpoints not given; probes whole chromosome paint X present, DXZ1 present twice; in 10 cells.",
            ],
            t.Items.Select(i => i.Sentence));
    }

    [Theory]
    [InlineData("ish cgh enh(1)(q32q43),enh(12)(q13)", "Comparative genomic hybridization (cgh): 2 results.", "Gain (enhanced signal) of 1q32 to 1q43.")]
    [InlineData("ish cgh dim(7)(q21qter)", "Comparative genomic hybridization (cgh): 1 result.", "Loss (diminished signal) of 7q21 to 7qter.")]
    [InlineData("ish cgh enh(21)", "Comparative genomic hybridization (cgh): 1 result.", "Gain (enhanced signal) of chromosome 21.")]
    [InlineData("fib ish 15q11.2(D15S11+,SNRPN+,GABRB3+)", "FISH on extended DNA fibers (fib ish): 1 result.", "At 15q11.2, in order from pter to qter: D15S11, SNRPN and GABRB3 present.")]
    [InlineData("ish subtel(41×2)", "Metaphase FISH (ish): 1 result.", "Subtelomeric probe panel: 2 signals for each of the 41 subtelomeric regions.")]
    public void OtherTechniques_AreTranslated(string input, string summary, string first)
    {
        var t = Translate(input);
        Assert.Equal(summary, t.Summary.Text);
        Assert.Equal(first, t.Items[0].Sentence);
        Assert.All(t.Items, i => Assert.True(i.IsTranslated));
    }

    [Fact]
    public void RevIsh_RegionIsTheMaterialOfTheMarker()
    {
        var t = Translate("47,XX,+mar.rev ish 15q");
        Assert.Equal("47 chromosomes; sex chromosomes XX; 1 abnormality. Reverse in situ hybridization (rev ish): 1 result.", t.Summary.Text);
        Assert.Equal("Material from 15q.", t.Items[^1].Sentence);
    }

    [Fact]
    public void SeveralInSituSegments_EachAddTheirSummary()
    {
        var t = Translate("46,XY[20].ish 9q34(ABL1×2),22q11.2(BCR×2)[20].nuc ish(TP53×2)[400]");
        Assert.Equal(
            "46 chromosomes; sex chromosomes XY; no abnormality. Metaphase FISH (ish): 2 results. Interphase FISH (nuc ish): 1 result.",
            t.Summary.Text);
        Assert.Equal(3, t.Items.Length);
        Assert.Contains(t.Summary.Links, l => l.Ref.ToString() == "vocab:symbols/ish");
        Assert.Contains(t.Summary.Links, l => l.Ref.ToString() == "rule:NUC_ISH.SIGNAL_COUNT_FORMAT");
    }

    [Fact]
    public void Links_NameTheLocusRules()
    {
        var item = Assert.Single(Translate("nuc ish(ABL1,BCR)×2[400]").Items);
        var refs = item.Links.Select(l => l.Ref.ToString()).ToList();
        Assert.Contains("rule:ISH.LOCUS_DESIGNATION", refs);
        Assert.Contains("rule:ISH.CELL_LINES", refs);
        Assert.All(item.Links, l => Assert.False(string.IsNullOrWhiteSpace(l.Title)));
        Assert.True(item.IsDraft);
    }

    [Fact]
    public void Coordinates_GiveTheBandOfARegion()
    {
        var item = Assert.Single(Translate("nuc ish 21q22(D21S65×2)", coordinates: true).Items);
        Assert.Equal(["21q22"], item.Coordinates!.Bands.Select(b => b.Band));
        Assert.NotNull(item.Coordinates.Bands[0].Grch38);
    }

    [Fact]
    public void SyntaxError_LeavesTheResultUntranslated()
    {
        var t = Translate("nuc ish(ABL1,BCR×2[400]");
        Assert.Contains(t.Items, i => !i.IsTranslated && i.Sentence.StartsWith("Not translated", StringComparison.Ordinal));
        Assert.True(t.IsPartial);
        Assert.NotNull(t.Banner);
    }

    [Fact]
    public void KaryotypeWithNucIsh_KeepsTheKaryotypeReadings()
    {
        var t = Translate("47,XY,+21.nuc ish(D21S65×3)");
        Assert.Equal("47 chromosomes; sex chromosomes XY; 1 abnormality. Interphase FISH (nuc ish): 1 result.", t.Summary.Text);
        Assert.NotNull(t.Items[0].Readings);
        Assert.Null(t.Items[^1].Readings);
        Assert.Equal("3 signals for D21S65.", t.Items[^1].Sentence);
    }
}
