using System.Text.Json;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Parsing;
using CytoLex.Core.Tests.Semantics;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>T-092: in situ results in the abnormality list (one group per cell line of an in situ segment, one entry per result).</summary>
public class InSituAbnormalityListTests
{
    private const string _combined =
        "46,XY,der(15)t(8;15)(q22.3;q26.2)mat.ish der(15)t(8;15)(RP11-1143I12+,RP11-14C10–).arr[GRCh38] 8q23.1q24.3(105171556_146201911)×3,15q26.2q26.3(96062102_100201136)×1";

    private static EditionResult Result(string input, bool fragment = false) =>
        Assert.Single(Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), IsFragment = fragment }).Results);

    private static List<CellLineSummary> Groups(EditionResult result) =>
        [.. result.CellLines.Where(c => c.Technique == SegmentTechnique.InSitu)];

    private static List<AbnormalityEntry> Entries(EditionResult result) =>
        [.. result.Abnormalities.Where(a => result.CellLines[a.CellLine].Technique == SegmentTechnique.InSitu)];

    private static AbnormalityEntry One(string input) => Assert.Single(Entries(Result(input)));

    public static IEnumerable<TheoryDataRow<string, bool, bool>> KbFishCases() =>
        ParseHelper.KbExamples()
            .Where(x => BindHelper.BindAll(x.Text, x.Fragment).Any(m => m is FishModel))
            .Select(x => new TheoryDataRow<string, bool, bool>(x.Text, x.Fragment, x.Kind == ExampleKind.Invalid) { TestDisplayName = $"{x.RuleId} {x.Kind}: {x.Text}" });

    /// <summary>Acceptance: every KB example with an in situ segment lists one group per cell line and one entry per result, with its span and loci.</summary>
    [Theory]
    [MemberData(nameof(KbFishCases))]
    public void KbExample_EveryResultListed(string text, bool fragment, bool invalid)
    {
        var result = Result(text, fragment);
        var models = BindHelper.BindAll(text, fragment).OfType<FishModel>().ToList();
        var groups = Groups(result);
        Assert.Equal(
            models.SelectMany(m => m.CellLines.IsEmpty ? [m.Span] : m.CellLines.Select(l => l.Span)),
            groups.Select(g => g.Span));

        var next = 0;
        foreach (var model in models)
        {
            var count = model.CellLines.IsEmpty ? 1 : model.CellLines.Length;
            var modelGroups = groups.Skip(next).Take(count).ToList();
            next += count;
            Assert.All(modelGroups, g => Assert.Equal(model.Type, g.Notation));
            var entries = result.Abnormalities.Where(a => modelGroups.Any(g => g.Index == a.CellLine)).ToList();
            Assert.Equal(modelGroups.Sum(g => g.AbnormalityCount), entries.Count);

            var items = model.CellLines.SelectMany(l => l.Items).ToList();
            Assert.Equal(
                items.Select(i => i.Span).Concat(model.Unbound.Select(u => u.Span)).OrderBy(s => s.Start),
                entries.Select(e => e.Span).OrderBy(s => s.Start));

            foreach (var item in items)
            {
                var e = Assert.Single(entries, x => x.Span == item.Span);
                Assert.Equal(item.Span.Of(text), e.Text);
                Assert.False(string.IsNullOrEmpty(e.ShortForm));
                var inSitu = Assert.IsType<InSituResultSummary>(e.InSitu);
                Assert.Equal(model.Type, inSitu.Notation);
                Assert.Equal(item.LocusGroups.Select(g => g.Span), inSitu.Groups.Select(g => g.Span));
                Assert.Equal(
                    item.LocusGroups.SelectMany(g => g.Loci).Select(l => (l.Name, l.LeadingSigns + l.Status, l.Count?.Text)),
                    inSitu.Groups.SelectMany(g => g.Loci).Select(l => (l.Name, l.Status, l.CountText)));
                Assert.Equal(item.Cells?.Text, inSitu.Cells?.Text);
                Assert.Equal(inSitu.IsNormal, inSitu.NormalRule is not null);
                if (item.Region is not null)
                {
                    Assert.Equal(AbnormalityKind.InSituRegion, e.Kind);
                }

                Assert.All(e.Diagnostics, d => Assert.True(
                    result.Diagnostics[d].Span.Start >= e.Span.Start && result.Diagnostics[d].Span.End <= e.Span.End));
                if (!invalid)
                {
                    Assert.False(e.Flags.IsPartial, $"'{e.Text}' is partial");
                    Assert.NotEqual(AbnormalityKind.Unrecognised, e.Kind);
                }
            }
        }
    }

    [Fact]
    public void NormalChromosome_Ish()
    {
        const string input = "46,XY.ish 22q11.2(D22S75×2)";
        var result = Result(input);
        var group = Assert.Single(Groups(result));
        Assert.Equal(NotationType.Ish, group.Notation);
        Assert.Equal(1, group.SegmentIndex);
        var e = Assert.Single(Entries(result));
        Assert.Equal(AbnormalityKind.InSituRegion, e.Kind);
        Assert.Equal("22q11.2(D22S75×2)", e.Text);
        Assert.Equal(["22"], e.Chromosomes);
        Assert.Equal(["22q11.2"], e.Breakpoints.Select(b => b.Text));
        Assert.True(e.InSitu!.IsNormal);
        Assert.Equal("ISH.NORMAL_CHROMOSOME_FORMAT", e.InSitu.NormalRule);
        var locus = Assert.Single(Assert.Single(e.InSitu.Groups).Loci);
        Assert.Equal(("D22S75", "2", 2), (locus.Name, locus.CountText, locus.CountLow));
        Assert.Equal("D22S75×2", locus.Span.Of(input));
    }

    [Fact]
    public void Abnormality_KeepsKaryotypeEntry()
    {
        var e = One("ish del(22)(q11.2q11.2)(D22S75–)");
        Assert.Equal(AbnormalityKind.Structural, e.Kind);
        Assert.Equal("del", e.Symbol);
        Assert.Equal("del(22)(q11.2q11.2)(D22S75–)", e.Text);
        Assert.Equal("del(22q)", e.ShortForm);
        Assert.Equal(["22"], e.Chromosomes);
        Assert.False(e.InSitu!.IsNormal);
        var locus = Assert.Single(Assert.Single(e.InSitu.Groups).Loci);
        Assert.Equal(("D22S75", "–"), (locus.Name, locus.Status));
    }

    [Fact]
    public void NucIsh_PositionGroup_NotNormal()
    {
        var e = One("nuc ish(ABL1,BCR)×2(ABL1 con BCR×1)[400]");
        Assert.Equal(AbnormalityKind.InSituRegion, e.Kind);
        Assert.Equal("(ABL1,BCR)×2(ABL1 con BCR×1)", e.ShortForm);
        var inSitu = e.InSitu!;
        Assert.False(inSitu.IsNormal);
        Assert.Equal(2, inSitu.Groups.Length);
        Assert.Equal(("2", false), (inSitu.Groups[0].SharedCount, inSitu.Groups[0].IsPosition));
        Assert.Equal([","], inSitu.Groups[0].Separators);
        Assert.Equal(["con"], inSitu.Groups[1].Separators);
        Assert.True(inSitu.Groups[1].IsPosition);
        Assert.Equal(400, inSitu.Cells!.Count);
        Assert.Null(inSitu.Cells.Total);
    }

    [Theory]
    [InlineData("nuc ish(ABL1,BCR)×2[400]", true)]
    [InlineData("nuc ish 21q22(D21S65×2)", true)]
    [InlineData("nuc ish 21q22(D21S65×3)", false)]
    [InlineData("nuc ish(D17Z1×2,ERBB2×10~20)[100/200]", false)]
    [InlineData("ish subtel(41×2)", true)]
    [InlineData("fib ish 15q11.2(D15S11+,SNRPN+,GABRB3+)", false)]
    [InlineData("ish cgh enh(21)", false)]
    public void NormalMark(string input, bool normal) => Assert.Equal(normal, One(input).InSitu!.IsNormal);

    [Fact]
    public void RevIsh_Region_NotNormal()
    {
        var e = One("47,XX,+mar.rev ish 15q");
        Assert.Equal(AbnormalityKind.InSituRegion, e.Kind);
        Assert.False(e.InSitu!.IsNormal);
        Assert.Equal(NotationType.RevIsh, e.InSitu.Notation);
    }

    [Fact]
    public void Subtel_NormalShortForm() =>
        Assert.Equal("ISH.SUBTEL_NORMAL_SHORT", One("ish subtel(41×2)").InSitu!.NormalRule);

    [Fact]
    public void CellLines_OneGroupEach()
    {
        var result = Result("nuc ish(D13S319×0,LAMP1×2)[100/400]/(D13S319×1,LAMP1×2)[50/400]");
        var groups = Groups(result);
        Assert.Equal(2, groups.Count);
        Assert.Equal([CellLineSeparator.None, CellLineSeparator.Slash], groups.Select(g => g.SeparatorBefore));
        Assert.Equal([100, 50], groups.Select(g => g.Cells));
        Assert.Equal([1, 1], groups.Select(g => g.AbnormalityCount));
        Assert.Equal([(100, 400), (50, 400)], Entries(result).Select(e => (e.InSitu!.Cells!.Count!.Value, e.InSitu.Cells.Total!.Value)));
    }

    [Fact]
    public void Transplant_LeadingDoubleSlash()
    {
        var group = Assert.Single(Groups(Result("//nuc ish(DXZ1,DYZ3)×1[400]")));
        Assert.Equal(CellLineSeparator.DoubleSlash, group.SeparatorBefore);
        Assert.Equal(CellLineRelation.Transplant, group.Relation);
    }

    [Fact]
    public void SeveralResultsPerLine_CellsPerResult()
    {
        var result = Result("nuc ish(ATM,TP53)×2[200],(D12Z3×3,D13S319×2,LAMP1×2)[100/200]");
        Assert.Equal(2, Assert.Single(Groups(result)).AbnormalityCount);
        Assert.Equal(["200", "100/200"], Entries(result).Select(e => e.InSitu!.Cells!.Text));
        Assert.Equal([true, false], Entries(result).Select(e => e.InSitu!.IsNormal));
    }

    [Fact]
    public void Combined_KaryotypeIshArr_InInputOrder()
    {
        var result = Result(_combined);
        Assert.Equal([SegmentTechnique.Karyotype, SegmentTechnique.InSitu, SegmentTechnique.Arr], result.CellLines.Select(c => c.Technique));
        Assert.Equal([0, 1, 2], result.CellLines.Select(c => c.SegmentIndex));
        Assert.Equal([0, 1, 2, 2], result.Abnormalities.Select(a => a.CellLine));
        var ish = result.Abnormalities[1];
        Assert.Equal("der", ish.Symbol);
        Assert.Equal(["RP11-1143I12", "RP11-14C10"], ish.InSitu!.Groups.SelectMany(g => g.Loci).Select(l => l.Name));
        Assert.Equal(["+", "–"], ish.InSitu.Groups.SelectMany(g => g.Loci).Select(l => l.Status));
    }

    [Fact]
    public void Diagnostics_Linked()
    {
        var result = Result("46,XY.ish (22q11.2)(D22S75×2)");
        var e = Assert.Single(Entries(result));
        Assert.Contains(e.Diagnostics, i => result.Diagnostics[i].RuleId == "ISH.NORMAL_CHROMOSOME_FORMAT");
    }

    [Fact]
    public void Json_RoundTrip()
    {
        var report = Validator.Default.Validate("46,XY[20].ish 9q34(ABL1×2),22q11.2(BCR×2)[20].nuc ish(ABL1,BCR)×3(ABL1 con BCR×1)(ABL1 con BCR con ABL1×1)[400]");
        var json = JsonSerializer.Serialize(report, Validator.JsonOptions);
        Assert.Contains("\"InSitu\"", json, StringComparison.Ordinal);
        var back = JsonSerializer.Deserialize<ValidationReport>(json, Validator.JsonOptions)!;
        Assert.Equal(json, JsonSerializer.Serialize(back, Validator.JsonOptions));
    }
}
