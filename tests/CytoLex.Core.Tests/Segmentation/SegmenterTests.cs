using CytoLex.Core.Segmentation;
using CytoLex.Core.Text;

namespace CytoLex.Core.Tests.Segmentation;

public class SegmenterTests
{
    /// <summary>Input → expected segment types, in order.</summary>
    public static TheoryData<string, string[]> TypedStrings => new()
    {
        { "46,XX", ["karyotype"] },
        { "46,XY,del(5)(q13q33)", ["karyotype"] },
        { "46,XX.ish del(22)(q11.2q11.2)(HIRA-)", ["karyotype", "ish"] },
        { "46,XX. ish del(22)(q11.2q11.2)(HIRA-)", ["karyotype", "ish"] },
        { "nuc ish(D21S65×3)[200]", ["nuc_ish"] },
        { "nucish(D21S65×3)[200]", ["nuc_ish"] },
        { "46,XX.nuc ish(ABL1,BCR)×2[200]", ["karyotype", "nuc_ish"] },
        { "fib ish 22q11.2(N25×2)", ["fib_ish"] },
        { "rev ish enh(2)(p23.2p24)", ["rev_ish"] },
        { "cgh enh(8)", ["cgh"] },
        { "arr[GRCh38] Xp22.33q28(168546_155233730)×1[0.6]", ["arr"] },
        { "arr[GRCh38] (8)×3,9q34.11q34.3(129300000_140273252)×1.rsa(BCR::ABL1)pos", ["arr", "rsa"] },
        { "ish der(2)(p11.2q13)(D2Z2+)[5/30].arr[GRCh38] 2p11.2q13(90982729_96982729)×1", ["ish", "arr"] },
        { "seq[GRCh37] dup(8)(q24.21q24.21)chr8:g.128746677_128749160dup", ["seq"] },
        { "seq[GRCh37] t(2;11)(q31.1;q22.3)g.[chr2:pter_cen_100del]", ["seq"] },
        { "1p36.3.3", ["karyotype"] },
        { "46,XX.", ["karyotype"] },
        { "46,XX.1", ["karyotype"] },
        { "inv(2)", ["karyotype"] },
        { "arrangement", ["karyotype"] },
        { "ishx", ["karyotype"] },
        { "", ["karyotype"] },
    };

    [Theory]
    [MemberData(nameof(TypedStrings))]
    public void Split_DetectsTypesInOrder(string input, string[] expected)
    {
        var segments = Segmenter.Split(input);

        Assert.Equal(expected, segments.Select(s => s.Type.ToKbName()));
        Assert.Equal(Enumerable.Range(0, segments.Length), segments.Select(s => s.Index));
    }

    [Theory]
    [MemberData(nameof(TypedStrings))]
    public void Split_IsLossless(string input, string[] expected)
    {
        _ = expected;
        AssertLossless(input, Segmenter.Split(input));
    }

    [Fact]
    public void Split_ReportsSpansSeparatorsAndKeywords()
    {
        const string input = "46,XX. nuc ish(D21S65×3)[200]";
        var segments = Segmenter.Split(input);

        Assert.Equal(2, segments.Length);

        var karyotype = segments[0];
        Assert.Equal(new TextSpan(0, 5), karyotype.Span);
        Assert.Null(karyotype.Separator);
        Assert.Null(karyotype.Keyword);
        Assert.Empty(karyotype.KeywordVocabRefs);

        var nuc = segments[1];
        Assert.Equal(new TextSpan(5, 1), nuc.Separator);
        Assert.Equal(" nuc ish(D21S65×3)[200]", nuc.Span.Of(input));
        Assert.Equal("nuc ish", nuc.Keyword!.Value.Of(input));
        Assert.Equal(["vocab:symbols/nuc", "vocab:symbols/ish"], nuc.KeywordVocabRefs);
    }

    [Fact]
    public void Split_KeepsDotsInsideBracketsAndNumbers()
    {
        const string input = "46,XX,del(5)(q13.1q33.2)[20].arr[GRCh38] 9q34.11(1_2)×1[0.6]";
        var segments = Segmenter.Split(input);

        Assert.Equal(2, segments.Length);
        Assert.Equal("46,XX,del(5)(q13.1q33.2)[20]", segments[0].Span.Of(input));
        Assert.Equal(new TextSpan(28, 1), segments[1].Separator);
    }

    internal static void AssertLossless(string input, IReadOnlyList<Segment> segments)
    {
        var rebuilt = string.Concat(segments.Select(s =>
            (s.Separator is { } sep ? sep.Of(input) : "") + s.Span.Of(input)));
        Assert.Equal(input, rebuilt);

        var pos = 0;
        foreach (var s in segments)
        {
            if (s.Separator is { } sep)
            {
                Assert.Equal(pos, sep.Start);
                pos = sep.End;
            }

            Assert.Equal(pos, s.Span.Start);
            pos = s.Span.End;
        }

        Assert.Equal(input.Length, pos);
    }
}
