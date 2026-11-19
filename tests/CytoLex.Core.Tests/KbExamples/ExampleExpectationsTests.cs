using System.Text.Json.Nodes;

namespace CytoLex.Core.Tests.KbExamples;

/// <summary>Tests of the harness itself (T-025), with a fake validator, so it is trustworthy before T-028.</summary>
public class ExampleExpectationsTests
{
    private static readonly SampleContext[] _both = [SampleContext.Constitutional, SampleContext.Acquired];

    private static KbRuleInfo Rule(string id = "TEST.RULE", Severity severity = Severity.Error, bool needsReview = false, params string[] context) =>
        new("2016", id, "1", "test.yaml", ["karyotype"], context, severity, needsReview, 1, "replace", 1, 1, 1);

    private static KbExampleCase Case(KbRuleInfo rule, ExampleKind kind, string text, string? corrected = null) =>
        new(rule, kind, 0, text, corrected, KbCatalog.IsFragment(text) || (corrected is not null && KbCatalog.IsFragment(corrected)));

    private static ExampleRun Clean() => new(true, _both, []);

    private static ExampleRun With(bool isValid, params ExampleDiagnostic[] diagnostics) =>
        new(isValid, isValid ? _both : [], diagnostics);

    private sealed class FakeValidator(Func<string, SampleContext?, ExampleRun> run, bool supportsFixes = false) : IExampleValidator
    {
        public bool SupportsFixes => supportsFixes;

        public ExampleRun Run(string edition, string text, SampleContext? hint, bool isFragment) => run(text, hint);
    }

    // --- skipping ---

    [Fact]
    public void NoValidator_Skips()
    {
        var outcome = ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Valid, "46,XX"), null, ruleImplemented: true);
        Assert.Equal(ExampleExpectations.NoValidator, outcome.SkipReason);
    }

    [Fact]
    public void Invalid_RuleNotImplemented_Skips()
    {
        var outcome = ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Invalid, "46XX"), new FakeValidator((_, _) => Clean()), ruleImplemented: false);
        Assert.Equal("rule TEST.RULE not implemented", outcome.SkipReason);
    }

    [Fact]
    public void Valid_RunsEvenIfRuleNotImplemented()
    {
        var outcome = ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Valid, "46,XX"), new FakeValidator((_, _) => Clean()), ruleImplemented: false);
        Assert.True(outcome.Passed);
    }

    // --- valid ---

    [Fact]
    public void Valid_WithFirmError_Fails()
    {
        var v = new FakeValidator((_, _) => With(false, new ExampleDiagnostic("OTHER.RULE", Severity.Error)));
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Valid, "46,XX"), v, true).Failures);
    }

    [Fact]
    public void Valid_WithConditionalErrorOnly_Passes()
    {
        // e.g. 47,XX,+X: an error only if constitutional; valid as acquired.
        var v = new FakeValidator((_, _) => new ExampleRun(true, [SampleContext.Acquired],
            [new ExampleDiagnostic("OTHER.RULE", Severity.Error, Conditional: true)]));
        Assert.True(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Valid, "47,XX,+X"), v, true).Passed);
    }

    [Fact]
    public void Valid_NotValidInRuleContext_Fails()
    {
        var rule = Rule(context: "neoplastic");
        var v = new FakeValidator((_, _) => new ExampleRun(true, [SampleContext.Constitutional], []));
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(rule, ExampleKind.Valid, "47,XX,+X"), v, true).Failures);
    }

    [Fact]
    public void Valid_OwnRuleFiresAsWarning_Fails()
    {
        var v = new FakeValidator((_, _) => With(true, new ExampleDiagnostic("TEST.RULE", Severity.Warning)));
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Valid, "46,XX"), v, true).Failures);
    }

    [Theory]
    [InlineData("info", true)]
    [InlineData("warning", false)]
    public void Valid_OwnRuleCannotVerifyNote_Allowed(string reportedSeverity, bool passes)
    {
        var severity = reportedSeverity == "info" ? Severity.Info : Severity.Warning;
        var rule = Rule(severity: Severity.Info);
        var v = new FakeValidator((_, _) => With(true, new ExampleDiagnostic("TEST.RULE", severity, CannotVerify: true)));
        Assert.Equal(passes, ExampleExpectations.Evaluate(Case(rule, ExampleKind.Valid, "46,XX,der(5)mat"), v, true).Passed);
    }

    // --- invalid: the string's own markers set another context (T-031) ---

    [Theory]
    [InlineData(true, true)] // rec → constitutional: silent without a hint is fine
    [InlineData(false, false)] // inferred inside the rule's context (acquired): it must fire
    public void Invalid_OwnMarkersSetOtherContext_CheckedWithHintOnly(bool inferredConstitutional, bool passes)
    {
        var inferred = inferredConstitutional ? SampleContext.Constitutional : SampleContext.Acquired;
        var rule = Rule(context: "neoplastic");
        var v = new FakeValidator((_, hint) => hint is null
            ? new ExampleRun(true, _both, [], inferred)
            : With(false, new ExampleDiagnostic("TEST.RULE", Severity.Error)));
        Assert.Equal(passes, ExampleExpectations.Evaluate(Case(rule, ExampleKind.Invalid, "46,XX,rec(6)dup(6p)"), v, true).Passed);
    }

    [Fact]
    public void ValidFragment_OnlyOwnRuleMatters()
    {
        // inv(2) is not a whole karyotype; other errors (e.g. missing count) are not held against it.
        var v = new FakeValidator((_, _) => With(false, new ExampleDiagnostic("KARYO.COUNT_SEX_ORDER", Severity.Error)));
        Assert.True(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Valid, "inv(2)"), v, true).Passed);
    }

    [Fact]
    public void Unparsed_Fails()
    {
        var v = new FakeValidator((_, _) => With(true, new ExampleDiagnostic(ExampleExpectations.Unparsed, Severity.Warning)));
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Valid, "inv(2)"), v, true).Failures);
    }

    // --- invalid ---

    [Fact]
    public void Invalid_Silent_Fails()
    {
        var v = new FakeValidator((_, _) => Clean());
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Invalid, "46XX"), v, true).Failures);
    }

    [Fact]
    public void Invalid_RuleFiresWithKbSeverity_Passes()
    {
        var v = new FakeValidator((_, _) => With(false, new ExampleDiagnostic("TEST.RULE", Severity.Error)));
        Assert.True(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Invalid, "46XX"), v, true).Passed);
    }

    [Fact]
    public void Invalid_ContextRule_SilentWithoutHint_Fails()
    {
        // T-021: an acquired-only example run without a hint must give the conditional diagnostic, not silence.
        var rule = Rule(context: "neoplastic");
        var v = new FakeValidator((_, hint) => hint == SampleContext.Acquired
            ? With(false, new ExampleDiagnostic("TEST.RULE", Severity.Error))
            : Clean());
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(rule, ExampleKind.Invalid, "47,XXX"), v, true).Failures);
    }

    [Fact]
    public void Invalid_ContextRule_ConditionalWithoutHint_FirmWithHint_Passes()
    {
        var rule = Rule(context: "neoplastic");
        var v = new FakeValidator((_, hint) => hint == SampleContext.Acquired
            ? With(false, new ExampleDiagnostic("TEST.RULE", Severity.Error))
            : With(true, new ExampleDiagnostic("TEST.RULE", Severity.Error, Conditional: true)));
        Assert.True(ExampleExpectations.Evaluate(Case(rule, ExampleKind.Invalid, "47,XXX"), v, true).Passed);
    }

    [Fact]
    public void Invalid_ContextRule_ConditionalDespiteHint_Fails()
    {
        var rule = Rule(context: "neoplastic");
        var v = new FakeValidator((_, _) => With(true, new ExampleDiagnostic("TEST.RULE", Severity.Error, Conditional: true)));
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(rule, ExampleKind.Invalid, "47,XXX"), v, true).Failures);
    }

    [Theory]
    [InlineData("error", false)]
    [InlineData("warning", true)]
    public void Invalid_NeedsReview_CappedAtWarning(string reportedSeverity, bool passes)
    {
        var reported = reportedSeverity == "error" ? Severity.Error : Severity.Warning;
        var rule = Rule(needsReview: true);
        var v = new FakeValidator((_, _) => With(reported != Severity.Error, new ExampleDiagnostic("TEST.RULE", reported)));
        Assert.Equal(passes, ExampleExpectations.Evaluate(Case(rule, ExampleKind.Invalid, "46XX"), v, true).Passed);
    }

    [Fact]
    public void Invalid_UncertainFinding_CappedAtWarning()
    {
        var v = new FakeValidator((_, _) => With(true, new ExampleDiagnostic("TEST.RULE", Severity.Warning, Uncertain: true)));
        Assert.True(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Invalid, "46XX"), v, true).Passed);
    }

    [Fact]
    public void Invalid_InfoRule_ExpectsInfo()
    {
        var rule = Rule(severity: Severity.Info);
        var v = new FakeValidator((_, _) => With(false, new ExampleDiagnostic("TEST.RULE", Severity.Error)));
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(rule, ExampleKind.Invalid, "46XX"), v, true).Failures);
    }

    // --- corrected ---

    private static FakeValidator Fixer(string preview) => new(
        (text, _) => text == "46XX"
            ? With(false, new ExampleDiagnostic("TEST.RULE", Severity.Error, FixPreviews: [preview]))
            : Clean(),
        supportsFixes: true);

    [Fact]
    public void Corrected_ExactPreview_Passes() =>
        Assert.True(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Corrected, "46XX", "46,XX"), Fixer("46,XX"), true).Passed);

    [Fact]
    public void Corrected_WrongPreview_Fails() =>
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Corrected, "46XX", "46,XX"), Fixer("46, XX"), true).Failures);

    [Fact]
    public void Corrected_WithoutFixer_ChecksValidityOnly()
    {
        var v = new FakeValidator((text, _) => text == "46XX" ? With(false, new ExampleDiagnostic("TEST.RULE", Severity.Error)) : Clean());
        Assert.True(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Corrected, "46XX", "46,XX"), v, true).Passed);
    }

    [Fact]
    public void Corrected_StillInvalid_Fails()
    {
        var v = new FakeValidator((_, _) => With(false, new ExampleDiagnostic("TEST.RULE", Severity.Error)));
        Assert.NotEmpty(ExampleExpectations.Evaluate(Case(Rule(), ExampleKind.Corrected, "46XX", "46,XX"), v, false).Failures);
    }

    // --- catalog ---

    [Fact]
    public void Catalog_HasOneCasePerExampleAndCorrectedForm()
    {
        using var stream = typeof(KbCatalog).Assembly.GetManifestResourceStream(KbCatalog.ResourceName)!;
        var root = JsonNode.Parse(stream)!;
        var expected = 0;
        foreach (var (_, edition) in root["editions"]!.AsObject())
        {
            foreach (var rule in edition!["rules"]!.AsArray())
            {
                var examples = rule!["examples"]!;
                var invalid = examples["invalid"]?.AsArray() ?? [];
                expected += examples["valid"]!.AsArray().Count + invalid.Count + invalid.Count(e => e!["corrected"] is not null);
            }
        }

        Assert.True(expected > 0);
        Assert.Equal(expected, KbCatalog.Default.Cases.Count);
        Assert.All(KbCatalog.Default.Cases, c => Assert.Same(c, KbCatalog.Default.Find(c.Key)));
    }

    [Theory]
    [InlineData("46,XY", false)]
    [InlineData("mos 45,X/46,XX", false)]
    [InlineData("chi 46,XX/46,XY", false)]
    [InlineData("//46,XX[20]", false)]
    [InlineData("arr[GRCh38] Xp22.33q28(168546_155233730)×1[0.6]", false)]
    [InlineData("XX,46", true)]
    [InlineData("inv(2)", true)]
    [InlineData("+21", true)]
    [InlineData("GTG", true)]
    [InlineData("psu dic", true)]
    [InlineData("chr(2)", true)]
    [InlineData("43~47,XX,…", true)]
    [InlineData("46XX", false)]
    [InlineData("45,Z", false)]
    [InlineData("58<2n>,XY", false)]
    [InlineData("58(2n),XY", false)]
    [InlineData("1p31", true)]
    [InlineData("1P31", true)]
    [InlineData("16qh+", true)]
    [InlineData("1q23 or q24", true)]
    public void IsFragment(string text, bool expected) => Assert.Equal(expected, KbCatalog.IsFragment(text));

    [Fact]
    public void Catalog_ContextMapping()
    {
        var rule = KbCatalog.Default.Rules.Single(r => r.Id == "SEX.ACQUIRED_GAIN_LOSS");
        Assert.Equal([SampleContext.Acquired], rule.SampleContexts);
        Assert.False(rule.NeedsReview); // cleared in T-056: conditional error if acquired
        Assert.Equal(Severity.Error, rule.Severity);
    }
}
