using System.Collections.Immutable;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Rules;

/// <summary>T-034: the arr rules (ch. 14). The KB examples are in the harness; these cover the edges.</summary>
public class ArrChecksTests
{
    private static ImmutableArray<Diagnostic> Diagnostics(string input) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016") }).Results.Single().Diagnostics;

    private static EditionResult Result(string input) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016") }).Results.Single();

    private static IEnumerable<string> Previews(Diagnostic d) => d.Fixes.Select(f => f.Preview).OfType<string>();

    [Theory]
    [InlineData("GEN.GENOME_BUILD_REQUIRED", "arr 6q21q25.1(113900000_149100000)×1", "arr")]
    [InlineData("GEN.GENOME_BUILD_REQUIRED", "arr[GRCh38](21)×3", "]")] // no space after the build
    [InlineData("ARR.NUCLEOTIDE_FORMAT", "arr[GRCh38] 4q32.2q35.1(163146681–183022312)×1", "163146681–183022312")]
    [InlineData("ARR.NUCLEOTIDE_FORMAT", "arr[GRCh38] Xq25(126023321×2,126,228,413_126535347×1,126556900×2)", "126,228,413_126535347×1")]
    [InlineData("ARR.ABERRATION_ORDER", "arr(22,X,21)×3", "(22,X,21)")] // group-internal order
    [InlineData("ARR.ABERRATION_ORDER", "arr[GRCh38] 13q14.2(48986461_49176936)×0,13q12.11q14.2(19438807_48800573)×1", "13q12.11q14.2(19438807_48800573)×1")]
    [InlineData("ARR.ABERRATION_ORDER", "arr(Y)×0,(X)×1", "(X)×1")] // X before Y
    [InlineData("ARR.NORMAL_RESULT", "arr(X)×2,(1–22)×2", "(1–22)×2")]
    [InlineData("ARR.ABERRATIONS_ONLY", "arr(21)×3,(1–20,22)×2", "(1–20,22)×2")]
    [InlineData("ARR.REGION_FORMAT", "arr[GRCh38] 6q21q25.1×1", "6q21q25.1")] // no positions
    [InlineData("ARR.REGION_FORMAT", "arr[GRCh38] Xq28 or Yq12(155790872_156030895)×1", "Xq28 or Yq12(155790872_156030895)")]
    [InlineData("ARR.PTER_TO_QTER", "arr[GRCh38] 4q32.2q35.1(183022312_163146681)×1", "4q32.2q35.1(183022312_163146681)")] // positions only
    [InlineData("ARR.PTER_TO_QTER", "arr[GRCh38] 11p15.4p15.5(2265338_6275434)×2 hmz", "11p15.4p15.5(2265338_6275434)")] // p arm
    [InlineData("ARR.COPY_NUMBER", "arr(8)", "(8)")]
    [InlineData("ARR.COPY_NUMBER", "arr(8)×3×4", "×4")]
    [InlineData("ARR.MOSAIC_PROPORTION", "arr(X)×1[6]", "[6]")]
    [InlineData("ARR.INHERITANCE_SPACING", "arr[GRCh38] 11p12(37741458_39209912)×3pat", "pat")]
    [InlineData("ARR.ZYGOSITY", "arr(7)×2 hmzmat", "mat")] // the origin after hmz needs a space too
    [InlineData("ARR.ZYGOSITY", "arr(7)×2 hmz mos", "mos")]
    [InlineData("ARR.COMPLEX_CX", "arr[GRCh38] 6q25.1q27(149100000_170899992)cx", "cx")]
    [InlineData("ARR.CHROMOTHRIPSIS", "arr(1)×3cth", "cth")]
    [InlineData("ARR.COMBINED_WITH_OTHER_METHODS", "arr(22)×3.ish del(22)(q11.2q11.2)(HIRA–)", "arr(22)×3")]
    [InlineData("SYM.NO_SPACE_AT_PARENTHESIS", "arr(X,Y) ×1", " ")]
    [InlineData("SYM.NO_SPACE_AT_PARENTHESIS", "arr(1–22) cx", " ")]
    [InlineData("BAND.MUST_EXIST", "arr[GRCh38] 9q35(1_2)×1", "9q35")]
    [InlineData("CHROM.IDENTIFIERS", "arr(23)×3", "23")]
    [InlineData("BAND.DESIGNATION_FORMAT", "arr[GRCh38] 6 q21q25.1(113900000_149100000)×1", "6 q21")]
    public void Rule_Fires(string ruleId, string input, string spanText)
    {
        var d = Assert.Single(Diagnostics(input), d => d.RuleId == ruleId);
        Assert.Equal(spanText, d.Span.Of(input));
        Assert.NotNull(d.Detail);
        Assert.NotEmpty(d.Citations);
    }

    /// <summary>Book forms and their variants: no arr rule fires and the string is valid.</summary>
    [Theory]
    [InlineData("arr(1–22)×2,(X,Y)×1")]
    [InlineData("arr(X)×2,(Y)×1")] // gain of X in a male, Y written with it (§14.2 E4)
    [InlineData("arr(1–22)×3,(X)×2,(Y)×1")] // triploidy: (X)×2 is not a normal group listed with aberrations
    [InlineData("arr(1,2)×4,(3)×2,(4–22)×4")] // tetraploid baseline: ×2 is an aberration (§14.2.1 E9)
    [InlineData("arr(8)×3,(21)×3")]
    [InlineData("arr[GRCh38] (1)cth,6q25.1q27(149100000_170899992)×1,(13)cth")]
    [InlineData("arr[GRCh38] 13q14.2qter(49176999_115095706)×1")]
    [InlineData("arr[GRCh38] Xq28 or Yq12(155790872_156030895 or 56959165_57217415)×1")]
    [InlineData("arr[GRCh38] 4q32.2q35.1(163002425×2,163146681_183022312×1,184322231×2)dn")]
    [InlineData("arr[GRCh38] 11p12(37741458_39209912)×2 mos hmz mat")]
    [InlineData("arr[GRCh38] 11p15.5p15.4(2265338_6275434)×2 hmz c,19q13.33qter(49759500_58586384)×2 hmz")]
    [InlineData("arr[GRCh38] 11p12(37741458_39209912)x3")] // typed multiplication sign
    [InlineData("arr[GRCh38] 2q11.2q13(100982729_112106760)×1[0.4]")]
    [InlineData("47,XY,+mar.ish r(1)(p13.1q11?)(D1Z1+).arr[GRCh38] 1p12p11.2(117596421_121013236)×3 dn")]
    public void ArrRules_DoNotFire(string input)
    {
        var result = Result(input);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == Severity.Error).Select(d => $"{d.RuleId} '{d.Span.Of(input)}'"));
        Assert.True(result.Validity.IsValid);
    }

    /// <summary>
    /// GRCh37 positions printed under [GRCh38] (§14.2 E23, E44, E45): only the ARR.POSITIONS_IN_BANDS warning (T-088), so
    /// the strings stay valid as examples of their other rules.
    /// </summary>
    [Theory]
    [InlineData("arr[GRCh38] 8q23.1q24.3(105171556_146201911)×3,15q26.2q26.3(96062102_100201136)×1")]
    [InlineData("arr[GRCh38] 21q11.2q22.3(13531865_46914745)×3")]
    [InlineData("arr[GRCh38] 18p11.32p11.21(102328_15079388)×1,18q22.3q23(69172132_79093443)×1")]
    public void BookPositionsOfAnotherBuild_GetOnlyThePositionWarning(string input)
    {
        var result = Result(input);
        var d = Assert.Single(result.Diagnostics);
        Assert.Equal("ARR.POSITIONS_IN_BANDS", d.RuleId);
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.True(result.Validity.IsValid);
    }

    [Theory]
    [InlineData("arr[GRCh38] 4q35.1q32.2(183022312_163146681)×1", "ARR.PTER_TO_QTER", "arr[GRCh38] 4q32.2q35.1(163146681_183022312)×1")]
    [InlineData("arr[GRCh38] 4q32.2q35.1(163,146,681–183,022,312)×1", "ARR.NUCLEOTIDE_FORMAT", "arr[GRCh38] 4q32.2q35.1(163146681_183022312)×1")]
    [InlineData("arr(22,X,21)×3", "ARR.ABERRATION_ORDER", "arr(21,22,X)×3")]
    [InlineData("arr(21)×3,(1–20,22)×2", "ARR.ABERRATIONS_ONLY", "arr(21)×3")]
    [InlineData("arr(X,Y) ×1", "SYM.NO_SPACE_AT_PARENTHESIS", "arr(X,Y)×1")]
    [InlineData("arr[GRCh38] 2q11.2q13(100982729_112106760)×1.ish del(2)(q11.2q13)(RP11-11P22–)", "ARR.COMBINED_WITH_OTHER_METHODS", "ish del(2)(q11.2q13)(RP11-11P22–).arr[GRCh38] 2q11.2q13(100982729_112106760)×1")]
    public void Fix_PreviewsTheCorrectedForm(string input, string ruleId, string corrected)
    {
        var d = Assert.Single(Diagnostics(input), d => d.RuleId == ruleId);
        Assert.Contains(corrected, Previews(d));
    }

    [Fact]
    public void TrailingKaryotype_IsReportedOnceWithAMoveFix()
    {
        const string input = "arr(21)×3.47,XY,+21";
        var d = Assert.Single(Diagnostics(input), d => d.RuleId == "ARR.COMBINED_WITH_OTHER_METHODS");
        Assert.Equal(".47,XY,+21", d.Span.Of(input));
        Assert.Contains("47,XY,+21.arr(21)×3", Previews(d));
    }

    [Fact]
    public void ArrSegment_IsImplemented_AndCompleteWhenAlone()
    {
        var result = Result("arr(8)×3,(21)×3");
        var segment = Assert.Single(result.Segments);
        Assert.Equal(EditionStatus.Implemented, segment.Status);
        Assert.True(result.Validity.IsComplete);
        Assert.Equal(["(8)×3", "(21)×3"], result.Abnormalities.Select(a => a.ShortForm)); // T-072
    }

    [Fact]
    public void ArrWithoutResult_IsAnError()
    {
        var d = Assert.Single(Diagnostics("arr"));
        Assert.Equal("ARR.NORMAL_RESULT", d.RuleId);
    }

    [Fact]
    public void UnlistedWord_IsSymListedOnly()
    {
        const string input = "arr(8)×3 xyz";
        Assert.Contains(Diagnostics(input), d => d.RuleId == "SYM.LISTED_ONLY" && d.Span.Of(input) == "xyz");
    }
}
