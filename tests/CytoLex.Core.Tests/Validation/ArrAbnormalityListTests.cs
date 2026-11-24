using System.Text.Json;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Parsing;
using CytoLex.Core.Tests.Semantics;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>T-072: arr results in the abnormality list (one group per arr segment, one entry per result).</summary>
public class ArrAbnormalityListTests
{
    private static EditionResult Result(string input, bool fragment = false) =>
        Assert.Single(Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), IsFragment = fragment }).Results);

    private static List<AbnormalityEntry> ArrEntries(EditionResult result) =>
        [.. result.Abnormalities.Where(a => result.CellLines[a.CellLine].Technique == SegmentTechnique.Arr)];

    private static AbnormalityEntry One(string input) => Assert.Single(ArrEntries(Result(input)));

    public static IEnumerable<TheoryDataRow<string, bool, bool>> KbArrCases() =>
        ParseHelper.KbExamples()
            .Where(x => BindHelper.BindAll(x.Text, x.Fragment).Any(m => m is ArrModel))
            .Select(x => new TheoryDataRow<string, bool, bool>(x.Text, x.Fragment, x.Kind == ExampleKind.Invalid) { TestDisplayName = $"{x.RuleId} {x.Kind}: {x.Text}" });

    /// <summary>Acceptance: every KB example with an arr segment lists one group per segment and one entry per result, with its bands, positions and copy number.</summary>
    [Theory]
    [MemberData(nameof(KbArrCases))]
    public void KbExample_EveryResultListed(string text, bool fragment, bool invalid)
    {
        var result = Result(text, fragment);
        var models = BindHelper.BindAll(text, fragment).OfType<ArrModel>().ToList();
        var groups = result.CellLines.Where(c => c.Technique == SegmentTechnique.Arr).ToList();
        Assert.Equal(models.Select(m => m.Span), groups.Select(g => g.Span));
        Assert.Equal(models.Select(m => m.Build?.Text), groups.Select(g => g.Build));

        for (var g = 0; g < models.Count; g++)
        {
            var (model, group) = (models[g], groups[g]);
            var entries = result.Abnormalities.Where(a => a.CellLine == group.Index).ToList();
            Assert.Equal(group.AbnormalityCount, entries.Count);
            var aberrations = model.Aberrations;
            Assert.Equal(
                aberrations.Select(a => a.Span).Concat(model.Unbound.Select(u => u.Span)).OrderBy(s => s.Start),
                entries.Select(e => e.Span));

            foreach (var a in aberrations)
            {
                var e = Assert.Single(entries, x => x.Span == a.Span);
                Assert.Equal(AbnormalityKind.ArrResult, e.Kind);
                var arr = Assert.IsType<ArrResultSummary>(e.Arr);
                Assert.Equal(a.Span.Of(text), e.Text);
                Assert.False(string.IsNullOrEmpty(e.ShortForm));
                Assert.Equal(model.Build?.Text, arr.Build);
                Assert.Equal(a.IsWholeChromosomes, arr.IsWholeChromosomes);
                Assert.Equal(a.Regions.Select(r => r.Span), arr.Regions.Select(r => r.Span));
                Assert.Equal(a.Regions.SelectMany(r => r.Bands).Select(b => b.Span), e.Breakpoints.Select(b => b.Span));
                Assert.Equal(
                    a.Regions.SelectMany(r => r.Positions?.Items ?? []).Select(i => (i.Start, i.End)),
                    arr.Regions.SelectMany(r => r.Positions).Select(p => (p.Start, p.End)));
                if (a.Copy is { } copy)
                {
                    Assert.Equal((copy.Kind, copy.Min, copy.Max), (arr.Kind, arr.CopyMin, arr.CopyMax));
                    Assert.Equal(copy.Span.Of(text), arr.CopyText);
                }

                Assert.Equal(a.Proportion?.Text, arr.Proportion);
                Assert.Equal(a.Qualifiers.FirstOrDefault(q => q.IsZygosity)?.Word, arr.Zygosity);
                Assert.Equal(a.Qualifiers.FirstOrDefault(q => q.IsInheritance)?.Word, e.Flags.Inheritance);
                Assert.All(e.Diagnostics, d => Assert.True(
                    result.Diagnostics[d].Span.Start >= e.Span.Start && result.Diagnostics[d].Span.End <= e.Span.End));
                if (!invalid)
                {
                    Assert.False(e.Flags.IsPartial, $"'{e.Text}' is partial");
                    Assert.NotNull(arr.CopyText);
                }
            }
        }
    }

    [Fact]
    public void Region_BandsPositionsCopyAndOrigin()
    {
        var result = Result("arr[GRCh38] 9p24.3(1310386_1709409)×1 mat,9p22.3p22.2(16455330_16763471)×1 dn,18q21.33q22.1(62747805_67920791)×1 dn");
        var group = Assert.Single(result.CellLines);
        Assert.Equal(SegmentTechnique.Arr, group.Technique);
        Assert.Equal("GRCh38", group.Build);
        Assert.Equal(3, group.AbnormalityCount);
        Assert.Null(group.Count);

        var e = result.Abnormalities[1];
        Assert.Equal("9p22.3p22.2(16455330_16763471)×1 dn", e.Text);
        Assert.Equal("9p22.3p22.2×1", e.ShortForm);
        Assert.Equal(["9"], e.Chromosomes);
        Assert.Equal(["9p22.3", "9p22.2"], e.Breakpoints.Select(b => b.Text));
        Assert.Equal("dn", e.Flags.Inheritance);
        var arr = e.Arr!;
        Assert.Equal((ArrResultKind.CopyNumber, 1, 1, "×1"), (arr.Kind, arr.CopyMin, arr.CopyMax, arr.CopyText));
        var region = Assert.Single(arr.Regions);
        Assert.Equal("9p22.3p22.2", region.Bands);
        var p = Assert.Single(region.Positions);
        Assert.Equal((16455330L, 16763471L), (p.Start!.Value, p.End!.Value));
        Assert.Equal(["mat", "dn", "dn"], result.Abnormalities.Select(a => a.Flags.Inheritance));
    }

    [Fact]
    public void WholeChromosomes()
    {
        var result = Result("arr(1–22)×2,(X,Y)×1");
        Assert.Null(Assert.Single(result.CellLines).Build);
        Assert.Equal(["(1–22)×2", "(X,Y)×1"], result.Abnormalities.Select(a => a.ShortForm));
        Assert.Equal(22, result.Abnormalities[0].Chromosomes.Length);
        Assert.Equal(["X", "Y"], result.Abnormalities[1].Chromosomes);
        Assert.True(result.Abnormalities[1].Arr!.IsWholeChromosomes);
        Assert.Equal(["16q"], One("arr(16q)×3").Chromosomes);
    }

    [Fact]
    public void RegionGroup_SharedResult()
    {
        var e = One("arr[GRCh38] (15q11.2q26.3(23123715_101888908),21q11.2q22.3(14595263_48084819))×2 hmz pat");
        Assert.Equal("(15q11.2q26.3,21q11.2q22.3)×2", e.ShortForm);
        Assert.Equal(["15", "21"], e.Chromosomes);
        Assert.True(e.Arr!.IsRegionGroup);
        Assert.Equal("hmz", e.Arr.Zygosity);
        Assert.Equal("pat", e.Flags.Inheritance);
        Assert.Equal(2, e.Arr.Regions.Length);
    }

    [Fact]
    public void DetailedSystem_CopyOfTheSpan()
    {
        var e = One("arr[GRCh38] 4q32.2q35.1(163002425×2,163146681_183022312×1,184322231×2)");
        Assert.True(e.Arr!.IsDetailed);
        Assert.Equal("×1", e.Arr.CopyText);
        Assert.Equal(1, e.Arr.CopyMin);
        Assert.Equal(["×2", "×1", "×2"], e.Arr.Regions[0].Positions.Select(p => p.CopyText));
        Assert.Equal("4q32.2q35.1×1", e.ShortForm);
    }

    [Fact]
    public void OrReadings_AreUncertain()
    {
        var e = One("arr[GRCh38] Xq28 or Yq12(155790872_156030895 or 56959165_57217415)×1");
        Assert.Equal("Xq28 or Yq12×1", e.ShortForm);
        Assert.Equal(["X", "Y"], e.Chromosomes);
        Assert.True(e.Flags.IsUncertain);
        var region = Assert.Single(e.Arr!.Regions);
        Assert.Equal("Yq12", Assert.Single(region.Alternatives).Bands);
        Assert.Single(Assert.Single(region.Positions).Alternatives);
    }

    [Theory]
    [InlineData("arr[GRCh38] 7p11.2(54290345_55087100)amp", ArrResultKind.Amplification, "amp", null, null)]
    [InlineData("arr[GRCh38] 12p13.33p11.1(84917_34382567)×2~4", ArrResultKind.CopyNumber, "×2~4", 2, 4)]
    [InlineData("arr[GRCh38] 21q11.2q22.3(13531865_46914745)×3", ArrResultKind.CopyNumber, "×3", 3, 3)]
    public void CopyNumber_AsWritten(string input, ArrResultKind kind, string copy, int? min, int? max)
    {
        var arr = One(input).Arr!;
        Assert.Equal((kind, copy, min, max), (arr.Kind, arr.CopyText, arr.CopyMin, arr.CopyMax));
    }

    [Fact]
    public void Chromothripsis_And_Proportion()
    {
        var result = Result("arr[GRCh38] (1)cth,6q25.1q27(149100000_170899992)×1,(13)cth");
        Assert.Equal([ArrResultKind.Chromothripsis, ArrResultKind.CopyNumber, ArrResultKind.Chromothripsis], result.Abnormalities.Select(a => a.Arr!.Kind));
        Assert.Equal(["(1)cth", "6q25.1q27×1", "(13)cth"], result.Abnormalities.Select(a => a.ShortForm));

        var mos = One("arr(X)×1[0.6]");
        Assert.Equal("0.6", mos.Arr!.Proportion);
        Assert.Equal("(X)×1", mos.ShortForm);
    }

    [Fact]
    public void Combined_KaryotypeThenArr()
    {
        var result = Result("46,XX,del(6)(q21q25.1).arr[GRCh38] 6q21q25.1(113900000_149100000)×1");
        Assert.Equal([SegmentTechnique.Karyotype, SegmentTechnique.Arr], result.CellLines.Select(c => c.Technique));
        Assert.Equal([0, 1], result.CellLines.Select(c => c.SegmentIndex));
        Assert.Equal([AbnormalityKind.Structural, AbnormalityKind.ArrResult], result.Abnormalities.Select(a => a.Kind));
        Assert.Equal([0, 1], result.Abnormalities.Select(a => a.CellLine));
    }

    [Fact]
    public void Diagnostics_Linked()
    {
        var result = Result("arr[GRCh38] 12p13.33p11.1(84917_34382567)×2-4");
        var e = Assert.Single(result.Abnormalities);
        Assert.Contains(e.Diagnostics, i => result.Diagnostics[i].RuleId == "ARR.COPY_NUMBER");
    }

    [Fact]
    public void Json_RoundTrip()
    {
        var report = Validator.Default.Validate("arr[GRCh38] Xq28 or Yq12(155790872_156030895 or 56959165_57217415)×1,(15q11.2q26.3(23123715_101888908),21q11.2q22.3(14595263_48084819))×2 hmz pat");
        var json = JsonSerializer.Serialize(report, Validator.JsonOptions);
        Assert.Contains("\"Arr\"", json, StringComparison.Ordinal);
        var back = JsonSerializer.Deserialize<ValidationReport>(json, Validator.JsonOptions)!;
        Assert.Equal(json, JsonSerializer.Serialize(back, Validator.JsonOptions));
    }
}
