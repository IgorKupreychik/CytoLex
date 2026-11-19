using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Rules;

/// <summary>T-029: the ch. 2–3 rule checks (chromosome identifiers, band format and existence, symbol spacing).</summary>
public class GeneralRuleChecksTests
{
    private static ImmutableArray<Diagnostic> Diagnostics(string input, bool fragment) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), IsFragment = fragment })
            .Results.Single().Diagnostics;

    [Theory]
    [InlineData("CHROM.IDENTIFIERS", "47,XY,+23", false, "23")]
    [InlineData("CHROM.IDENTIFIERS", "47,XX,+0", false, "0")]
    [InlineData("CHROM.IDENTIFIERS", "46,XY,t(23;1)(q11;p13)", false, "23")]
    [InlineData("CHROM.IDENTIFIERS", "23q11", true, "23")]
    [InlineData("BAND.DESIGNATION_FORMAT", "1p 31", true, "1p 31")]
    [InlineData("BAND.DESIGNATION_FORMAT", "1 p31", true, "1 p31")]
    [InlineData("BAND.DESIGNATION_FORMAT", "46,XX,t(1;3)(p 32;q21)", false, "p 32")]
    [InlineData("BAND.MUST_EXIST", "9q35", true, "9q35")]
    [InlineData("BAND.MUST_EXIST", "9q34.4", true, "9q34.4")]
    [InlineData("BAND.MUST_EXIST", "46,XX,del(9)(q35)", false, "q35")]
    [InlineData("BAND.MUST_EXIST", "46,XX,del(9)(q5)", false, "q5")]
    [InlineData("BAND.MUST_EXIST", "46,XY,dup(1)(q22~45)", false, "45")]
    [InlineData("BAND.GENOME_ASSEMBLY_ONLY", "9q34.11", true, "9q34.11")]
    [InlineData("BAND.GENOME_ASSEMBLY_ONLY", "46,XX,del(9)(q34.11)", false, "q34.11")]
    [InlineData("SYM.SPACE_BETWEEN_SYMBOLS", "psudic", true, "psudic")]
    [InlineData("SYM.SPACE_BETWEEN_SYMBOLS", "45,XX,psudic(15;13)(q12;q12)", false, "psudic")]
    [InlineData("SYM.SPACE_BEFORE_CHROMOSOME_COUNT", "mos47,XXX[25]/46,XX[5]", false, "mos47")]
    [InlineData("SYM.SPACE_BEFORE_CHROMOSOME_COUNT", "chi46,XX/46,XY", false, "chi46")]
    [InlineData("SYM.NO_SPACE_AT_PARENTHESIS", "46,XX,del (5)(q13q33)", false, " ")]
    [InlineData("SYM.NO_SPACE_AT_PARENTHESIS", "46,XX,del(5)( q13q33)", false, " ")]
    public void Rule_Fires(string ruleId, string input, bool fragment, string spanText)
    {
        var own = Diagnostics(input, fragment).Where(d => d.RuleId == ruleId).ToList();

        var d = Assert.Single(own);
        Assert.Equal(spanText, d.Span.Of(input));
        Assert.Equal(DiagnosticOrigin.Rule, d.Origin);
        Assert.NotNull(d.Detail);
        Assert.NotEmpty(d.Citations);
    }

    [Theory]
    [InlineData("CHROM.IDENTIFIERS", "47,XX,+21", false)]
    [InlineData("CHROM.IDENTIFIERS", "46,XY,t(X;8)(p22.3;q24.1)", false)]
    [InlineData("CHROM.IDENTIFIERS", "47,XX,+?", false)]
    [InlineData("BAND.DESIGNATION_FORMAT", "1p31", true)]
    [InlineData("BAND.DESIGNATION_FORMAT", "46,XX,del(5)(q13 q33)", false)] // space between bands, not inside one
    [InlineData("BAND.MUST_EXIST", "9q34.1", true)]
    [InlineData("BAND.MUST_EXIST", "1p36.33", true)]
    [InlineData("BAND.MUST_EXIST", "46,XX,del(1)(p1)", false)] // region only (§4.2)
    [InlineData("BAND.MUST_EXIST", "46,XY,t(1;3)(q10;p10)", false)] // centromere (§2.3.2)
    [InlineData("BAND.MUST_EXIST", "46,XX,del(5)(q2?3)", false)] // uncertain: not checked
    [InlineData("BAND.MUST_EXIST", "1P31", true)] // recovered input: the parser reports the arm
    [InlineData("BAND.MUST_EXIST", "9q34.11", true)] // genome-assembly band: BAND.GENOME_ASSEMBLY_ONLY
    [InlineData("BAND.MUST_EXIST", "47,XY,+23q11", false)] // invalid chromosome: CHROM.IDENTIFIERS
    [InlineData("BAND.MUST_EXIST", "46,XX,der(1)(1qter→1p22::3q13.1→3qter)", false)]
    [InlineData("BAND.GENOME_ASSEMBLY_ONLY", "46,XX,del(9)(q34.1)", false)]
    [InlineData("SYM.SPACE_BETWEEN_SYMBOLS", "psu dic", true)]
    [InlineData("SYM.SPACE_BETWEEN_SYMBOLS", "47,XY,+mardn[14]/46,XY[16]", false)] // suffix: KARYO.DE_NOVO_SPACE
    [InlineData("SYM.SPACE_BETWEEN_SYMBOLS", "14cenh+", true)] // variant features are written together
    [InlineData("SYM.SPACE_BETWEEN_SYMBOLS", "47,XX,+dicr(1;3)(p36.1q32;p24q26.2)", false)] // RING.DIC_TRC_ORDER (T-031)
    [InlineData("SYM.SPACE_BEFORE_CHROMOSOME_COUNT", "mos 47,XXX[25]/46,XX[5]", false)]
    [InlineData("SYM.NO_SPACE_AT_PARENTHESIS", "46,XX,del(5)(q13q33)", false)]
    [InlineData("SYM.NO_SPACE_AT_PARENTHESIS", "46,XY,del(8)(q21.1) or i(8)(p10)", false)]
    public void Rule_DoesNotFire(string ruleId, string input, bool fragment) =>
        Assert.DoesNotContain(Diagnostics(input, fragment), d => d.RuleId == ruleId);

    [Fact]
    public void SexChromosomeLetters_AreLeftToTheParser()
    {
        var d = Assert.Single(Diagnostics("45,Z", fragment: false), d => d.RuleId == "CHROM.IDENTIFIERS");
        Assert.Equal(DiagnosticOrigin.Parser, d.Origin);
    }

    [Fact]
    public void GenomeAssemblyBand_IsAnInfoNote_AndTheStringStaysValid()
    {
        var result = Validator.Default.Validate("46,XX,del(9)(q34.11)", new ValidationOptions { Editions = EditionSelection.One("2016") })
            .Results.Single();

        var d = Assert.Single(result.Diagnostics);
        Assert.Equal("BAND.GENOME_ASSEMBLY_ONLY", d.RuleId);
        Assert.Equal(Severity.Info, d.Severity);
        Assert.Contains("NCBI", d.Detail, StringComparison.Ordinal);
        Assert.True(result.Validity.IsValid);
    }

    [Fact]
    public void GenomeAssemblyBand_RuleDoesNotApplyToArraysOrSequences()
    {
        var rule = KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Rule("BAND.GENOME_ASSEMBLY_ONLY")!;

        Assert.True(rule.AppliesToType(NotationType.Karyotype));
        Assert.True(rule.AppliesToType(NotationType.Ish));
        Assert.False(rule.AppliesToType(NotationType.Arr));
        Assert.False(rule.AppliesToType(NotationType.Seq));
    }

    [Fact]
    public void BandSet_KnowsRegions()
    {
        var bands = KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Bands!;

        Assert.True(bands.ContainsRegion("1p1"));
        Assert.True(bands.ContainsRegion("9q3"));
        Assert.False(bands.ContainsRegion("9q5"));
    }
}
