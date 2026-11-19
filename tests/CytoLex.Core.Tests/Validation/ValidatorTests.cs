using System.Text;
using System.Text.Json;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Rules;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Syntax;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>T-028: the rule engine, edition selection, severity resolution and the result API (docs/design/core.md §7–§9).</summary>
public class ValidatorTests
{
    private static Validator V => Validator.Default;

    private static ValidationReport Validate(string input, SampleContext? hint = null, EditionSelection? editions = null) =>
        V.Validate(input, new ValidationOptions { ContextHint = hint, Editions = editions ?? EditionSelection.One("2016") });

    /// <summary>A validator with one fake check bound to <paramref name="ruleId"/>, reporting the whole segment.</summary>
    private static Validator WithCheck(string ruleId, Func<RuleContext, bool>? when = null, Certainty certainty = Certainty.Certain, bool cannotVerify = false) =>
        new(KnowledgeBase.LoadEmbedded(), new RuleCatalog([(new KbRuleAttribute(ruleId), new FakeCheck(when, certainty, cannotVerify))]));

    [Fact]
    public void SingleEdition_ValidString()
    {
        var report = Validate("46,XX");

        var result = Assert.Single(report.Results);
        Assert.Equal("2016", result.Edition);
        Assert.Equal(EditionStatus.Implemented, result.Status);
        Assert.True(result.Validity.IsValid);
        Assert.True(result.Validity.IsComplete);
        Assert.Equal([SampleContext.Constitutional, SampleContext.Acquired], result.Validity.ValidContexts);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(SeverityCounts.None, result.Counts);
    }

    [Fact]
    public void DefaultSelection_IsNewestImplementedEdition()
    {
        var result = Assert.Single(V.Validate("46,XX").Results);
        Assert.Equal("2016", result.Edition);
        Assert.Equal("2016", V.DefaultEdition);
    }

    [Fact]
    public void AllEditions_2016ImplementedOthersNot()
    {
        const string input = "46,XX.ish 5p15.2(D5S23×2)";
        var report = Validate(input, editions: EditionSelection.All);

        Assert.Equal(["2016", "2020", "2024"], report.Results.Select(r => r.Edition));
        var r2016 = report.Results[0];
        Assert.Equal(EditionStatus.Implemented, r2016.Status);
        Assert.True(r2016.Validity.IsValid);
        Assert.False(r2016.Validity.IsComplete);
        Assert.Equal([EditionStatus.Implemented, EditionStatus.NotImplemented], r2016.Segments.Select(s => s.Status));
        Assert.Equal("ish: not implemented in the pilot", r2016.Segments[1].Reason);

        foreach (var r in report.Results.Skip(1))
        {
            Assert.Equal(EditionStatus.NotImplemented, r.Status);
            Assert.Equal($"ISCN {r.Edition}: not implemented", r.Reason);
            Assert.Empty(r.Diagnostics);
            Assert.False(r.Validity.IsComplete);
        }
    }

    [Fact]
    public void UnknownEdition_IsNotImplemented()
    {
        var result = Assert.Single(Validate("46,XX", editions: EditionSelection.One("1995")).Results);
        Assert.Equal(EditionStatus.NotImplemented, result.Status);
        Assert.Equal("ISCN 1995: unknown edition", result.Reason);
    }

    [Fact]
    public void ParserError_CarriesKbSeverityAndCitations()
    {
        var result = Validate("46XX").Results[0];

        var d = Assert.Single(result.Diagnostics);
        Assert.Equal("KARYO.COUNT_SEX_ORDER", d.RuleId);
        Assert.Equal(Severity.Error, d.Severity);
        Assert.Equal(DiagnosticOrigin.Parser, d.Origin);
        Assert.Equal(new Text.TextSpan(2, 0), d.Span);
        Assert.NotEmpty(d.Citations);
        Assert.All(d.Citations, c => Assert.StartsWith("ISCN 2016 §", c.Rendered, StringComparison.Ordinal));
        Assert.Equal(KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Rule("KARYO.COUNT_SEX_ORDER")!.Title, d.Message);
        Assert.False(result.Validity.IsValid);
        Assert.Empty(result.Validity.ValidContexts);
        Assert.Equal(1, result.Counts.Errors);
    }

    [Fact]
    public void BothCellLines_AreParsed()
    {
        const string input = "46,XX,t(9;22)(q34;q11.2),del(11)(p11.2p15)[19]/46,XX[1]";
        var result = Validate(input).Results[0];

        Assert.Empty(result.Diagnostics);
        Assert.Equal([SampleContext.Constitutional, SampleContext.Acquired], result.Validity.ValidContexts);
        Assert.Null(Validate(input).Context.Effective);
    }

    [Fact]
    public void NeedsReviewRule_IsCappedAtWarning()
    {
        // SYM.LISTED_ONLY is an error rule with needs_review.
        var result = Validate("46,XX,dl(5)(q13q33)").Results[0];

        var d = Assert.Single(result.Diagnostics, x => x.RuleId == "SYM.LISTED_ONLY");
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.True(d.BookAmbiguous);
        Assert.NotNull(d.ReviewNote);
    }

    [Fact]
    public void UncertainFinding_IsCappedAtWarning()
    {
        var v = WithCheck("CHROM.IDENTIFIERS", certainty: Certainty.Uncertain);
        var d = Assert.Single(v.Validate("46,XX").Results[0].Diagnostics);

        Assert.Equal(DiagnosticOrigin.Rule, d.Origin);
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.Equal(Certainty.Uncertain, d.Certainty);
        Assert.False(d.BookAmbiguous);
    }

    [Fact]
    public void CannotVerifyFinding_IsInfo()
    {
        var v = WithCheck("CHROM.IDENTIFIERS", cannotVerify: true);
        var d = Assert.Single(v.Validate("46,XX").Results[0].Diagnostics);

        Assert.Equal(Severity.Info, d.Severity);
        Assert.True(d.CannotVerify);
    }

    [Theory]
    [InlineData(null, true, true)]                         // unknown → conditional, valid as constitutional only
    [InlineData(SampleContext.Acquired, false, true)]      // firm error
    [InlineData(SampleContext.Constitutional, false, false)] // dropped
    public void ContextLimitedRule_ConditionalFirmOrDropped(SampleContext? hint, bool conditional, bool fires)
    {
        // REC.NOT_ACQUIRED: error, context neoplastic (= acquired).
        var v = WithCheck("REC.NOT_ACQUIRED");
        var result = v.Validate("47,XXX", new ValidationOptions { ContextHint = hint }).Results[0];

        if (!fires)
        {
            Assert.Empty(result.Diagnostics);
            Assert.Equal([SampleContext.Constitutional], result.Validity.ValidContexts);
            return;
        }

        var d = Assert.Single(result.Diagnostics);
        Assert.Equal(Severity.Error, d.Severity);
        Assert.Equal(conditional, d.IsConditional);
        if (conditional)
        {
            Assert.Equal(Severity.Error, d.Condition!.In(SampleContext.Acquired));
            Assert.Null(d.Condition.In(SampleContext.Constitutional));
            Assert.True(result.Validity.IsValid);
            Assert.Equal([SampleContext.Constitutional], result.Validity.ValidContexts);
            Assert.Equal(1, result.Counts.Conditional);
        }
        else
        {
            Assert.False(result.Validity.IsValid);
            Assert.Empty(result.Validity.ValidContexts);
        }
    }

    [Fact]
    public void Check_RunsOnlyOnSegmentsItsRuleAppliesTo()
    {
        // CHROM.IDENTIFIERS applies to general; the ish segment is not implemented, so the check sees one segment.
        var v = WithCheck("CHROM.IDENTIFIERS");
        Assert.Single(v.Validate("46,XX.ish 5p15.2(D5S23×2)").Results[0].Diagnostics);
    }

    [Fact]
    public void ContradictingMarkers_GiveContextConflictInfo()
    {
        const string input = "46,XX,rec(6)dup(6p)inv(6)(p22q25)[10]/47,idem,+8[5]";
        var result = Validate(input).Results[0];

        var d = Assert.Single(result.Diagnostics, x => x.RuleId == CoreCodes.ContextConflict);
        Assert.Equal(Severity.Info, d.Severity);
        Assert.Equal(DiagnosticOrigin.Core, d.Origin);
        Assert.Empty(d.Citations);
        Assert.Equal("rec", d.Span.Of(input));
        Assert.Equal(["idem"], d.SecondarySpans.Select(s => s.Of(input)));
    }

    [Fact]
    public void NotImplementedConstruct_MakesResultIncomplete()
    {
        var result = Validate("47,XX,+8[10]/48,idem,+9[5]").Results[0];

        Assert.False(result.Validity.IsComplete);
        Assert.Contains(result.Segments[0].Constructs, c => c.Kind == ConstructKind.CloneReference);
    }

    [Fact]
    public void Diagnostics_AreSortedBySpanThenSeverity()
    {
        var result = Validate("mos 45,X[15],47,XXX[10],46,XX[23]").Results[0];
        var starts = result.Diagnostics.Select(d => d.Span.Start).ToList();
        Assert.Equal(starts.Order(), starts);
        Assert.Equal(2, result.Diagnostics.Count(d => d.RuleId == "KARYO.CLONE_SEPARATOR"));
    }

    [Fact]
    public void Fragment_UsesFragmentEntry()
    {
        var asFragment = V.Validate("inv(2)(p21q31)", new ValidationOptions { IsFragment = true }).Results[0];
        var asWhole = V.Validate("inv(2)(p21q31)").Results[0];

        Assert.Empty(asFragment.Diagnostics);
        Assert.NotEmpty(asWhole.Diagnostics);
    }

    [Fact]
    public void Report_SerialisesToJson()
    {
        var report = Validate("46XX.ish 5p15.2(D5S23×2)", editions: EditionSelection.All);
        var json = JsonSerializer.Serialize(report, Validator.JsonOptions);

        Assert.Contains("\"RuleId\":\"KARYO.COUNT_SEX_ORDER\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Status\":\"NotImplemented\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Rendered\":\"ISCN 2016 \\u00A7", json, StringComparison.Ordinal);

        var back = JsonSerializer.Deserialize<ValidationReport>(json, Validator.JsonOptions)!;
        Assert.Equal(json, JsonSerializer.Serialize(back, Validator.JsonOptions));
    }

    [Fact]
    public void Validator_IsThreadSafe()
    {
        var inputs = Enumerable.Range(0, 200).Select(i => i % 2 == 0 ? "46XX" : "46,XX,dl(5)(q13q33)").ToList();
        var counts = inputs.AsParallel().Select(s => Validate(s).Results[0].Diagnostics.Length).ToList();
        Assert.All(counts, c => Assert.Equal(1, c));
    }

    [Fact]
    public void StartupCheck_RejectsUnknownRuleId()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WithCheck("TEST.NOT_IN_KB"));
        Assert.Contains("TEST.NOT_IN_KB", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupCheck_RejectsEquallySpecificVariants()
    {
        var catalog = new RuleCatalog(
        [
            (new KbRuleAttribute("CHROM.IDENTIFIERS") { Editions = "2016" }, new FakeCheck(null, Certainty.Certain, false)),
            (new KbRuleAttribute("CHROM.IDENTIFIERS") { Editions = "2016,2020" }, new OtherFakeCheck()),
        ]);
        Assert.Throws<InvalidOperationException>(() => new Validator(KnowledgeBase.LoadEmbedded(), catalog));
    }

    [Fact]
    public void MostSpecificVariant_Wins()
    {
        var specific = new OtherFakeCheck();
        var catalog = new RuleCatalog(
        [
            (new KbRuleAttribute("CHROM.IDENTIFIERS"), new FakeCheck(null, Certainty.Certain, false)),
            (new KbRuleAttribute("CHROM.IDENTIFIERS") { Editions = "2016" }, specific),
        ]);
        var check = Assert.Single(catalog.ChecksFor(KnowledgeBase.LoadEmbedded().ForEdition("2016")!));
        Assert.Same(specific, check.Check);
    }

    [Fact]
    public void Coverage_ListsParserRules()
    {
        var coverage = V.Coverage("2016")!;
        Assert.True(coverage.IsParserReported("KARYO.COUNT_SEX_ORDER"));
        Assert.True(coverage.IsChecked("BAND.MUST_EXIST"));
        Assert.False(coverage.IsChecked("KARYO.COUNT_SEX_ORDER"));
        Assert.Null(V.Coverage("2020"));
    }

    [Fact]
    public void Segments_AndContext_AreReported()
    {
        var report = Validate("47,XX,+21c.ish 21q22(RUNX1×3)");
        Assert.Equal([NotationType.Karyotype, NotationType.Ish], report.Segments.Select(s => s.Type));
        Assert.Equal(SampleContext.Acquired, report.Context.Inferred);
    }

    private sealed class FakeCheck(Func<RuleContext, bool>? when, Certainty certainty, bool cannotVerify) : IRuleCheck
    {
        public void Check(RuleContext context)
        {
            if (when?.Invoke(context) ?? true)
            {
                context.Report(context.Segment.Span, "fake", certainty: certainty, cannotVerify: cannotVerify);
            }
        }
    }

    private sealed class OtherFakeCheck : IRuleCheck
    {
        public void Check(RuleContext context)
        {
        }
    }
}

/// <summary>T-028: the runtime KB loader (docs/design/core.md §6).</summary>
public class KnowledgeBaseTests
{
    [Fact]
    public void Embedded_HasEdition2016WithRulesVocabAndBands()
    {
        var kb = KnowledgeBase.LoadEmbedded();
        Assert.Contains("2016", kb.Editions);
        Assert.Null(kb.ForEdition("2020"));

        var ed = kb.ForEdition("2016")!;
        using var doc = JsonDocument.Parse(EmbeddedBundle.Open());
        Assert.Equal(doc.RootElement.GetProperty("editions").GetProperty("2016").GetProperty("rules").GetArrayLength(), ed.Rules.Count);
        Assert.NotNull(ed.Vocab.Find("del"));
        Assert.True(ed.Bands!.Contains("5q13"));
        Assert.Equal(new GenomicRange(66700001, 76900000), ed.Bands.Find("5q13")!.Grch37);
    }

    [Fact]
    public void Rules_CarryMetadata()
    {
        var ed = KnowledgeBase.LoadEmbedded().ForEdition("2016")!;

        var rec = ed.Rule("REC.NOT_ACQUIRED")!;
        Assert.Equal([SampleContext.Acquired], rec.Contexts);
        Assert.Equal(Severity.Error, rec.Severity);
        Assert.True(rec.AppliesToType(NotationType.Karyotype));
        Assert.False(rec.AppliesToType(NotationType.Arr));

        var chrom = ed.Rule("CHROM.IDENTIFIERS")!;
        Assert.True(chrom.AppliesToType(NotationType.Arr)); // general
        Assert.Empty(chrom.Contexts);
        Assert.All(ed.Rules, r => Assert.NotEmpty(r.Citations));
        Assert.Contains(ed.Rules, r => r.Citations.Any(c => c.Anchor == "Table 2"));
    }

    [Fact]
    public void Load_RejectsUnsupportedFormatVersion()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"format_version": 99, "editions": {}}"""));
        Assert.Throws<InvalidDataException>(() => KnowledgeBase.Load(stream));
    }
}
