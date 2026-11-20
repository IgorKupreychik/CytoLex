using System.Collections.Immutable;
using CytoLex.Core.Validation;
using CoreContext = CytoLex.Core.Context.SampleContext;

namespace CytoLex.Core.Tests.Rules;

/// <summary>T-068: the ch. 9 symbol rules, and the faults they take over from the general rules (one fault, one finding).</summary>
public class SymbolChecksTests
{
    private static readonly string[] _t068Rules =
    [
        "ADD.FORMAT", "DEL.FORMAT", "DUP.FORMAT", "FIS.FORMAT", "FRA.FORMAT", "ISO.FORMAT", "DER.NO_PH_SYMBOL", "DER.ISODERIVATIVE",
        "DIC.FORMAT", "DIC.NO_DER_DIC", "HSR.REPLACED_SEGMENT_PROXIMAL", "HSR.INTERFACE_TWO_CHROMOSOMES", "HSR.NO_ABR_SYMBOL",
        "MAR.PLUS_SIGN", "MAR.NO_SIZE_MORPHOLOGY", "MAR.NUMBERED", "DMIN.FORMAT", "ACE.NOT_IN_KARYOTYPE", "RING.UNIDENTIFIED",
        "TRANSLOC.WHOLE_ARM", "DER.WHOLE_ARM_TWO_CHROMOSOMES", "ROB.FORMAT", "ROB.NOT_ACQUIRED", "ROB.DICENTRIC_USE_DIC",
        "KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY",
    ];

    private static ImmutableArray<Diagnostic> Diagnostics(string input, CoreContext? hint = null) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), ContextHint = hint })
            .Results.Single().Diagnostics;

    private static List<Diagnostic> Own(string input, CoreContext? hint = null) =>
        [.. Diagnostics(input, hint).Where(d => _t068Rules.Contains(d.RuleId))];

    [Theory]
    [InlineData("ADD.FORMAT", "46,XX,t(1;?)(p36;?)", "t(1;?)(p36;?)")]
    [InlineData("DEL.FORMAT", "46,XX,del(5q)", "5q")]
    [InlineData("DEL.FORMAT", "46,XX,del(5)(q13→q33)", "q13→q33")]
    [InlineData("DUP.FORMAT", "46,XX,dup(1)(q22→q25)", "q22→q25")]
    [InlineData("FIS.FORMAT", "47,XY,–10,+fis(10)(p11.1),+fis(10)(q10)", "p11.1")]
    [InlineData("FRA.FORMAT", "46,X,fra(Xq27.3)", "Xq27.3")]
    [InlineData("ISO.FORMAT", "46,XX,i(17q)", "17q")]
    [InlineData("ISO.FORMAT", "46,XX,i(17)(q11.2)", "q11.2")]
    [InlineData("DER.NO_PH_SYMBOL", "47,XX,t(9;22)(q34;q11.2),+Ph", "Ph")]
    [InlineData("DER.ISODERIVATIVE", "46,XX,ider(22)(q11.2)t(9;22)(q34;q11.2)", "q11.2")]
    [InlineData("DIC.NO_DER_DIC", "45,XX,der dic(13;15)(q22;q24)", "der dic")]
    [InlineData("HSR.REPLACED_SEGMENT_PROXIMAL", "46,XY,der(1)del(1)(p21p33)hsr(1)(p33)", "p33")]
    [InlineData("HSR.INTERFACE_TWO_CHROMOSOMES", "46,XX,der(1)ins(1;7)(q21;p11.2p21)hsr(1)(q21)", "hsr(1)")]
    [InlineData("MAR.PLUS_SIGN", "47,XX,mar", "mar")]
    [InlineData("MAR.NO_SIZE_MORPHOLOGY", "47,XX,+min mar", "min")]
    [InlineData("MAR.NUMBERED", "51,XX,i(17)(q10),+3mar1,+mar2,+mar3", "3")]
    [InlineData("DMIN.FORMAT", "49,XX,+3mar,+1dmin", "+1dmin")]
    [InlineData("DMIN.FORMAT", "49,XX,1dmin,+3mar", "+3mar")]
    [InlineData("ACE.NOT_IN_KARYOTYPE", "46,XX,+ace", "+ace")]
    [InlineData("RING.UNIDENTIFIED", "50,XX,+1,+3,+8,r", "r")]
    [InlineData("RING.UNIDENTIFIED", "51,XY,+1,+3,+8,+mar,+r", "+r")]
    [InlineData("RING.UNIDENTIFIED", "49,XX,+1,+3,+8,r(7)", "r(7)")]
    [InlineData("TRANSLOC.WHOLE_ARM", "46,XY,t(1;3)(q10;p10)", "q10;p10")]
    [InlineData("TRANSLOC.WHOLE_ARM", "46,XY,t(1;3)(q10;q10)", "q10;q10")]
    [InlineData("DER.WHOLE_ARM_TWO_CHROMOSOMES", "45,XX,der(1)t(1;3)(p10;q10)", "der(1)")]
    [InlineData("ROB.FORMAT", "45,XX,rob(9;22)(q10;q10)", "rob(9;22)")]
    [InlineData("ROB.DICENTRIC_USE_DIC", "45,XY,rob(14;21)(p11.2;p11.2)", "rob(14;21)")]
    [InlineData("ROB.DICENTRIC_USE_DIC", "45,XY,der(14;21)(p11.2;p11.2)", "der(14;21)")]
    [InlineData("KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY", "46,XX,2del(6)(q13q23)", "2")]
    [InlineData("KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY", "47,XX,del(6)(q13q23)×3", "×3")]
    [InlineData("KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY", "70,XXX,del(6)(q13q23)×4", "×4")] // triploid: three homologues
    public void Rule_Fires(string ruleId, string input, string spanText)
    {
        var d = Assert.Single(Diagnostics(input), d => d.RuleId == ruleId);
        Assert.Equal(spanText, d.Span.Of(input));
        Assert.Equal(DiagnosticOrigin.Rule, d.Origin);
        Assert.NotNull(d.Detail);
        Assert.NotEmpty(d.Citations);
    }

    /// <summary>Unlisted words a symbol rule names are reported by the parser under that rule, not as SYM.LISTED_ONLY.</summary>
    [Theory]
    [InlineData("46,XX,iso(17)(q10)", "ISO.FORMAT", "iso")]
    [InlineData("46,XX,abr(1)(p22)", "HSR.NO_ABR_SYMBOL", "abr")]
    [InlineData("47,XX,+acro mar", "MAR.NO_SIZE_MORPHOLOGY", "acro")]
    public void UnlistedWord_NamedByASymbolRule(string input, string ruleId, string spanText)
    {
        var diagnostics = Diagnostics(input);
        var d = Assert.Single(diagnostics, d => d.RuleId == ruleId);
        Assert.Equal(spanText, d.Span.Of(input));
        Assert.Equal(DiagnosticOrigin.Parser, d.Origin);
        Assert.DoesNotContain(diagnostics, d => d.RuleId == "SYM.LISTED_ONLY");
        Assert.Contains(Diagnostics("46,XX,dl(5)(q13q33)"), d => d.RuleId == "SYM.LISTED_ONLY");
    }

    /// <summary>One fault, one finding: the symbol rule takes over from the general count and order rules (T-068).</summary>
    [Theory]
    [InlineData("45,XX,–1,–3,der(1;3)(p10;q10)", "DER.WHOLE_ARM_TWO_CHROMOSOMES", "KARYO.REPLACED_NORMAL_NOT_MISSING", 2)]
    [InlineData("45,XX,–13,–15,dic(13;15)(q22;q24)", "DIC.FORMAT", "KARYO.REPLACED_NORMAL_NOT_MISSING", 2)]
    [InlineData("45,XX,–13,–21,rob(13;21)(q10;q10)", "ROB.FORMAT", "KARYO.REPLACED_NORMAL_NOT_MISSING", 2)]
    [InlineData("48,XX,+mar,+r", "RING.UNIDENTIFIED", "ORDER.UNIDENTIFIED_LAST", 1)]
    [InlineData("49,XX,+1,+3,+8,r(7)", "RING.UNIDENTIFIED", "KARYO.ABNORMALITY_ORDER", 1)]
    [InlineData("49,XX,1dmin,+3mar", "DMIN.FORMAT", "ORDER.UNIDENTIFIED_LAST", 1)]
    [InlineData("47,XX,del(6)(q13q23)×3", "KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY", "NUM.COUNT_MATCHES_CHANGES", 1)]
    public void SymbolRule_TakesOver(string input, string ruleId, string generalRule, int count)
    {
        var diagnostics = Diagnostics(input);
        Assert.Equal(count, diagnostics.Count(d => d.RuleId == ruleId));
        Assert.DoesNotContain(diagnostics, d => d.RuleId == generalRule);
    }

    /// <summary>An arrow in the short system is the symbol's format fault alone, not a detailed-system fault too.</summary>
    [Theory]
    [InlineData("46,XX,del(5)(q13→q33)", "DEL.FORMAT")]
    [InlineData("46,XX,dup(1)(q22→q25)", "DUP.FORMAT")]
    public void ShortSystemArrow_IsTheOnlyFinding(string input, string ruleId) =>
        Assert.Equal([ruleId], Diagnostics(input).Select(d => d.RuleId));

    [Fact]
    public void GeneralRules_KeepTheirOwnFaults()
    {
        Assert.Contains(Diagnostics("46,XX,–4,del(4)(p14)"), d => d.RuleId == "KARYO.REPLACED_NORMAL_NOT_MISSING");
        Assert.Contains(Diagnostics("48,XX,+mar,+8"), d => d.RuleId == "ORDER.UNIDENTIFIED_LAST");
        Assert.Contains(Diagnostics("47,XY,+21,del(5)(q13q33)"), d => d.RuleId == "KARYO.ABNORMALITY_ORDER");
    }

    [Fact]
    public void RepeatedNumberedMarker_ReportedOnTheRepeats()
    {
        const string input = "51,XX,i(17)(q10),+mar1,+mar1,+mar1,+mar2,+mar3";
        var found = Own(input).Where(d => d.RuleId == "MAR.NUMBERED").ToList();
        Assert.Equal(2, found.Count);
        Assert.All(found, d => Assert.Equal("+mar1", d.Span.Of(input)));
        Assert.Equal([23, 29], found.Select(d => d.Span.Start));
    }

    [Fact]
    public void RobNotAcquired_IsConditional_AndFirmWhenAcquired()
    {
        const string input = "45,XX,rob(13;21)(q10;q10)";
        Assert.Contains(Diagnostics(input), d => d.RuleId == "ROB.NOT_ACQUIRED" && d.IsConditional);
        Assert.Contains(Diagnostics(input, CoreContext.Acquired), d => d.RuleId == "ROB.NOT_ACQUIRED" && !d.IsConditional && d.Severity == Severity.Error);
        Assert.DoesNotContain(Diagnostics(input, CoreContext.Constitutional), d => d.RuleId == "ROB.NOT_ACQUIRED");
    }

    [Theory]
    [InlineData("46,XX,t(1;?)(p36;?)", "ADD.FORMAT", Severity.Warning)]
    [InlineData("47,XX,+min mar", "MAR.NO_SIZE_MORPHOLOGY", Severity.Warning)]
    [InlineData("46,XY,der(1)del(1)(p21p33)hsr(1)(p33)", "HSR.REPLACED_SEGMENT_PROXIMAL", Severity.Info)]
    public void Severity_ComesFromTheKb(string input, string ruleId, Severity severity) =>
        Assert.Equal(severity, Assert.Single(Diagnostics(input), d => d.RuleId == ruleId).Severity);

    /// <summary>Book forms and near misses: none of the T-068 rules fires firmly.</summary>
    [Theory]
    [InlineData("46,XX,idic(17)(p11.2)")] // idic breaks outside the centromere
    [InlineData("46,XY,t(1;3)(p10;q10)")]
    [InlineData("46,XY,t(1;3)(p10;p10)")]
    [InlineData("46,X,t(X;18)(p11.1;q11.1)")] // §4.3.1.1 E3: an ordinary translocation at p11.1/q11.1
    [InlineData("46,XX,der(1)t(1;?)(p36;?)")] // inside a derivative: no add suggestion
    [InlineData("46,XX,t(1;?)(p36;q13)")]
    [InlineData("45,XX,–mar")] // a lost marker keeps its minus sign
    [InlineData("45,XX,der(5;7)t(3;7)(q21;q21)")] // a dicentric derivative with aberrations: not a whole-arm der
    [InlineData("46,XY,rec(1)dup(5q)ins(1;5)(q32;q11.2q22)inh,rec(5)del(1q)ins(1;5)inh")] // arms inside a recombinant
    [InlineData("46,XX,del(5)(pter→q13:)")]
    [InlineData("46,XX,dup(1)(pter→q25::q22→qter)")]
    [InlineData("53,XX,+1,+2,+3,+4,+5,+r1,+r2")]
    [InlineData("51,XY,+5r")]
    [InlineData("51,XX,+1,+2,+3mar,9~34dmin")]
    [InlineData("48,XX,i(17)(q10),+mar1,+mar2[17]/51,XX,i(17)(q10),+mar1×3,+mar2,+mar3[13]")]
    [InlineData("94,XXYY,t(8;14)(q24.1;q32)×2,+der(14)t(8;14)×2")]
    [InlineData("47,XX,+8,i(8)(q10)×2")]
    [InlineData("46,XX,del(6)(q13q23)×2")]
    [InlineData("46,XY,der(1)del(1)(p21p33)hsr(1)(p21)")]
    [InlineData("46,XX,der(1)ins(1;7)(q21;p11.2p21)hsr(1;7)(q21;p11.2)")]
    [InlineData("45,XY,dic(14;21)(p11.2;p11.2)")]
    [InlineData("46,XX,+21,rob(21;21)(q10;q10)")]
    [InlineData("45,XX,der(1;3)(p10;q10)")]
    [InlineData("44,XY,–1,der(1;3)(p10;q10)")]
    [InlineData("47,XY,–10,+fis(10)(p10),+fis(10)(q10)")]
    [InlineData("46,XX,ider(22)(q10)t(9;22)(q34;q11.2)")]
    [InlineData("47,XX,t(9;22)(q34;q11.2),+der(22)t(9;22)")]
    public void BookForms_HaveNoFirmFinding(string input) =>
        Assert.DoesNotContain(Own(input), d => !d.IsConditional && d.Severity != Severity.Info);
}
