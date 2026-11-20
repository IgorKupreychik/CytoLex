using System.Collections.Immutable;
using CytoLex.Core.Validation;
using CoreContext = CytoLex.Core.Context.SampleContext;

namespace CytoLex.Core.Tests.Rules;

/// <summary>T-032: clones, idem/sl/sdl, composite karyotypes, clone order. The KB examples are in the harness; these cover the edges.</summary>
public class CloneChecksTests
{
    private static ImmutableArray<Diagnostic> Diagnostics(string input, CoreContext? hint = null) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), ContextHint = hint })
            .Results.Single().Diagnostics;

    private static EditionResult Result(string input) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016") }).Results.Single();

    private static IEnumerable<string> Previews(Diagnostic d) => d.Fixes.Select(f => f.Preview).OfType<string>();

    [Theory]
    [InlineData("CLONE.SUBCLONE_REFERENCE", "46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]/48,idem,+9[3]", "48")]
    [InlineData("CLONE.SUBCLONE_REFERENCE", "46,XX,t(9;22)(q34;q11.2)[3]/48,sdl,+8[17]", "sdl")] // no sideline before
    [InlineData("CLONE.SUBCLONE_REFERENCE", "46,XX,t(9;22)(q34;q11.2)[3]/47,sl2,+8[17]", "sl2")] // no second stemline
    [InlineData("CLONE.SUBCLONE_REFERENCE", "46,XY,t(9;22)(q34;q11.2)[3]/93,idem×2,+8,+9[2]", "93")] // 92 + 1
    [InlineData("CLONE.SECOND_STEMLINE", "45,XY,–7[5]/46,sl,+8[6]/46,XY,t(9;22)(q34;q11.2)[3]/92,sl2×2[5]/93,sdl,+8[2]", "sdl")]
    [InlineData("CLONE.IDEM_SL_NOT_MIXED", "46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]/48,sl,+8,+9[3]", "sl")]
    [InlineData("CLONE.CELL_COUNT_REQUIRED", "46,XX,t(8;21)(q22;q22)[10]/46,XX", "46,XX")]
    [InlineData("CLONE.MIN_CELLS", "46,XX,t(8;21)(q22;q22)[12]/45,XX,–7,–9[2]", "[2]")]
    [InlineData("CP.FORMAT", "45~48,XX,–5,+8[7cp]", "[7cp]")]
    [InlineData("SEX.UNEVEN_PLOIDY_MALE", "69<3n>,XY,–Y,+8[10]", "–Y")]
    public void Rule_Fires(string ruleId, string input, string spanText)
    {
        var d = Assert.Single(Diagnostics(input), d => d.RuleId == ruleId);
        Assert.Equal(spanText, d.Span.Of(input));
        Assert.NotNull(d.Detail);
        Assert.NotEmpty(d.Citations);
    }

    /// <summary>The book's forms and their variants: no clone rule fires (infos that can't be verified aside).</summary>
    [Theory]
    [InlineData("46,XX,t(9;22)(q34;q11.2)[3]/47,sl,+8[17]/48,sdl1,+9[3]/49,sdl2,+11[12]")]
    [InlineData("46,XY,t(9;22)(q34;q11.2)[3]/47,sl,+8[10]/48,sdl,+der(22)t(9;22)[4]/47,sl,+19[3]")]
    [InlineData("26,X,+4,+6,+21[3]/52,idem×2[13]")]
    [InlineData("46,XX,t(8;21)(q22;q22)[12]/45,idem,–X[19]/46,idem,–X,+8[5]")]
    [InlineData("68<3n>,XY,–X[10]")]
    [InlineData("46,XX")] // one line without a count: never CELL_COUNT_REQUIRED
    [InlineData("46,XY,t(9;22)(q34;q11.2)[4]//46,XX[16]")] // recipient and donor sides are ordered separately
    [InlineData("46,XY[3]//46,XX,t(1;3)(p22;p14)[2]/46,XX[17]")]
    [InlineData("mos 45,X[2]/46,XX[18]")] // a constitutional mosaic: no clone rules
    public void CloneRules_DoNotFire(string input)
    {
        Assert.DoesNotContain(Diagnostics(input), d => IsCloneRule(d.RuleId) && d.Severity != Severity.Info);
        Assert.Empty(Result(input).Segments.SelectMany(s => s.Constructs));
    }

    [Fact]
    public void ReorderFix_IsWithheld_WhenItWouldChangeWhatIdemRefersTo()
    {
        // Moving the normal line to the end is safe for idem (it refers to the first line, which stays first) ...
        const string safe = "46,XX,t(9;22)(q34;q11.2)[3]/46,XX[7]/47,idem,+8[17]";
        var d = Assert.Single(Diagnostics(safe), d => d.RuleId == "CLONE.NORMAL_LAST");
        Assert.Contains("46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]/46,XX[7]", Previews(d));

        // ... but moving the first line would make idem refer to another clone: no edit, only the explanation.
        const string unsafeInput = "46,XX[7]/46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]";
        var e = Assert.Single(Diagnostics(unsafeInput), d => d.RuleId == "CLONE.NORMAL_LAST");
        Assert.Empty(Previews(e));
    }

    [Fact]
    public void NormalFirst_IsReportedPerContext()
    {
        const string input = "46,XX[7]/46,XX,t(3;9)(p13;p13)[14]";
        var all = Diagnostics(input);
        Assert.Contains(all, d => d.RuleId == "CLONE.NORMAL_LAST" && d.IsConditional);
        Assert.Contains(all, d => d.RuleId == "KARYO.CLONE_ORDER_CONSTITUTIONAL" && d.IsConditional);
        Assert.DoesNotContain(Diagnostics(input, CoreContext.Acquired), d => d.RuleId == "KARYO.CLONE_ORDER_CONSTITUTIONAL");
        Assert.DoesNotContain(Diagnostics(input, CoreContext.Constitutional), d => d.RuleId == "CLONE.NORMAL_LAST");
    }

    [Fact]
    public void JumpingTranslocations_TakeOverTheUnrelatedOrder()
    {
        const string input = "46,XX,t(1;7)(p36.3;q11.2)[4]/46,XX,t(4;7)(q35;q11.2)[6]/46,XX,t(7;9)(q11.2;p24)[3]";
        var all = Diagnostics(input);
        Assert.Contains(all, d => d.RuleId == "TRANSLOC.JUMPING");
        Assert.DoesNotContain(all, d => d.RuleId == "CLONE.ORDER_UNRELATED");
    }

    [Fact]
    public void SingleCellClone_AndComposite_AreCannotVerifyInfos()
    {
        var single = Assert.Single(Diagnostics("46,XX,t(9;22)(q34;q11.2)[1]/46,XX[19]"), d => d.RuleId == "CLONE.SINGLE_CELL_REPORTING");
        Assert.True(single.CannotVerify);
        Assert.Equal(Severity.Info, single.Severity);
        var cp = Assert.Single(Diagnostics("43~46,XX,–7,+8[cp4]"), d => d.RuleId == "CP.NOT_FOR_RANDOM_LOSS");
        Assert.True(cp.CannotVerify);
    }

    [Fact]
    public void MissingIdemRepeat_IsOneFinding()
    {
        var all = Diagnostics("46,XX,t(9;22)(q34;q11.2)[3]/47,idem,+8[17]/48,idem,+9[3]");
        Assert.Single(all, d => d.RuleId == "CLONE.SUBCLONE_REFERENCE");
        Assert.DoesNotContain(all, d => d.RuleId == "NUM.COUNT_MATCHES_CHANGES");
    }

    private static bool IsCloneRule(string id) =>
        id.StartsWith("CLONE.", StringComparison.Ordinal) || id.StartsWith("CP.", StringComparison.Ordinal)
        || id is "SEX.UNEVEN_PLOIDY_MALE" or "KARYO.CLONE_ORDER_CONSTITUTIONAL" or "TRANSLOC.JUMPING";
}
