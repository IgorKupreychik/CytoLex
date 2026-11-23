using System.Collections.Immutable;
using CytoLex.Core.Validation;
using Severity = CytoLex.Core.Validation.Severity;

namespace CytoLex.Core.Tests.Rules;

/// <summary>
/// T-088: <c>ARR.POSITIONS_IN_BANDS</c> — arr positions vs. the bands of the stated build (GRCh37/GRCh38 coordinates of
/// <c>kb/bands/2016/bands.yaml</c>). The KB examples are in the harness; these cover the edges and the message.
/// </summary>
public class ArrPositionsInBandsTests
{
    internal const string Rule = "ARR.POSITIONS_IN_BANDS";

    private static ImmutableArray<Diagnostic> Findings(string input) =>
        [.. Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016") })
            .Results.Single().Diagnostics.Where(d => d.RuleId == Rule)];

    private static Diagnostic Single(string input) => Assert.Single(Findings(input));

    [Theory]
    [InlineData("arr[GRCh38] 4q32.2q35.1(163146681_183022312)×1")] // §14.2 E10
    [InlineData("arr[GRCh38] 17p12(14194598_15567589)×3")] // one band holds both
    [InlineData("arr[GRCh38] 19q13.33qter(49759500_58586384)×2 hmz")] // qter: ends within chromosome 19
    [InlineData("arr[GRCh38] 19q13.33qter(49759500_58617616)×1")] // the last base of chromosome 19
    [InlineData("arr[GRCh37] 19q13.33qter(49759500_59128983)×1")] // the same in GRCh37
    [InlineData("arr[GRCh38] 4q32.2q35.1(160000000×2,163146681_183022312×1,190000000×2)")] // flanks outside the bands
    [InlineData("arr[GRCh38] Xq28 or Yq12(155790872_156030895 or 56959165_57217415)×1")] // §14.2 E46, each reading
    public void FittingPositions_AreNotReported(string input) => Assert.Empty(Findings(input));

    [Theory]
    [InlineData("arr 4q32.2q35.1(1_2)×1")] // no build: GEN.GENOME_BUILD_REQUIRED
    [InlineData("arr[hg19] 4q32.2q35.1(1_2)×1")] // a build without band coordinates
    [InlineData("arr[NCBI36] 4q32.2q35.1(1_2)×1")]
    [InlineData("arr[GRCh38] 4q36(1_2)×1")] // unknown band: BAND.MUST_EXIST
    [InlineData("arr[GRCh38] 6q21.1(1_2)×1")] // a band without GRCh38 coordinates
    [InlineData("arr[GRCh38] 4q3(1_2)×1")] // region only
    [InlineData("arr[GRCh38] (8)×3")] // whole chromosome
    [InlineData("arr[GRCh38] (1–22,X)×2,(9q)×3")] // whole arm
    [InlineData("arr[GRCh38] 4q32.2q35.1(183022312_163146681)×1")] // reversed: ARR.PTER_TO_QTER
    public void Uncheckable_IsSkipped(string input) => Assert.Empty(Findings(input));

    [Fact]
    public void Finding_IsAWarning_WithAnExplanationOnly()
    {
        var d = Single("arr[GRCh38] 17p12(9000000_15567589)×3");

        Assert.Equal(Severity.Warning, d.Severity);
        Assert.All(d.Fixes, f => Assert.False(f.IsEdit));
        Assert.Contains(d.Fixes, f => f.Level == FixLevel.Explanation);
    }

    [Fact]
    public void StartOutsideTheFirstBand_NamesTheBandItLiesIn()
    {
        const string input = "arr[GRCh38] 17p12(9000000_15567589)×3";
        var d = Single(input);

        Assert.Equal("9000000", d.Span.Of(input));
        Assert.Contains("9000000 is not in 17p12 in GRCh38 (10800001_16100000)", d.Detail);
        Assert.Contains("it lies in 17p13", d.Detail);
    }

    [Fact]
    public void QterSpan_BeyondTheChromosomeEnd_IsReported()
    {
        const string input = "arr[GRCh38] 19q13.33qter(49759500_58700000)×1";
        var d = Single(input);

        Assert.Equal("58700000", d.Span.Of(input));
        Assert.Contains("58700000 lies beyond the end of chromosome 19 (58617616) in GRCh38", d.Detail);
        Assert.Contains("fit the GRCh37 bands", d.Detail); // 58700000 is in 19q13.43 of GRCh37 (to 59128983)
    }

    [Fact]
    public void DetailedSystem_ChecksTheAbnormalSpan()
    {
        const string input = "arr[GRCh38] 4q32.2q35.1(163002425×2,160000000_183022312×1,184322231×2)";
        var d = Single(input);

        Assert.Equal("160000000", d.Span.Of(input));
    }

    [Fact]
    public void OrReading_IsCheckedOnItsOwn()
    {
        const string input = "arr[GRCh38] Xq28 or Yq12(155790872_156030895 or 20000000_57217415)×1";
        var d = Single(input);

        Assert.Equal("20000000", d.Span.Of(input));
        Assert.Contains("not in Yq12", d.Detail);
    }

    [Fact]
    public void PositionsFittingTheOtherBuild_AreNamedInTheDetail()
    {
        // §14.2 E34: GRCh37 positions printed under [GRCh38] (E33 prints them under [GRCh37]).
        const string input = "arr[GRCh38] 20q13.2q13.33(51840606_62375085)×3,Yq11.23(26887746_27019505)×0";
        var d = Single(input);

        Assert.Equal("26887746", d.Span.Of(input));
        Assert.Equal(["27019505"], d.SecondarySpans.Select(s => s.Of(input)));
        Assert.EndsWith("The positions fit the GRCh37 bands.", d.Detail);
    }

    [Fact]
    public void MixedBuilds_GetNoOtherBuildHint()
    {
        // §14.2 E23: a GRCh38 start with a GRCh37 end.
        const string input = "arr[GRCh38] 8q23.1q24.3(105171556_146201911)×3,15q26.2q26.3(96062102_100201136)×1";
        var d = Single(input);

        Assert.Equal("146201911", d.Span.Of(input));
        Assert.Contains("146201911 lies beyond the end of chromosome 8 (145138636) in GRCh38", d.Detail);
        Assert.DoesNotContain("fit the", d.Detail);
    }

    [Fact]
    public void SeveralFailures_AreOneFindingPerSegment()
    {
        const string input = "arr[GRCh38] 17p12(9000000_15567589)×3,19q13.33qter(49759500_58700000)×1";
        var d = Single(input);

        Assert.Equal("9000000", d.Span.Of(input));
        Assert.Equal(["58700000"], d.SecondarySpans.Select(s => s.Of(input)));
        Assert.Contains("17p12", d.Detail);
        Assert.Contains("chromosome 19", d.Detail);
    }

    [Fact]
    public void EachArrSegment_IsCheckedOnItsOwn()
    {
        const string input = "46,XY.arr[GRCh38] 17p12(9000000_15567589)×3";
        var result = Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016") }).Results.Single();

        var d = Assert.Single(result.Diagnostics, d => d.RuleId == Rule);
        Assert.Equal("9000000", d.Span.Of(input));
    }
}
