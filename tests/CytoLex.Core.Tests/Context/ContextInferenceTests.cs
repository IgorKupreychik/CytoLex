using CytoLex.Core.Context;
using CytoLex.Core.Segmentation;

namespace CytoLex.Core.Tests.Context;

public class ContextInferenceTests
{
    private static ContextInfo Infer(string input, SampleContext? hint = null) =>
        ContextInference.Infer(input, Segmenter.Split(input), hint);

    [Theory]
    [InlineData("46,XX,t(9;22)(q34;q11.2)[20]")]
    [InlineData("47,XX,+21")]
    [InlineData("46,XY,del(5)(q13q33)")]
    [InlineData("46,XX,cen,cp,idic(17)(p11.2)")]
    [InlineData("mos 47,XXY[10]/46,XY[20]")]
    [InlineData("chi 46,XX[15]//46,XY[5]")]
    [InlineData("47,XX,+21/46,XX")]
    [InlineData("47,XX,+8[10]/46,XX[20]")]
    [InlineData("46,XX[20]//46,XY[10]")]
    [InlineData("45,X[25]/46,X,i(X)(q10)[25]")]
    [InlineData("46,XX,t(9;22)(q34;q11.2),del(11)(p11.2p15)[19]/46,XX[1]")]
    [InlineData("")]
    public void NoClearMarker_GivesUnknown(string input)
    {
        var info = Infer(input);

        Assert.Empty(info.Markers);
        Assert.Null(info.Inferred);
        Assert.Null(info.Effective);
        Assert.False(info.IsContradictory);
    }

    [Theory]
    [InlineData("48,idem,+9", ContextMarkerKind.StemlineReference, "idem")]
    [InlineData("47,XX,+8,sl,+9", ContextMarkerKind.StemlineReference, "sl")]
    [InlineData("47,XX,+8,sdl,+9", ContextMarkerKind.StemlineReference, "sdl")]
    [InlineData("48,XY,+8,+21c", ContextMarkerKind.ConstitutionalAnomaly, "c")]
    [InlineData("69<3n>,XXX,-1", ContextMarkerKind.PloidyLevel, "<3n>")]
    public void AcquiredMarker_GivesAcquired(string input, ContextMarkerKind kind, string markerText)
    {
        var info = Infer(input);

        var marker = Assert.Single(info.Markers);
        Assert.Equal(kind, marker.Kind);
        Assert.Equal(SampleContext.Acquired, marker.Indicates);
        Assert.Equal(markerText, marker.Span.Of(input));
        Assert.Equal(SampleContext.Acquired, info.Inferred);
        Assert.Equal(SampleContext.Acquired, info.Effective);
    }

    [Fact]
    public void Rec_GivesConstitutional()
    {
        const string input = "46,XX,rec(6)dup(6p)inv(6)(p22q25)";
        var info = Infer(input);

        var marker = Assert.Single(info.Markers);
        Assert.Equal(ContextMarkerKind.Recombinant, marker.Kind);
        Assert.Equal("rec", marker.Span.Of(input));
        Assert.Equal(SampleContext.Constitutional, info.Effective);
    }

    [Fact]
    public void ContradictingMarkers_GiveUnknownAndListBoth()
    {
        const string input = "46,XX,rec(6)dup(6p)inv(6)(p22q25)[10]/47,idem,+8[5]";
        var info = Infer(input);

        Assert.True(info.IsContradictory);
        Assert.Null(info.Inferred);
        Assert.Null(info.Effective);
        Assert.Equal(
            [ContextMarkerKind.Recombinant, ContextMarkerKind.StemlineReference],
            info.Markers.Select(m => m.Kind));
    }

    [Theory]
    [InlineData(SampleContext.Constitutional)]
    [InlineData(SampleContext.Acquired)]
    public void Hint_OverridesInference(SampleContext hint)
    {
        var info = Infer("48,XY,+8,+21c", hint);

        Assert.Equal(hint, info.Hint);
        Assert.Equal(SampleContext.Acquired, info.Inferred);
        Assert.Equal(hint, info.Effective);
    }

    [Fact]
    public void Hint_AppliesWithoutMarkers()
    {
        var info = Infer("47,XX,+21", SampleContext.Constitutional);

        Assert.Null(info.Inferred);
        Assert.Equal(SampleContext.Constitutional, info.Effective);
    }

    [Theory]
    [InlineData("46,XX.arr[GRCh38] 1p36.33(1_2)×1 c")]
    [InlineData("seq[GRCh37] del(1)(p36.33)chr1:c.123del")]
    [InlineData("46,XX.rsa 8p23.1(GATA4)×1 sl")]
    public void ArrRsaSeqSegments_AreNotScanned(string input)
    {
        Assert.Empty(Infer(input).Markers);
    }

    [Fact]
    public void IshSegments_AreScanned()
    {
        const string input = "46,XX.ish del(2)(q11.2q13)(RP11-11P22–)[10/30]";
        Assert.Empty(Infer(input).Markers);

        const string withC = "47,XX,+21c.ish 21q22(RUNX1×3)c";
        Assert.Equal(2, Infer(withC).Markers.Length);
    }

    [Fact]
    public void MarkerSpans_AreOffsetsInTheWholeInput()
    {
        const string input = "46,XX.ish 21q22(RUNX1×3)c";
        var marker = Assert.Single(Infer(input).Markers);

        Assert.Equal(input.Length - 1, marker.Span.Start);
        Assert.Equal("c", marker.Span.Of(input));
    }
}
