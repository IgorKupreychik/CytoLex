using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Parsing;
using CytoLex.Core.Tests.Semantics;
using CytoLex.Core.Validation;
using Severity = CytoLex.Core.Validation.Severity;

namespace CytoLex.Core.Tests.Rules;

/// <summary>T-103: the probe table (T-102) and the order checks ISH.LOCUS_ORDER_PTER_QTER and NUC_ISH.CHROMOSOME_ORDER.</summary>
public class ProbeOrderTests
{
    private const string _locusOrder = "ISH.LOCUS_ORDER_PTER_QTER";
    private const string _chromosomeOrder = "NUC_ISH.CHROMOSOME_ORDER";

    private static readonly ProbeTable _probes = KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Probes;

    private static ImmutableArray<Diagnostic> Diagnostics(string input, bool fragment = false) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), IsFragment = fragment }).Results.Single().Diagnostics;

    private static List<Diagnostic> Of(string input, string rule) => [.. Diagnostics(input).Where(d => d.RuleId == rule)];

    // --- probe table -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("ABL1", "ABL1", null)]
    [InlineData("abl1", "ABL1", null)]
    [InlineData("KAL1", "ANOS1", null)]
    [InlineData("MLL", "KMT2A", null)]
    [InlineData("5′MLL", "KMT2A", "5′")]
    [InlineData("3'IGH", "IGH", "3'")]
    public void ProbeTable_FindsByNameAliasAndEnd(string name, string row, string? end)
    {
        var match = _probes.Find(name);
        Assert.NotNull(match);
        Assert.Equal((row, end), (match.Row.Name, match.End));
    }

    [Theory]
    [InlineData("GLTSCR")]
    [InlineData("D21Z1")]
    [InlineData("5′")]
    public void ProbeTable_NoRow(string name) => Assert.Null(_probes.Find(name));

    [Fact]
    public void ProbeTable_RowFields()
    {
        var dmd = _probes.Find("DMD")!.Row;
        Assert.Equal(("X", "Xp21.2", "Xp21.1"), (dmd.Chromosome, dmd.FromBand, dmd.ToBand));
        Assert.NotNull(dmd.Grch38);
        Assert.NotEmpty(dmd.Source);
    }

    // --- NUC_ISH.CHROMOSOME_ORDER ------------------------------------------------------------------------------------

    [Fact]
    public void ChromosomeOrder_InGroup_UnreviewedRow_WarningNamingSource()
    {
        const string input = "nuc ish(BCR,ABL1)×2[400]";
        var d = Assert.Single(Of(input, _chromosomeOrder));
        Assert.Equal("ABL1", d.Span.Of(input));
        Assert.Equal("BCR", Assert.Single(d.SecondarySpans).Of(input));
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.Equal(Certainty.Uncertain, d.Certainty);
        Assert.Contains("not yet reviewed", d.Detail, StringComparison.Ordinal);
        Assert.Contains(_probes.Find("BCR")!.Row.Source, d.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ChromosomeOrder_BetweenHybridizations()
    {
        const string input = "nuc ish(ZNF443×2,GLTSCR×1)[105/200],(TP73×1,ANGPTL×2)[107/200]";
        var d = Assert.Single(Of(input, _chromosomeOrder));
        Assert.Equal("TP73", d.Span.Of(input));
        Assert.Equal("ZNF443", d.SecondarySpans[0].Of(input));
        Assert.Equal(Severity.Warning, d.Severity);
    }

    [Theory]
    [InlineData("nuc ish(D18Z1×2,DXZ1×1)", "DXZ1")]
    [InlineData("nuc ish(D21Z1×3),(D13Z1×2)", "D13Z1")]
    [InlineData("nuc ish(DYZ3×1,DXZ1×1)", "DXZ1")]
    public void ChromosomeOrder_ByName_Error(string input, string misplaced)
    {
        var d = Assert.Single(Of(input, _chromosomeOrder));
        Assert.Equal(misplaced, d.Span.Of(input));
        Assert.Equal((Severity.Error, Certainty.Certain), (d.Severity, d.Certainty));
        Assert.DoesNotContain("probe table", d.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nuc ish(ABL1,BCR)×3(ABL1 con BCR×1)(ABL1 con BCR con ABL1×1)[400]")] // con groups are not lists
    [InlineData("nuc ish(BCR×2)[200]/(ABL1×2)[200]")] // cell lines are judged apart
    [InlineData("nuc ish(KAL1,D21S65)×2")] // alias KAL1 → X
    [InlineData("nuc ish 9q34(ABL1×2),22q11.2(BCR×2)[400]")]
    [InlineData("nuc ish(GLTSCR,ANGPTL)×2")] // not in the table
    public void ChromosomeOrder_NotReported(string input) => Assert.Empty(Of(input, _chromosomeOrder));

    [Fact]
    public void ChromosomeOrder_DetailedRegions_WrittenChromosomesCount()
    {
        const string input = "nuc ish 22q11.2(BCR×2),9q34(ABL1×2)[400]";
        var d = Assert.Single(Of(input, _chromosomeOrder));
        Assert.Equal("9", d.Span.Of(input));
        Assert.Equal(Severity.Error, d.Severity); // written chromosomes: no probe table row involved
    }

    // --- ISH.LOCUS_ORDER_PTER_QTER -----------------------------------------------------------------------------------

    [Fact]
    public void LocusOrder_IshDeletion_Reversed()
    {
        const string input = "46,XY.ish del(15)(q11.2q12)(GABRB3+,D15S10–,SNRPN–,D15S11+)";
        var d = Assert.Single(Of(input, _locusOrder));
        Assert.Equal("SNRPN", d.Span.Of(input));
        Assert.Equal("GABRB3", d.SecondarySpans[0].Of(input));
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.Contains("source:", d.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nuc ish(CBFB,MYH11)×2[200]", "MYH11")]
    [InlineData("fib ish 9q34(ABL1+,ASS1+)", "ASS1")]
    [InlineData("46,XX.ish r(15)(p11.2q12)(GABRB3+,SNRPN+)", "SNRPN")]
    public void LocusOrder_Reported(string input, string misplaced) =>
        Assert.Equal(misplaced, Assert.Single(Of(input, _locusOrder)).Span.Of(input));

    [Theory]
    [InlineData("nuc ish(KAL1,STS)×2")] // one band (Xp22.31): below banding resolution
    [InlineData("nuc ish(KAL1,GK,DMD)×1")] // GK in DMD's band range
    [InlineData("nuc ish(3′IGH×2,5′IGH×3)(3′IGH con 5′IGH×1)[210/237]")] // two ends of one gene
    [InlineData("46,XX,inv(16)(p13.1q22).ish inv(16)(p13.1q22)(CBFB+,MYH11+;MYH11+,CBFB+)")] // inv: not read
    [InlineData("46,XX.ish t(9;22)(q34;q11.2)(ABL1+,BCR+;BCR+,ABL1+)")] // ; and other chromosomes
    [InlineData("nuc ish(MYH11,CBFB)×3(CBFB con MYH11×2)")] // con group
    public void LocusOrder_NotReported(string input) => Assert.Empty(Of(input, _locusOrder));

    // --- every valid FISH KB example ---------------------------------------------------------------------------------

    public static IEnumerable<TheoryDataRow<string, bool>> KbValidFishCases() =>
        ParseHelper.KbExamples()
            .Where(x => x.Kind == ExampleKind.Valid && BindHelper.BindAll(x.Text, x.Fragment).Any(m => m is FishModel))
            .Select(x => new TheoryDataRow<string, bool>(x.Text, x.Fragment) { TestDisplayName = $"{x.RuleId}: {x.Text}" });

    /// <summary>Acceptance: no valid in situ KB example reports either order rule.</summary>
    [Theory]
    [MemberData(nameof(KbValidFishCases))]
    public void KbValidExample_NoOrderFinding(string text, bool fragment) =>
        Assert.DoesNotContain(Diagnostics(text, fragment), d => d.RuleId is _locusOrder or _chromosomeOrder);
}
