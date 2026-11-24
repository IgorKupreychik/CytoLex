using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>
/// T-041: level-2 "did you mean" suggestions for unlisted words (<c>SYM.LISTED_ONLY</c>) and band names the edition
/// doesn't have (<c>BAND.MUST_EXIST</c>): 1–3 options, each a replace with a preview, never applied automatically.
/// </summary>
public class SuggestionTests
{
    private const string _listedOnly = "SYM.LISTED_ONLY";
    private const string _mustExist = "BAND.MUST_EXIST";

    private static EditionResult Validate(string input, bool autoCorrect = false) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), AutoCorrect = autoCorrect }).Results[0];

    private static List<Fix> Suggestions(EditionResult result, string ruleId) =>
        [.. Assert.Single(result.Diagnostics, d => d.RuleId == ruleId).Fixes.Where(f => f.IsSuggestion)];

    /// <summary>The test set of typos: input, rule, expected first suggestion (whole corrected string).</summary>
    public static TheoryData<string, string, string> Typos => new()
    {
        // symbols: typos of a listed abbreviation
        { "46,XX,dl(5)(q13q33)", _listedOnly, "46,XX,del(5)(q13q33)" },
        { "46,XX,dell(5)(q13q33)", _listedOnly, "46,XX,del(5)(q13q33)" },
        { "46,XY,invv(9)(p12q13)", _listedOnly, "46,XY,inv(9)(p12q13)" },
        { "46,XX,dpu(1)(q21q32)", _listedOnly, "46,XX,dup(1)(q21q32)" },
        { "46,XX,dle(5)(q13q33)", _listedOnly, "46,XX,del(5)(q13q33)" },
        // symbols: the word a listed abbreviation stands for
        { "46,XY,trans(9;22)(q34;q11.2)", _listedOnly, "46,XY,t(9;22)(q34;q11.2)" },
        { "46,XY,translocation(9;22)(q34;q11.2)", _listedOnly, "46,XY,t(9;22)(q34;q11.2)" },
        { "46,XX,deletion(5)(q13q33)", _listedOnly, "46,XX,del(5)(q13q33)" },
        { "46,XY,inversion(9)(p12q13)", _listedOnly, "46,XY,inv(9)(p12q13)" },
        { "46,XY,inversoin(9)(p12q13)", _listedOnly, "46,XY,inv(9)(p12q13)" },
        // bands
        { "46,XY,del(9)(q35)", _mustExist, "46,XY,del(9)(q34)" },
        { "46,XY,t(9;22)(q35;q11.2)", _mustExist, "46,XY,t(9;22)(q34;q11.2)" },
        { "46,XX,del(9)(q34.4)", _mustExist, "46,XX,del(9)(q34.3)" },
        { "46,XX,del(1)(p37)", _mustExist, "46,XX,del(1)(p36)" },
        { "46,XX,del(5)(q37)", _mustExist, "46,XX,del(5)(q35)" },
    };

    [Theory]
    [MemberData(nameof(Typos))]
    public void Typo_GetsOneToThreeSuggestions_BestFirst(string input, string ruleId, string expected)
    {
        var suggestions = Suggestions(Validate(input), ruleId);

        Assert.InRange(suggestions.Count, 1, 3);
        Assert.Equal(expected, suggestions[0].Preview);
        Assert.All(suggestions, s =>
        {
            Assert.Equal(FixLevel.Suggestion, s.Level);
            Assert.Equal(FixKind.Replace, s.Kind);
            Assert.Single(s.Edits);
            Assert.StartsWith("Did you mean", s.Description, StringComparison.Ordinal);
            Assert.False(s.IsEdit);
            Assert.True(s.HasEdits);
        });
    }

    [Theory]
    [MemberData(nameof(Typos))]
    public void Typo_EverySuggestion_RemovesTheFinding(string input, string ruleId, string expected)
    {
        _ = expected;
        foreach (var s in Suggestions(Validate(input), ruleId))
        {
            var after = Validate(s.Preview!);
            Assert.DoesNotContain(after.Diagnostics, d => d.RuleId == ruleId || d.RuleId.StartsWith("CORE.", StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(Typos))]
    public void Typo_KeepsTheExplanation_AfterTheSuggestions(string input, string ruleId, string expected)
    {
        _ = expected;
        var fixes = Assert.Single(Validate(input).Diagnostics, d => d.RuleId == ruleId).Fixes;
        Assert.Equal(FixLevel.Explanation, fixes[^1].Level);
        Assert.All(fixes.Take(fixes.Length - 1), f => Assert.Equal(FixLevel.Suggestion, f.Level));
    }

    [Theory]
    [MemberData(nameof(Typos))]
    public void Typo_IsNeverAppliedAutomatically(string input, string ruleId, string expected)
    {
        _ = (ruleId, expected);
        var result = Validate(input);
        Assert.Equal(input, Fixer.ApplyAll(input, result).Text);

        // SYM.LISTED_ONLY is capped at warning (needs_review), so such strings may be valid; what matters is the text.
        var corrected = Validate(input, autoCorrect: true);
        Assert.True(corrected.AutoCorrection is null || corrected.AutoCorrection.Corrected == input);
        Assert.NotEqual(EditionVerdict.ValidAfterAutoCorrection, corrected.Verdict);
    }

    [Theory]
    [InlineData("46,XX,qwzx(5)(q13q33)")]
    [InlineData("46,XX,abcdefgh(5)(q13q33)")]
    public void UnrelatedWord_GetsNoSuggestion(string input)
    {
        var d = Assert.Single(Validate(input).Diagnostics, d => d.RuleId == _listedOnly);
        Assert.DoesNotContain(d.Fixes, f => f.IsSuggestion);
        Assert.Equal(FixLevel.Explanation, Assert.Single(d.Fixes).Level);
    }

    [Fact]
    public void Symbol_SuggestionNamesTheMeaning()
    {
        var s = Suggestions(Validate("46,XX,dl(5)(q13q33)"), _listedOnly)[0];
        Assert.Equal("Did you mean 'del' (Deletion)?", s.Description);
    }

    [Fact]
    public void Band_OnTheOtherArm_IsSuggested()
    {
        var previews = Suggestions(Validate("46,XY,del(9)(p34)"), _mustExist).Select(s => s.Preview);
        Assert.Contains("46,XY,del(9)(q34)", previews);
    }

    [Fact]
    public void Band_EnclosingBand_IsSuggestedWhenNoSiblingExists()
    {
        var previews = Suggestions(Validate("46,XX,del(21)(q11.29)"), _mustExist).Select(s => s.Preview);
        Assert.Contains("46,XX,del(21)(q11.2)", previews);
    }

    /// <summary>The KB's arr examples of <c>BAND.MUST_EXIST</c>: the corrected band is the one holding the stated position.</summary>
    [Theory]
    [InlineData(
        "arr[GRCh38] 9p24.3(1310386_1709409)×1 mat,9p22.3p22.2(16455330_16763471)×1 dn,18q22.33q22.1(62747805_67920791)×1 dn",
        "arr[GRCh38] 9p24.3(1310386_1709409)×1 mat,9p22.3p22.2(16455330_16763471)×1 dn,18q21.33q22.1(62747805_67920791)×1 dn")]
    [InlineData(
        "arr[GRCh38] 15q11.2q26.3(23123715_101888908)×2 hmz pat,21q11.21q22.3(14595263_48084819)×2 hmz pat",
        "arr[GRCh38] 15q11.2q26.3(23123715_101888908)×2 hmz pat,21q11.2q22.3(14595263_48084819)×2 hmz pat")]
    public void ArrBand_SuggestsTheBandHoldingThePosition(string input, string corrected)
    {
        var suggestions = Suggestions(Validate(input), _mustExist);
        Assert.Equal(corrected, suggestions[0].Preview);
    }

    [Fact]
    public void KbExample_ListedOnly_CorrectedIsTheFirstSuggestion()
    {
        var s = Suggestions(Validate("46,XX,dl(5)(q13q33)"), _listedOnly);
        Assert.Equal("46,XX,del(5)(q13q33)", s[0].Preview);
    }

    [Fact]
    public void SuggestionsSurviveJsonRoundTrip()
    {
        var report = Validator.Default.Validate("46,XX,dl(5)(q13q33)", new ValidationOptions { Editions = EditionSelection.One("2016") });
        var json = System.Text.Json.JsonSerializer.Serialize(report, Validator.JsonOptions);
        Assert.Contains("\"Suggestion\"", json, StringComparison.Ordinal);
    }
}
