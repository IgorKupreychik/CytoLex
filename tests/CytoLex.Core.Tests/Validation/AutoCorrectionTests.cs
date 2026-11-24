using CytoLex.Core.Kb;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>
/// T-096: autocorrection applies the level-1 fixes of the rules the KB flags with <c>fix.auto</c>, keeps the original,
/// and maps the remaining diagnostics back to it. Which rules qualify is read from the KB, never listed here.
/// </summary>
public class AutoCorrectionTests
{
    private static readonly IKnowledgeBase _kb = KnowledgeBase.LoadEmbedded();

    private static bool IsAuto(string edition, string ruleId) => _kb.ForEdition(edition)?.Rule(ruleId)?.Fix?.Auto == true;

    private static EditionResult Correct(string input, string edition = "2016", bool isFragment = false) =>
        Validator.Default.Validate(input, new ValidationOptions
        {
            Editions = EditionSelection.One(edition),
            IsFragment = isFragment,
            AutoCorrect = true,
        }).Results.Single();

    public static IEnumerable<TheoryDataRow<string>> AutoRuleInvalidExamples() =>
        KbCatalog.Default.Cases
            .Where(c => c.Kind == ExampleKind.Corrected && IsAuto(c.Rule.Edition, c.Rule.Id))
            .Select(c => new TheoryDataRow<string>(c.Key) { TestDisplayName = "autocorrect: " + c.DisplayName });

    public static IEnumerable<TheoryDataRow<string>> ValidExamples() =>
        KbCatalog.Default.Cases
            .Where(c => c.Kind == ExampleKind.Valid)
            .Select(c => new TheoryDataRow<string>(c.Key) { TestDisplayName = "unchanged: " + c.DisplayName });

    [Fact]
    public void Kb_FlagsSomeRules() =>
        Assert.Contains(KbCatalog.Default.Rules, r => IsAuto(r.Edition, r.Id));

    [Theory]
    [MemberData(nameof(AutoRuleInvalidExamples))]
    public void InvalidExampleOfAutoRule_CorrectsToItsCorrectedForm(string key)
    {
        var c = KbCatalog.Default.Find(key);
        var result = Correct(c.Text, c.Rule.Edition, c.IsFragment);
        var auto = Assert.IsType<AutoCorrection>(result.AutoCorrection);

        // The corrected form, or further when the corrected form itself still has autocorrectable faults.
        var expected = Correct(c.Corrected!, c.Rule.Edition, c.IsFragment).AutoCorrection!.Corrected;
        Assert.Equal(expected, auto.Corrected);
        Assert.Contains(auto.Corrections, x => x.RuleId == c.Rule.Id);
        Assert.All(auto.Corrections, x => Assert.True(IsAuto(c.Rule.Edition, x.RuleId), x.RuleId));
        Assert.DoesNotContain(result.Diagnostics, d => IsAuto(c.Rule.Edition, d.RuleId) && d.Fixes.Any(f => f.IsEdit && f.ForContext is null));
    }

    [Theory]
    [MemberData(nameof(ValidExamples))]
    public void ValidExample_IsNotChanged(string key)
    {
        var c = KbCatalog.Default.Find(key);
        var auto = Correct(c.Text, c.Rule.Edition, c.IsFragment).AutoCorrection;

        Assert.NotNull(auto);
        Assert.Empty(auto.Corrections);
        Assert.Equal(c.Text, auto.Corrected);
    }

    public static TheoryData<string, string, string[]> NonBookStrings() => new()
    {
        { "47,XY, +21", "47,XY,+21", ["SYM.NO_OTHER_SPACE@6-7="] },
        { "46,X Y,dup(1)(q21q32)", "46,XY,dup(1)(q21q32)", ["SYM.NO_OTHER_SPACE@4-5="] },
        { "46,xy", "46,XY", ["SYM.LETTER_CASE@3-5=XY"] },
        { "46,XY,T(9;22)(q34;q11.2)", "46,XY,t(9;22)(q34;q11.2)", ["SYM.LETTER_CASE@6-7=t"] },
    };

    [Theory]
    [MemberData(nameof(NonBookStrings))]
    public void NonBookString_IsValidAfterAutoCorrection(string input, string corrected, string[] corrections)
    {
        var result = Correct(input);
        var auto = result.AutoCorrection!;

        Assert.Equal(EditionVerdict.ValidAfterAutoCorrection, result.Verdict);
        Assert.Equal(corrected, auto.Corrected);
        Assert.Equal(corrections, auto.Corrections.Select(x => $"{x.RuleId}@{x.OriginalSpan.Start}-{x.OriginalSpan.End}={x.NewText}"));
        Assert.All(auto.Corrections, x =>
        {
            Assert.Equal(x.OriginalSpan.Of(input), x.OriginalText);
            Assert.NotEmpty(x.Citations);
            Assert.Equal(1, x.Round);
        });
    }

    [Fact]
    public void SeveralFaults_AllCorrected()
    {
        var result = Correct("46,xy,Del (5)(q13 q33)");

        Assert.Equal(EditionVerdict.ValidAfterAutoCorrection, result.Verdict);
        Assert.Equal("46,XY,del(5)(q13q33)", result.AutoCorrection!.Corrected);
        Assert.Equal(
            ["SYM.LETTER_CASE", "SYM.LETTER_CASE", "SYM.NO_SPACE_AT_PARENTHESIS", "SYM.NO_OTHER_SPACE"],
            result.AutoCorrection.Corrections.OrderBy(x => x.OriginalSpan.Start).Select(x => x.RuleId));
    }

    [Fact]
    public void BandDesignationSpaces_AreCorrected()
    {
        var result = Correct("46,XX,t(1;3)(p 32;q21)");

        Assert.Equal("46,XX,t(1;3)(p32;q21)", result.AutoCorrection!.Corrected);
        Assert.Equal("BAND.DESIGNATION_FORMAT", Assert.Single(result.AutoCorrection.Corrections).RuleId);
    }

    [Fact]
    public void RemainingError_StaysInvalid_AndMapsToTheOriginal()
    {
        const string input = "46, xy,inv(3)(q27p21)";
        var result = Correct(input);
        var auto = result.AutoCorrection!;

        Assert.Equal(EditionVerdict.NotValid, result.Verdict);
        Assert.Equal("46,XY,inv(3)(q27p21)", auto.Corrected);
        Assert.Equal(2, auto.Corrections.Length);

        var error = Assert.Single(result.Diagnostics, d => d.Severity == CytoLex.Core.Validation.Severity.Error);
        Assert.DoesNotContain(result.Diagnostics, d => IsAuto("2016", d.RuleId));
        var original = auto.ToOriginal(error.Span);
        Assert.Equal(error.Span.Of(auto.Corrected), original.Of(input));
    }

    [Fact]
    public void ValidString_IsValidWithoutCorrections()
    {
        var result = Correct("46,XX,del(5)(q13q33)");

        Assert.Equal(EditionVerdict.Valid, result.Verdict);
        Assert.Empty(result.AutoCorrection!.Corrections);
        Assert.Equal("46,XX,del(5)(q13q33)", result.AutoCorrection.Corrected);
    }

    [Fact]
    public void OptionOff_LeavesTheResultAsBefore()
    {
        var result = Validator.Default.Validate("47,XY, +21", new ValidationOptions { Editions = EditionSelection.One("2016") }).Results.Single();

        Assert.Null(result.AutoCorrection);
        Assert.Equal(EditionVerdict.NotValid, result.Verdict);
        Assert.Contains(result.Diagnostics, d => d.RuleId == "SYM.NO_OTHER_SPACE");
    }

    [Fact]
    public void AllEditions_NotImplementedEditionsHaveNoAutoCorrection()
    {
        var report = Validator.Default.Validate("46,xy", new ValidationOptions { Editions = EditionSelection.All, AutoCorrect = true });

        Assert.Equal("46,xy", report.Input);
        Assert.All(report.Results, r =>
        {
            if (r.Status == EditionStatus.NotImplemented)
            {
                Assert.Null(r.AutoCorrection);
                Assert.Equal(EditionVerdict.NotImplemented, r.Verdict);
            }
            else
            {
                Assert.Equal("46,XY", r.AutoCorrection!.Corrected);
            }
        });
    }

    [Fact]
    public void Translation_DescribesTheCorrectedString()
    {
        var result = Validator.Default.Validate("47,XY, +21", new ValidationOptions
        {
            Editions = EditionSelection.One("2016"),
            AutoCorrect = true,
            IncludeTranslation = true,
        }).Results.Single();

        Assert.NotNull(result.Translation);
        Assert.Equal(EditionVerdict.ValidAfterAutoCorrection, result.Verdict);
        Assert.Equal("47,XY,+21", result.TextOf("47,XY, +21"));
    }

    [Fact]
    public void ApplyAll_Include_FiltersDiagnostics()
    {
        var result = Validator.Default.Validate("46,xy,inv(3)(q27p21)", new ValidationOptions { Editions = EditionSelection.One("2016") }).Results.Single();

        var auto = Fixer.ApplyAll("46,xy,inv(3)(q27p21)", result, include: d => IsAuto("2016", d.RuleId));
        var all = Fixer.ApplyAll("46,xy,inv(3)(q27p21)", result);

        Assert.Equal("46,XY,inv(3)(q27p21)", auto.Text);
        Assert.Equal("46,XY,inv(3)(p21q27)", all.Text);
    }

    [Fact]
    public void ToOriginal_InsideAnEditedPiece_CoversTheReplacedSpan()
    {
        var auto = Correct("46,xy").AutoCorrection!;

        Assert.Equal(new TextSpan(3, 2), auto.ToOriginal(new TextSpan(3, 1)));
        Assert.Equal(new TextSpan(0, 2), auto.ToOriginal(new TextSpan(0, 2)));
    }
}
