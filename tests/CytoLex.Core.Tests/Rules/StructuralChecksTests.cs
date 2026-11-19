using System.Collections.Immutable;
using CytoLex.Core.Validation;
using CoreContext = CytoLex.Core.Context.SampleContext;

namespace CytoLex.Core.Tests.Rules;

/// <summary>T-031: chromosome and breakpoint order, breakpoint groups, the detailed system, derivatives and recombinants.</summary>
public class StructuralChecksTests
{
    private static readonly string[] _t031Rules =
    [
        "STRUCT.CHROMOSOME_ORDER", "STRUCT.MULTI_CHROMOSOME_ORDER", "STRUCT.INSERTION_RECEPTOR_FIRST", "TAS.FORMAT", "TRC.FORMAT",
        "RING.DIC_TRC_ORDER", "RING.MONOCENTRIC_AS_DER", "STRUCT.SHORT_ARM_BREAKPOINT_FIRST", "STRUCT.PROXIMAL_BREAKPOINT_FIRST",
        "STRUCT.BREAKPOINT_GROUPS", "INV.FORMAT", "RING.FORMAT", "QDP.FORMAT", "TRP.FORMAT", "TRANSLOC.FORMAT",
        "STRUCT.DETAILED_SYMBOLS", "STRUCT.COMPOSITION_ORDER", "STRUCT.COMPOSITION_CHROMOSOME_NUMBERS", "DER.CENTROMERE_CHROMOSOME",
        "DER.ABERRATION_ORDER", "DER.ABERRATIONS_NO_COMMA", "REC.FORMAT", "REC.NOT_ACQUIRED", "DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME",
        "DER.DICENTRIC_FORMAT",
    ];

    private static ImmutableArray<Diagnostic> Diagnostics(string input, CoreContext? hint = null) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), ContextHint = hint })
            .Results.Single().Diagnostics;

    private static List<Diagnostic> Own(string input, CoreContext? hint = null) =>
        [.. Diagnostics(input, hint).Where(d => _t031Rules.Contains(d.RuleId))];

    [Theory]
    [InlineData("STRUCT.CHROMOSOME_ORDER", "46,XY,t(16;12)(p11.1;q13)", "t(16;12)")]
    [InlineData("STRUCT.CHROMOSOME_ORDER", "46,X,t(18;X)(q11.1;p11.1)", "t(18;X)")]
    [InlineData("STRUCT.CHROMOSOME_ORDER", "46,t(Y;X)(q11.2;q22)", "t(Y;X)")]
    [InlineData("STRUCT.CHROMOSOME_ORDER", "45,XX,dic(15;13)(q24;q22)", "dic(15;13)")]
    [InlineData("STRUCT.CHROMOSOME_ORDER", "45,XX,der(3;1)(q10;p10)", "der(3;1)")]
    [InlineData("STRUCT.CHROMOSOME_ORDER", "46,XX,der(1)t(3;1)(q21;p32)", "t(3;1)")] // inside a derivative
    [InlineData("STRUCT.MULTI_CHROMOSOME_ORDER", "46,XX,t(22;17;9)(q11.2;q22;q34)", "t(22;17;9)")]
    [InlineData("STRUCT.INSERTION_RECEPTOR_FIRST", "46,XY,ins(2;5)(q22q32;p14)", "ins(2;5)")]
    [InlineData("TAS.FORMAT", "46,XX,tas(13;12)(q34;q24.3)", "tas(13;12)")]
    [InlineData("TAS.FORMAT", "46,Y,tas(3;12;X)(q29;q24.3p13;q28)", "tas(3;12;X)")]
    [InlineData("TAS.FORMAT", "45,XX,tas(12;13)(q24.3;q34)", "45")] // counted as one
    [InlineData("TRC.FORMAT", "44,XX,trc(9;12;4)(q34;p13q22;q31.2)", "trc(9;12;4)")]
    [InlineData("RING.DIC_TRC_ORDER", "47,XX,+dic r(3;1)(p24q26.2;p36.1q32)", "dic r(3;1)")]
    [InlineData("RING.DIC_TRC_ORDER", "47,XX,+dicr(1;3)(p36.1q32;p24q26.2)", "dicr")]
    [InlineData("RING.MONOCENTRIC_AS_DER", "46,XX,r(1;3)(p36.1q23;q21q27)", "r(1;3)")]
    [InlineData("RING.MONOCENTRIC_AS_DER", "46,XX,der(1)r(3;1)(q21q27;p36.1q23)", "r(3;1)")]
    [InlineData("STRUCT.SHORT_ARM_BREAKPOINT_FIRST", "46,XX,inv(2)(q31p21)", "q31p21")]
    [InlineData("STRUCT.PROXIMAL_BREAKPOINT_FIRST", "46,XX,inv(2)(p23p13)", "p23p13")]
    [InlineData("STRUCT.PROXIMAL_BREAKPOINT_FIRST", "46,XX,del(5)(q33q13)", "q33q13")]
    [InlineData("STRUCT.PROXIMAL_BREAKPOINT_FIRST", "46,XX,r(7)(q36q22)", "q36q22")]
    [InlineData("STRUCT.PROXIMAL_BREAKPOINT_FIRST", "46,XX,del(1)(q21.3q21.1)", "q21.3q21.1")] // sub-bands
    [InlineData("STRUCT.BREAKPOINT_GROUPS", "46,XX,ins(5;2)(p14q22q32)", "p14q22q32")]
    [InlineData("STRUCT.BREAKPOINT_GROUPS", "46,XX,ins(5;2)(p14;q22;q32)", "p14;q22;q32")]
    [InlineData("STRUCT.BREAKPOINT_GROUPS", "46,XX,del(5)(q13;q33)", "q13;q33")] // del: no group rule of its own (yet)
    [InlineData("INV.FORMAT", "46,XX,inv(3)(q21;q26.2)", "q21;q26.2")]
    [InlineData("RING.FORMAT", "46,XX,r(7)(p22;q36)", "p22;q36")]
    [InlineData("QDP.FORMAT", "46,XX,qdp(1)(q23;q32)", "q23;q32")]
    [InlineData("TRP.FORMAT", "46,XX,trp(1)(q21;q32)", "q21;q32")]
    [InlineData("TRANSLOC.FORMAT", "46,XY,t(2;5)(q21)", "q21")]
    [InlineData("TRANSLOC.FORMAT", "46,XY,t(5;6)(q13;q23;q15;q23)", "q13;q23;q15;q23")]
    [InlineData("STRUCT.DETAILED_SYMBOLS", "46,XX,del(5)(ter→q13:)", "ter")]
    [InlineData("STRUCT.COMPOSITION_ORDER", "46,XX,der(1)(1qter→1p22::3q13.1→3qter)", "1qter→1p22")]
    [InlineData("STRUCT.COMPOSITION_CHROMOSOME_NUMBERS", "46,XX,der(6)(6pter→6q25.2::6p22.2→6pter)", "6pter")]
    [InlineData("STRUCT.COMPOSITION_CHROMOSOME_NUMBERS", "46,XX,der(1)(3qter→q13.1::1p22→1qter)", "q13.1")]
    [InlineData("DER.CENTROMERE_CHROMOSOME", "46,XX,der(5)(2pter→2q21::5q31→5qter)", "5")]
    [InlineData("DER.ABERRATION_ORDER", "46,XX,der(1)t(1;11)(q25;q13)t(1;3)(p32;q21)", "t(1;3)(p32;q21)")]
    [InlineData("DER.ABERRATIONS_NO_COMMA", "46,XX,der(1)t(1;3)(p32;q21),t(1;11)(q25;q13)", "t(1;11)(q25;q13)")]
    [InlineData("REC.FORMAT", "46,XX,rec(2),dup(2p),inv(2)(p21q31)mat", "dup(2p)")]
    [InlineData("DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME", "46,XX,add(5)(p15.3)add(5)(q23)", "add(5)(q23)")]
    [InlineData("DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME", "46,XY,del(9)(p12)del(9)(q31)", "del(9)(q31)")]
    [InlineData("DER.DICENTRIC_FORMAT", "45,XX,dic(5;7)(q22;p13)t(3;7)(q21;q21)", "dic(5;7)(q22;p13)")]
    public void Rule_Fires(string ruleId, string input, string spanText)
    {
        var d = Assert.Single(Diagnostics(input), d => d.RuleId == ruleId);
        Assert.Equal(spanText, d.Span.Of(input));
        Assert.Equal(DiagnosticOrigin.Rule, d.Origin);
        Assert.NotNull(d.Detail);
        Assert.NotEmpty(d.Citations);
    }

    /// <summary>Forms the book prints as correct, and the exceptions: none of the T-031 rules fires.</summary>
    [Theory]
    [InlineData("46,XY,t(12;16)(q13;p11.1)")]
    [InlineData("46,t(X;Y)(q22;q11.2)")]
    [InlineData("46,XX,t(9;22;17)(q34;q11.2;q22)")]
    [InlineData("46,XX,t(2;7;5)(p21;q22;q23)")] // after the first, the exchange sets the order
    [InlineData("45,XX,psu dic(15;13)(q12;q12)")] // psu dic: DIC.PSEUDODICENTRIC_ORDER
    [InlineData("46,XX,der(?)t(?;6)(?;q16)")]
    [InlineData("46,X,ins(5;X)(p14;q21q25)")] // receiver first, even before X
    [InlineData("46,XY,ins(5;2)(p14;q32q22)")] // inserted segment: band order shows orientation
    [InlineData("46,XY,dup(1)(q25q22)")]
    [InlineData("46,XX,trp(1)(q32q21)")]
    [InlineData("46,XX,t(5;14;9)(q13q23;q24q21;p12p23)")]
    [InlineData("46,XX,der(1)r(1;3)(p36.1q23;q27q21)")]
    [InlineData("47,XY,+mos der(8)r(1;8;17)(p36.3p35;p12q13;q25q25)")] // §4.4: lowest-first ring inside der(8)
    [InlineData("46,X,tas(1;X;12;7)(p36.3;q28p22.3;p13q24.3;p22)")]
    [InlineData("44,XX,trc(4;12;9)(q31.2;q22p13;q34)")]
    [InlineData("47,XX,+trc r(1;3;12)(p36.1q32;q26.3p24;p12q23)")]
    [InlineData("46,XX,del(1)(q21q21.1)")] // nested bands cannot be ordered
    [InlineData("46,XY,inv(3)(pter→p13::q21→p13::q21→qter)")] // pericentric inversion: reversed centromere piece
    [InlineData("46,XX,tas(12;13)(12pter→12qter→13qter→13pter)")]
    [InlineData("45,XX,dic(13;13)(13pter→13q14::13q32→13pter)")] // homologues, with numbers
    [InlineData("47,XY,+dic(15;15)(pter→q12::q12→pter)")] // homologues, without
    [InlineData("46,XX,der(5)ins(5;?)(pter→q13::?::q13→qter)")]
    [InlineData("46,XX,der(1)(::1p36.1→1q23::?::)")]
    [InlineData("46,XX,der(8)t(8;17)(p23;q21)inv(8)(p22q13)t(8;22)(q22;q12)")]
    [InlineData("46,XX,der(1)t(1;3)(p32;q21)t(1;11)(q25;q13)")]
    [InlineData("46,XY,der(1)t(1;3)(p32;q21)t(3;7)(q28;q11.2)")] // t(3;7) has no band on chromosome 1
    [InlineData("46,XX,rec(2)dup(2p)inv(2)(p21q31)mat")]
    [InlineData("45,XX,der(5;7)t(5;7)(q22;p13)t(3;7)(q21;q21)")]
    [InlineData("46,XX,t(1;3)(p32;q21),t(1;11)(q25;q13)")] // no derivative: two abnormalities
    public void BookForms_HaveNoFinding(string input) =>
        Assert.Empty(Own(input));

    [Fact]
    public void GroupCountError_IsReportedOnce_ByTheSymbolRule()
    {
        var own = Own("46,XY,t(5;6)(q13;q23;q15;q23)");
        Assert.Equal("TRANSLOC.FORMAT", Assert.Single(own).RuleId);
    }

    [Fact]
    public void DicrJoined_IsNotAlsoASpacingFinding()
    {
        var all = Diagnostics("47,XX,+dicr(1;3)(p36.1q32;p24q26.2)");
        Assert.Contains(all, d => d.RuleId == "RING.DIC_TRC_ORDER");
        Assert.DoesNotContain(all, d => d.RuleId == "SYM.SPACE_BETWEEN_SYMBOLS");
    }

    [Fact]
    public void TasCountedOnce_IsNotACountMismatch()
    {
        var all = Diagnostics("45,XX,tas(12;13)(q24.3;q34)");
        Assert.Contains(all, d => d.RuleId == "TAS.FORMAT");
        Assert.DoesNotContain(all, d => d.RuleId == "NUM.COUNT_MATCHES_CHANGES");
    }

    [Fact]
    public void CommaAfterDerivativeWithAberrations_IsUncertain()
    {
        var d = Assert.Single(Own("46,XX,der(1)t(1;3)(p32;q21),t(1;11)(q25;q13)"));
        Assert.Equal(Certainty.Uncertain, d.Certainty);
        Assert.Equal(Severity.Warning, d.Severity);
    }

    [Fact]
    public void RecNotAcquired_FiresWithANeoplasticHint_AndNotOnItsOwnMarker()
    {
        const string input = "46,XX,rec(6)dup(6p)inv(6)(p22.2q25.2)";
        Assert.DoesNotContain(Diagnostics(input), d => d.RuleId == "REC.NOT_ACQUIRED"); // rec → constitutional (§3.3)
        var d = Assert.Single(Diagnostics(input, CoreContext.Acquired), d => d.RuleId == "REC.NOT_ACQUIRED");
        Assert.False(d.IsConditional);
        Assert.Equal(Severity.Error, d.Severity);
    }
}
