using System.Text.Json;
using CytoLex.Core.Context;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>T-035: level-1 fixes, level-3 explanations, <see cref="Fixer"/> (docs/design/core.md §10). Every KB <c>corrected</c> form is covered by the KB-example harness.</summary>
public class FixTests
{
    private static EditionResult Validate(string input, SampleContext? hint = null) =>
        Validator.Default.Validate(input, new ValidationOptions { ContextHint = hint, Editions = EditionSelection.One("2016") }).Results[0];

    private static Diagnostic Only(EditionResult result, string ruleId) => Assert.Single(result.Diagnostics, d => d.RuleId == ruleId);

    // --- Fixer.Apply ---

    [Fact]
    public void Apply_InsertDeleteReplace()
    {
        TextEdit[] edits =
        [
            new(new TextSpan(2, 0), ","),           // insert
            new(new TextSpan(8, 1), string.Empty),  // delete the space in "del (5)"
            new(new TextSpan(10, 1), "4"),          // replace 5 → 4
        ];
        Assert.Equal("46,XX,del(4)", Fixer.Apply("46XX,del (5)", edits));
    }

    [Fact]
    public void Apply_OverlappingEdits_Throw() =>
        Assert.Throws<ArgumentException>(() => Fixer.Apply("46,XX", [new TextEdit(new TextSpan(0, 3), "47,"), new TextEdit(new TextSpan(2, 2), "XY")]));

    [Fact]
    public void Apply_EditOutsideInput_Throws() =>
        Assert.Throws<ArgumentException>(() => Fixer.Apply("46,XX", [new TextEdit(new TextSpan(4, 3), "Y")]));

    // --- level 1 ---

    [Fact]
    public void Level1Fix_TakesLevelKindAndHintFromKb()
    {
        const string input = "46,XX,del (5)(q13q33)";
        var d = Only(Validate(input), "SYM.NO_SPACE_AT_PARENTHESIS");

        var fix = Assert.Single(d.Fixes);
        Assert.Equal(FixLevel.Deterministic, fix.Level);
        Assert.Equal(FixKind.Delete, fix.Kind);
        Assert.Equal("Remove the space next to the parenthesis.", fix.Description);
        Assert.Equal([new TextEdit(new TextSpan(9, 1), string.Empty)], fix.Edits);
        Assert.Equal("46,XX,del(5)(q13q33)", fix.Preview);
        Assert.Null(fix.ForContext);
        Assert.Equal(d.Citations, fix.Citations);
        Assert.True(fix.IsEdit);
    }

    [Fact]
    public void ParserMissingToken_GivesInsertFix()
    {
        var d = Only(Validate("46XX"), "KARYO.COUNT_SEX_ORDER");

        var fix = Assert.Single(d.Fixes);
        Assert.Equal(FixKind.Insert, fix.Kind);
        Assert.Equal([new TextEdit(new TextSpan(2, 0), ",")], fix.Edits);
        Assert.Equal("46,XX", fix.Preview);
    }

    [Fact]
    public void ConditionalDiagnostic_FixIsForTheRuleContext()
    {
        var d = Only(Validate("47,XXX"), "SEX.ACQUIRED_GAIN_LOSS");

        Assert.True(d.IsConditional);
        var fix = Assert.Single(d.Fixes);
        Assert.Equal(SampleContext.Acquired, fix.ForContext);
        Assert.Equal("47,XX,+X", fix.Preview);
    }

    [Fact]
    public void AmbiguousGain_HasNoEdit()
    {
        // 47,XXY acquired: XY,+X or XX,+Y; no single fix.
        var d = Only(Validate("47,XXY", SampleContext.Acquired), "SEX.ACQUIRED_GAIN_LOSS");
        Assert.Equal(FixLevel.Explanation, Assert.Single(d.Fixes).Level);
    }

    [Fact]
    public void TypedMultiplySign_IsKept()
    {
        var d = Only(Validate("47,XX,del(6)(q13q23)x3"), "KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY");
        Assert.Equal("47,XX,del(6)(q13q23)x2,+del(6)(q13q23)", Assert.Single(d.Fixes).Preview);
    }

    [Fact]
    public void SeveralFaultsOfOneRule_AlsoOfferFixAll()
    {
        const string input = "mos 45,X(15)/47,XXX(10)/46,XX(23)";
        var diagnostics = Validate(input).Diagnostics.Where(d => d.RuleId == "KARYO.CLONE_SEPARATOR").ToList();

        Assert.Equal(3, diagnostics.Count);
        foreach (var d in diagnostics)
        {
            Assert.Equal(2, d.Fixes.Length);
            Assert.Equal("mos 45,X[15]/47,XXX[10]/46,XX[23]", d.Fixes[1].Preview);
            Assert.EndsWith("(all 3 in this string)", d.Fixes[1].Description, StringComparison.Ordinal);
        }

        Assert.Equal(3, diagnostics.Select(d => d.Fixes[0].Preview).Distinct().Count());
    }

    // --- level 3 ---

    [Theory]
    [InlineData("48,XX,+21", "NUM.COUNT_MATCHES_CHANGES")]   // KB level 3
    [InlineData("46,XX,9q35", "BAND.MUST_EXIST")]           // KB level 3
    [InlineData("46,XX,del(5q)", "DEL.FORMAT")]              // level 1 in the KB, but the breakpoints are unknown
    public void NoSingleFix_GivesExplanationWithCitations(string input, string ruleId)
    {
        var d = Only(Validate(input), ruleId);

        // "Did you mean" options (T-041: 9q35 → 9q34) come before the explanation, which is always last.
        var fix = d.Fixes[^1];
        Assert.All(d.Fixes.Take(d.Fixes.Length - 1), f => Assert.True(f.IsSuggestion));
        Assert.Equal(FixLevel.Explanation, fix.Level);
        Assert.Null(fix.Kind);
        Assert.Empty(fix.Edits);
        Assert.Null(fix.Preview);
        Assert.False(fix.IsEdit);
        Assert.NotEmpty(fix.Citations);
        Assert.Equal(d.Citations, fix.Citations);
        Assert.Contains(fix.Citations[0].Rendered, fix.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreCodes_HaveNoFixes()
    {
        var d = Only(Validate("46,XX,rec(6)dup(6p)inv(6)(p22q25)[10]/47,idem,+8[5]"), CoreCodes.ContextConflict);
        Assert.Empty(d.Fixes);
    }

    // --- fix all ---

    [Fact]
    public void ApplyAll_AppliesDisjointFixes_ThenRevalidatesClean()
    {
        const string input = "46XX,del (5)(q13q33)";
        var all = Fixer.ApplyAll(input, Validate(input));

        Assert.Equal("46,XX,del(5)(q13q33)", all.Text);
        Assert.Equal(2, all.Applied.Length);
        Assert.Empty(all.Skipped);
        Assert.True(Validate(all.Text).Validity.IsValid);
    }

    [Fact]
    public void ApplyAll_LeavesOverlappingFixesForTheNextRound()
    {
        var result = Validate("46,XX");
        Diagnostic Fake(int start, int length, string text) =>
            new("SYM.NO_SPACE_AT_PARENTHESIS", Severity.Error, new TextSpan(start, length), [], "m", null, [], DiagnosticOrigin.Rule, false, null, Certainty.Certain, null, false,
                [new Fix(FixLevel.Deterministic, FixKind.Replace, [new TextEdit(new TextSpan(start, length), text)], "d", null, null, [])]);
        result = result with { Diagnostics = [Fake(0, 2, "47"), Fake(1, 3, "6,X"), Fake(3, 2, "XY")] };

        var all = Fixer.ApplyAll("46,XX", result);

        Assert.Equal("47,XY", all.Text);
        Assert.Equal(2, all.Applied.Length);
        Assert.Single(all.Skipped);
    }

    [Fact]
    public void ApplyAll_ConditionalFixesOnlyForTheirContext()
    {
        var result = Validate("47,XXX");

        Assert.Equal("47,XXX", Fixer.ApplyAll("47,XXX", result).Text);
        Assert.Equal("47,XX,+X", Fixer.ApplyAll("47,XXX", result, SampleContext.Acquired).Text);
        Assert.Equal("47,XXX", Fixer.ApplyAll("47,XXX", result, SampleContext.Constitutional).Text);
    }

    // --- diff (T-040) ---

    private static string Diff(string input, params TextEdit[] edits) =>
        string.Concat(FixDiff.Build(input, edits).Select(p => p.Kind switch
        {
            DiffKind.Removed => $"[-{p.Text}]",
            DiffKind.Added => $"[+{p.Text}]",
            _ => p.Text,
        }));

    [Fact]
    public void Diff_InsertDeleteReplace()
    {
        Assert.Equal("46[+,]XX", Diff("46XX", new TextEdit(new TextSpan(2, 0), ",")));
        Assert.Equal("46,XX,del[- ](5)", Diff("46,XX,del (5)", new TextEdit(new TextSpan(9, 1), string.Empty)));
        Assert.Equal("46,X[-X][+Y]", Diff("46,XX", new TextEdit(new TextSpan(3, 2), "XY")));
    }

    [Fact]
    public void Diff_TrimsTheSharedPrefixAndSuffix()
    {
        // A reorder replaces the whole span; the diff marks only the part between the shared prefix and suffix.
        Assert.Equal("t([-5;2][+2;5])", Diff("t(5;2)", new TextEdit(new TextSpan(0, 6), "t(2;5)")));
        Assert.Equal("ab[+c]", Diff("ab", new TextEdit(new TextSpan(0, 2), "abc")));
        Assert.Equal("a[-a]", Diff("aa", new TextEdit(new TextSpan(0, 2), "a")));
    }

    [Fact]
    public void Diff_MergesAdjacentRunsAndKeepsBothSides()
    {
        const string input = "46XX,del (5)";
        TextEdit[] edits = [new(new TextSpan(2, 0), ","), new(new TextSpan(8, 1), string.Empty), new(new TextSpan(10, 1), "4")];
        var parts = FixDiff.Build(input, edits);

        Assert.Equal(input, string.Concat(parts.Where(p => p.Kind != DiffKind.Added).Select(p => p.Text)));
        Assert.Equal(Fixer.Apply(input, edits), string.Concat(parts.Where(p => p.Kind != DiffKind.Removed).Select(p => p.Text)));
        Assert.All(parts.Zip(parts.Skip(1)), pair => Assert.NotEqual(pair.First.Kind, pair.Second.Kind));
        Assert.All(parts, p => Assert.NotEmpty(p.Text));
    }

    [Fact]
    public void Diff_OverlappingOrOutsideEdits_Throw()
    {
        Assert.Throws<ArgumentException>(() => FixDiff.Build("46,XX", [new TextEdit(new TextSpan(0, 3), "47,"), new TextEdit(new TextSpan(2, 2), "XY")]));
        Assert.Throws<ArgumentException>(() => FixDiff.Build("46,XX", [new TextEdit(new TextSpan(4, 3), "Y")]));
    }

    [Fact]
    public void Diff_OfAFix_MatchesItsPreview()
    {
        const string input = "46,XY,t(5;2)(q13;p11)";
        var fix = Validate(input).Diagnostics.SelectMany(d => d.Fixes).First(f => f.IsEdit);
        var parts = FixDiff.Build(input, fix);
        Assert.Equal(fix.Preview, string.Concat(parts.Where(p => p.Kind != DiffKind.Removed).Select(p => p.Text)));
        Assert.Contains(parts, p => p.Kind == DiffKind.Removed);
    }

    // --- serialisation ---

    [Fact]
    public void Fixes_SerialiseToJson()
    {
        var report = Validator.Default.Validate("46XX,del (5)(q13q33)");
        var json = JsonSerializer.Serialize(report, Validator.JsonOptions);

        Assert.Contains("\"Preview\":\"46,XX,del (5)(q13q33)\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Level\":\"Deterministic\"", json, StringComparison.Ordinal);
        var back = JsonSerializer.Deserialize<ValidationReport>(json, Validator.JsonOptions)!;
        Assert.Equal(json, JsonSerializer.Serialize(back, Validator.JsonOptions));
    }
}
