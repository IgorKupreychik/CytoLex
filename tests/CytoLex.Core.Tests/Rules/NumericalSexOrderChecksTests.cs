using System.Collections.Immutable;
using CytoLex.Core.Validation;
using CoreContext = CytoLex.Core.Context.SampleContext;

namespace CytoLex.Core.Tests.Rules;

/// <summary>T-030: count arithmetic, sex chromosome field and order of abnormalities.</summary>
public class NumericalSexOrderChecksTests
{
    private static readonly string[] _t030Rules =
    [
        "NUM.COUNT_MATCHES_CHANGES", "NUM.PLOIDY_REFERENCE", "SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST", "KARYO.REPLACED_NORMAL_NOT_MISSING",
        "SEX.CONSTITUTIONAL_COMPLEMENT", "SEX.ACQUIRED_GAIN_LOSS", "KARYO.UNCERTAIN_CONSTITUTIONAL", "MAR.CONSTITUTIONAL_C_SPACE",
        "KARYO.MULTIPLY_SIGN_REARRANGED_ONLY", "KARYO.ABNORMALITY_ORDER", "ORDER.X_BEFORE_Y", "ORDER.NUMERICAL_BEFORE_STRUCTURAL",
        "ORDER.HOMOLOGUES_ALPHABETICAL", "ORDER.UNKNOWN_CENTROMERE_DER", "ORDER.UNIDENTIFIED_LAST", "ORDER.SHORTEST_STRING_PREFERRED",
    ];

    private static ImmutableArray<Diagnostic> Diagnostics(string input, CoreContext? hint = null) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), ContextHint = hint })
            .Results.Single().Diagnostics;

    private static List<Diagnostic> Own(string input, CoreContext? hint = null) =>
        [.. Diagnostics(input, hint).Where(d => _t030Rules.Contains(d.RuleId))];

    [Theory]
    [InlineData("NUM.COUNT_MATCHES_CHANGES", "48,XX,+21", "48")]
    [InlineData("NUM.COUNT_MATCHES_CHANGES", "46,XX,+mar", "46")]
    [InlineData("NUM.COUNT_MATCHES_CHANGES", "46,XX,dic(13;15)(q22;q24)", "46")] // dic replaces both
    [InlineData("NUM.COUNT_MATCHES_CHANGES", "46,XX,der(13;21)(q10;q10)", "46")]
    [InlineData("NUM.COUNT_MATCHES_CHANGES", "60<2n>,XX,+8", "60<2n>")]
    [InlineData("NUM.PLOIDY_REFERENCE", "71,XX,+X,+1,+2,+3,+4,+5,+6,+7,+8,+8,+9,+10,+10,+11,+12,+13,+14,+15,+16,+17,+18,+19,+20,+21,+22", "71")]
    [InlineData("SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST", "46,XY,t(X;8)(p22.3;q24.1)", "XY")]
    [InlineData("SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST", "46,XX,t(X;13)(q27;q12)", "XX")]
    [InlineData("KARYO.REPLACED_NORMAL_NOT_MISSING", "46,XX,–4,del(4)(p14)", "–4")]
    [InlineData("KARYO.REPLACED_NORMAL_NOT_MISSING", "46,XX,–3,+inv(3)(q21q26.2)", "–3")]
    [InlineData("KARYO.MULTIPLY_SIGN_REARRANGED_ONLY", "48,XX,+21×2", "+21×2")]
    [InlineData("MAR.CONSTITUTIONAL_C_SPACE", "48,XXY,+marc", "marc")]
    [InlineData("KARYO.ABNORMALITY_ORDER", "47,XY,+21,del(5)(q13q33)", "del(5)(q13q33)")]
    [InlineData("KARYO.ABNORMALITY_ORDER", "47,XX,+8,t(X;13)(q27;q12)", "t(X;13)(q27;q12)")]
    [InlineData("ORDER.X_BEFORE_Y", "48,X,–Y,+X,+10", "+X")]
    [InlineData("ORDER.NUMERICAL_BEFORE_STRUCTURAL", "47,XX,inv(3)(q21q26.2),+3", "+3")]
    [InlineData("ORDER.HOMOLOGUES_ALPHABETICAL", "48,XX,+inv(1)(p31q41),+del(1)(p13)", "+del(1)(p13)")]
    [InlineData("ORDER.UNIDENTIFIED_LAST", "48,XX,+mar,+8", "+8")]
    [InlineData("ORDER.UNKNOWN_CENTROMERE_DER", "48,XX,+r,+der(?)t(?;6)(?;q16)", "+der(?)t(?;6)(?;q16)")]
    [InlineData("ORDER.UNKNOWN_CENTROMERE_DER", "48,XX,+der(?)t(?;6)(?;q16),+8", "+8")]
    public void Rule_Fires(string ruleId, string input, string spanText)
    {
        var d = Assert.Single(Diagnostics(input), d => d.RuleId == ruleId);
        Assert.Equal(spanText, d.Span.Of(input));
        Assert.Equal(DiagnosticOrigin.Rule, d.Origin);
        Assert.NotNull(d.Detail);
        Assert.NotEmpty(d.Citations);
    }

    /// <summary>Strings the book prints as correct, and the count conventions: none of the T-030 rules fires firmly.</summary>
    [Theory]
    [InlineData("26,X,+4,+6,+21")] // near-haploid, 23
    [InlineData("71,XXX,+8,+10")] // near-triploid, 69
    [InlineData("89,XXYY,–1,–3,–5,+8,–21")] // near-tetraploid, 92
    [InlineData("58<2n>,XY,+X,+4,+6,+8,+10,+11,+14,+14,+17,+18,+21,+21[10]")]
    [InlineData("68<3n>,XY,–X[10]")] // §11.2 E1: male triploid relative to XXY
    [InlineData("45,XX,dic(13;15)(q22;q24)")]
    [InlineData("46,XX,+13,dic(13;15)(q22;q24)")]
    [InlineData("45,XX,der(1;3)(p10;q10)")]
    [InlineData("44,XY,–1,der(1;3)(p10;q10)")]
    [InlineData("45,XY,–10,der(10)t(10;17)(q22;p12)")] // the homologue is lost as well
    [InlineData("45,XX,rob(13;21)(q10;q10)")]
    [InlineData("47,XY,–10,+fis(10)(p10),+fis(10)(q10)")] // fis products are written separately
    [InlineData("44,XX,trc(4;12;9)(q31.2;q22p13;q34)")]
    [InlineData("47,XX,+trc r(1;3;12)(p36.1q32;q26.3p24;p12q23)")]
    [InlineData("46,X,tas(1;X;12;7)(p36.3;q28p22.3;p13q24.3;p22)")]
    [InlineData("46,t(X;Y)(q22;q11.2)")]
    [InlineData("45,fra(X)(q27.3)")]
    [InlineData("47,XY,i(X)(q10)")]
    [InlineData("46,XX,del(6)(q13q23)×2")]
    [InlineData("47,XX,del(6)(q13q23)×2,+del(6)(q13q23)")]
    [InlineData("45,XY,upd der(13;13)(q10;q10)pat")]
    [InlineData("46,XXYc,–X")] // a constitutional field keeps the lost X
    [InlineData("44,Xc,–X")]
    [InlineData("45,Xc,t(X;18)(p11.1;q11.1)[20]")] // §11.3 E9: the constitutional field holds the translocated X
    [InlineData("48,XY,+21c,+21")]
    [InlineData("48,XXY,+mar c")]
    [InlineData("47,XXX?c")]
    [InlineData("46,XX,del(1)(q21),inc[4]")] // inc: not counted
    [InlineData("47~51,XY,t(11;22)(q24;q12),+1~5mar[cp10]")] // composite: not counted
    [InlineData("50,X,+X,–Y,+10,+14,+17,+21[5]/46,XY[15]")]
    [InlineData("47,Y,t(X;13)(q27;q12),inv(10)(p13q22),+21")]
    [InlineData("50,XX,+1,+del(1)(p13),+dup(1)(q21q32),+inv(1)(p31q41),+8,r(10)(p12q25),–21")]
    [InlineData("46,XX,der(8)ins(8;?)(p23;?)del(8)(q22)")]
    [InlineData("51,XY,+1,+3,+8,+r,+mar")]
    [InlineData("47,XX,t(2;5)(q21;q31)mat,+5")]
    [InlineData("47,XX,+2,t(2;5)(q21;q31)mat")]
    [InlineData("46,XX,der(1;3)(p10;q10),+3")]
    [InlineData("46,XX,+21,der(21;21)(q10;q10)")]
    [InlineData("47,XY,del(8)(q21.1) or i(8)(p10),+21")]
    public void BookForms_HaveNoFirmFinding(string input) =>
        Assert.DoesNotContain(Own(input), d => !d.IsConditional && d.Severity != Severity.Info);

    [Fact]
    public void CountRange_MismatchIsUncertain()
    {
        var d = Assert.Single(Own("50~55,XX,+8"));
        Assert.Equal("NUM.COUNT_MATCHES_CHANGES", d.RuleId);
        Assert.Equal(Certainty.Uncertain, d.Certainty);
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.Empty(Own("45~48,XX,+8"));
    }

    [Fact]
    public void LostSexChromosomeStillInField_IsASexFieldFinding_NotACountError()
    {
        var own = Own("45,XY,–Y");
        Assert.DoesNotContain(own, d => d.RuleId == "NUM.COUNT_MATCHES_CHANGES");
        Assert.Contains(own, d => d.RuleId == "SEX.ACQUIRED_GAIN_LOSS" && d.IsConditional);
        Assert.Contains(own, d => d.RuleId == "SEX.CONSTITUTIONAL_COMPLEMENT");
    }

    [Theory]
    [InlineData("47,XXX", "SEX.ACQUIRED_GAIN_LOSS", CoreContext.Acquired)]
    [InlineData("45,X", "SEX.ACQUIRED_GAIN_LOSS", CoreContext.Acquired)]
    [InlineData("47,XY,+X", "SEX.CONSTITUTIONAL_COMPLEMENT", CoreContext.Constitutional)]
    [InlineData("45,X,–X", "SEX.CONSTITUTIONAL_COMPLEMENT", CoreContext.Constitutional)]
    public void SexFieldRules_AreConditional_AndFirmWithTheirContext(string input, string ruleId, CoreContext context)
    {
        Assert.Contains(Diagnostics(input), d => d.RuleId == ruleId && d.IsConditional);
        Assert.Contains(Diagnostics(input, context), d => d.RuleId == ruleId && !d.IsConditional && d.Severity == Severity.Error);
        var other = context == CoreContext.Acquired ? CoreContext.Constitutional : CoreContext.Acquired;
        Assert.DoesNotContain(Diagnostics(input, other), d => d.RuleId == ruleId);
    }

    [Theory]
    [InlineData("46,XY,t(X;8)(p22.3;q24.1)", "SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST")]
    [InlineData("71,XX,+X,+1,+2,+3,+4,+5,+6,+7,+8,+8,+9,+10,+10,+11,+12,+13,+14,+15,+16,+17,+18,+19,+20,+21,+22", "NUM.PLOIDY_REFERENCE")]
    [InlineData("46,XX,–4,del(4)(p14)", "KARYO.REPLACED_NORMAL_NOT_MISSING")]
    public void CountMismatch_IsReportedOnce_ByTheMostPreciseRule(string input, string ruleId)
    {
        var own = Own(input);
        Assert.Contains(own, d => d.RuleId == ruleId);
        Assert.DoesNotContain(own, d => d.RuleId is "NUM.COUNT_MATCHES_CHANGES" or "SEX.ACQUIRED_GAIN_LOSS");
    }

    [Fact]
    public void OutOfOrderItem_IsReportedOnce_AgainstTheEarlierItem()
    {
        const string input = "49,X,inv(X)(p21q26),inv(3)(q21q26.2),+3,+7,+10,del(20)(q11.2),–20,+21";
        var order = Own(input).Where(d => d.RuleId != "ORDER.SHORTEST_STRING_PREFERRED").ToList();
        Assert.Equal(2, order.Count);
        Assert.All(order, d => Assert.Equal("ORDER.NUMERICAL_BEFORE_STRUCTURAL", d.RuleId));
        Assert.Equal(["+3", "–20"], order.Select(d => d.Span.Of(input)));
        Assert.Equal(["inv(3)(q21q26.2)", "del(20)(q11.2)"], order.Select(d => d.SecondarySpans.Single().Of(input)));
    }

    [Fact]
    public void ShortestString_IsAWarning()
    {
        const string input = "49,X,inv(X)(p21q26),+3,inv(3)(q21q26.2),+7,+10,–20,del(20)(q11.2),+21";
        var d = Assert.Single(Own(input));
        Assert.Equal("ORDER.SHORTEST_STRING_PREFERRED", d.RuleId);
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.Equal("+3", d.Span.Of(input));
        Assert.Empty(Own("49,X,inv(X)(p21q26),+inv(3)(q21q26.2),+7,+10,–20,del(20)(q11.2),+21"));
    }

    [Fact]
    public void UncertainConstitutional_IsAnInfo()
    {
        var d = Assert.Single(Own("47,XXXc?"));
        Assert.Equal("KARYO.UNCERTAIN_CONSTITUTIONAL", d.RuleId);
        Assert.Equal(Severity.Info, d.Severity);
        Assert.Equal("c?", d.Span.Of("47,XXXc?"));
    }
}
