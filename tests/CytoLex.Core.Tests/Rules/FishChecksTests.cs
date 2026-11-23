using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Rules;

/// <summary>T-033: the in situ rules (ch. 13). The KB examples are in the harness; these cover the edges.</summary>
public class FishChecksTests
{
    private static EditionResult Result(string input, SampleContext? hint = null) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), ContextHint = hint }).Results.Single();

    private static ImmutableArray<Diagnostic> Diagnostics(string input, SampleContext? hint = null) => Result(input, hint).Diagnostics;

    private static IEnumerable<string> Previews(Diagnostic d) => d.Fixes.Select(f => f.Preview).OfType<string>();

    [Theory]
    [InlineData("ISH.LOCUS_DESIGNATION", "ish del(7)(q11.23q11.23)(eln–)", "eln", "ish del(7)(q11.23q11.23)(ELN–)")]
    [InlineData("ISH.LOCUS_LIST", "ish del(7)(q11.23q11.23)(ELN–LIMK1–)", "", "ish del(7)(q11.23q11.23)(ELN–,LIMK1–)")]
    [InlineData("ISH.LOCUS_LIST", "ish 7q11.23(ELN×2;LIMK1×2)", ";", "ish 7q11.23(ELN×2,LIMK1×2)")]
    [InlineData("ISH.STRUCTURAL_FORMAT", "ish del(7)(ELN–)(q11.23q11.23)", "(ELN–)", "ish del(7)(q11.23q11.23)(ELN–)")]
    [InlineData("ISH.STRUCTURAL_FORMAT", "ish t(9;22)(q34q11.2)(ABL1+;BCR+)", "q34q11.2", "ish t(9;22)(q34;q11.2)(ABL1+;BCR+)")]
    [InlineData("ISH.LOCUS_STATUS", "ish del(7)(q11.23q11.23)(+ELN)", "+", "ish del(7)(q11.23q11.23)(ELN+)")]
    [InlineData("ISH.ABNORMAL_SIGNAL_COUNT", "ish dup(7)(q11.23q11.23)(ELN×3)", "×3", "ish dup(7)(q11.23q11.23)(ELN+++)")]
    [InlineData("ISH.INVERSION_SEPARATE_BREAKPOINTS", "ish inv(3)(q21q26.2)(RPN1+;MECOM+)", "(q21q26.2)(RPN1+;MECOM+)", "ish inv(3)(q21)(RPN1+)(q26.2)(MECOM+)")]
    [InlineData("ISH.SIGNAL_QUALIFIERS", "ish der(8)(MYCamp)", "amp", "ish der(8)(MYC amp)")]
    [InlineData("ISH.PARTIAL_PAINT", "ish dup(5)(pcp 5p15+)", " ", "ish dup(5)(pcp5p15+)")]
    [InlineData("NUC_ISH.DETAILED_FORMAT", "nuc ish13q14(RB1×1)", "", "nuc ish 13q14(RB1×1)")]
    [InlineData("NUC_ISH.SHARED_COUNT", "nuc ish(RB1×1,D13S319×1)", "(RB1×1,D13S319×1)", "nuc ish(RB1,D13S319)×1")]
    [InlineData("NUC_ISH.SIGNAL_POSITION_FORMAT", "nuc ish(ETV6,RUNX1)×2(ETV6sepRUNX1×1)", "ETV6sepRUNX1", "nuc ish(ETV6,RUNX1)×2(ETV6 sep RUNX1×1)")]
    [InlineData("NUC_ISH.ABNORMAL_OVER_TOTAL", "nuc ish(MYC×4)[20/200]/(MYC×2)[180/200]", "(MYC×2)[180/200]", "nuc ish(MYC×4)[20/200]")]
    [InlineData("REV_ISH.FORMAT", "rev ish7q", "", "rev ish 7q")]
    [InlineData("ISH.COMBINED_WITH_KARYOTYPE", "46,XX.ish22q11.2(HIRA×2)", "", "46,XX.ish 22q11.2(HIRA×2)")]
    public void Rule_Fires_WithItsFix(string ruleId, string input, string spanText, string preview)
    {
        var d = Assert.Single(Diagnostics(input), d => d.RuleId == ruleId);
        Assert.Equal(spanText, d.Span.Of(input));
        Assert.NotEmpty(d.Citations);
        Assert.Contains(preview, Previews(d));
    }

    [Theory]
    [InlineData("ISH.NORMAL_CHROMOSOME_FORMAT", "ish 7q11.23(ELN+)")]
    [InlineData("ISH.CELL_LINES", "nuc ish(MYC×4)[20%]")]
    [InlineData("NUC_ISH.SIGNAL_COUNT_FORMAT", "nuc ish (RB1×1)")]
    [InlineData("NUC_ISH.SIGNAL_COUNT_FORMAT", "nuc ish(RB1–)")]
    [InlineData("FIB_ISH.FORMAT", "fib ish(D15S11+,SNRPN+)")]
    public void Rule_Fires(string ruleId, string input) =>
        Assert.Contains(Diagnostics(input), d => d.RuleId == ruleId);

    [Theory]
    [InlineData("ish dmin(MYCN×20~50)[20]")] // cancer marker
    [InlineData("ish ider(17)(q10)(TP53×1)")]
    [InlineData("ish subtel(41×2)")]
    public void AbnormalSignalCount_NotReported_WithCancerMarkers(string input) =>
        Assert.DoesNotContain(Diagnostics(input), d => d.RuleId == "ISH.ABNORMAL_SIGNAL_COUNT");

    [Fact]
    public void AbnormalSignalCount_NotReported_WhenAcquired() =>
        Assert.DoesNotContain(Diagnostics("ish dup(7)(q11.23q11.23)(ELN×3)", SampleContext.Acquired), d => d.RuleId == "ISH.ABNORMAL_SIGNAL_COUNT");

    [Fact]
    public void CellsScored_IsConditionalOnTheNeoplasticContext()
    {
        var d = Assert.Single(Diagnostics("nuc ish(MYC×4)"), d => d.RuleId == "NUC_ISH.CELLS_SCORED");
        Assert.True(d.IsConditional);
        Assert.True(Result("nuc ish(MYC×4)").Validity.IsValid); // valid as constitutional
        Assert.DoesNotContain(Diagnostics("nuc ish(MYC×4)", SampleContext.Constitutional), d => d.RuleId == "NUC_ISH.CELLS_SCORED");
    }

    [Theory]
    [InlineData("ish dic(14;21)(p11.2;p11.2)(D14Z1/D22Z1+;D13Z1/D21Z1+)")] // contig members share the status
    [InlineData("ish inv(21)(q11.2q22.1)(q11.2)(RUNX1+)(q22.1)(RUNX1–)")]
    [InlineData("nuc ish(PAX7×1,PAX7 amp,FOXO1×1,FOXO1 amp)(PAX7 con FOXO1)amp")]
    [InlineData("nuc ish(DXZ1×2)[50]//(DXZ1,DYZ3)×1[300]/(DXZ1×1)[10]")]
    [InlineData("46,XX[20].ish 9q34(ABL1×2),22q11.2(BCR×2)[20].nuc ish(TP53×2)[400]")]
    public void WellFormed_HasNoErrors(string input) =>
        Assert.DoesNotContain(Diagnostics(input), d => d.Severity == Severity.Error && !d.IsConditional);

    /// <summary>
    /// Synthetic strings with the shape of real laboratory results (T-048: no patient data): typed x, a space after
    /// nuc ish, stray spaces after commas, [n/m] per hybridization, shared counts and a combined normal group.
    /// </summary>
    [Fact]
    public void LaboratoryShapedStrings()
    {
        const string first = "46,XX,del(13)(q12q22)[3]/46,XX[17].nuc ish (RB1x1,D13S319x2)[22/200], (TP53x2,D17Z1x3)[30/200]";
        var d1 = Diagnostics(first);
        Assert.Contains(d1, d => d.RuleId == "NUC_ISH.SIGNAL_COUNT_FORMAT" && d.Span.Of(first) == " ");
        Assert.Contains(d1, d => d.RuleId == "SYM.NO_OTHER_SPACE" && d.Span.Of(first) == " ");
        Assert.DoesNotContain(d1, d => d.RuleId is "SYM.TYPED_SYMBOL_FORMS" or "ISH.LOCUS_DESIGNATION" or "CORE.UNPARSED");

        const string second = "nuc ish(RB1x1,D13S319x2)[40/200]/(RB1,D13S319)x0[9/200],(ATM,TP53,D12Z3)x2[200],  (MYC,BCL2)x2[200]";
        var d2 = Diagnostics(second);
        Assert.Equal(["SYM.NO_OTHER_SPACE"], d2.Where(d => d.Severity == Severity.Error).Select(d => d.RuleId));
        Assert.Contains("nuc ish(RB1x1,D13S319x2)[40/200]/(RB1,D13S319)x0[9/200],(ATM,TP53,D12Z3)x2[200],(MYC,BCL2)x2[200]", d2.SelectMany(Previews));
    }
}
