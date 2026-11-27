using CytoLex.Core.Associations;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Associations;

/// <summary>T-109: every association rule of the bundle is matched by a synthetic string, through the public validator API.</summary>
public class SeedAssociationTests
{
    private static readonly IEditionKb _kb = KnowledgeBase.LoadEmbedded().ForEdition("2016")!;

    /// <summary>One synthetic string per rule; a new rule in <c>curated/associations/associations.yaml</c> needs a line here.</summary>
    private static readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal)
    {
        ["ASSOC.TRISOMY21_DOWN"] = "47,XY,+21",
        ["ASSOC.T9_22_CML"] = "46,XY,t(9;22)(q34;q11.2)[20]",
        ["ASSOC.T9_22_BALL"] = "46,XY,t(9;22)(q34;q11.2)[20]",
        ["ASSOC.T15_17_APL"] = "46,XX,t(15;17)(q24;q21)[20]",
        ["ASSOC.T8_21_AML"] = "46,XY,t(8;21)(q22;q22)[20]",
        ["ASSOC.INV16_AML"] = "46,XX,inv(16)(p13.1q22)[20]",
        ["ASSOC.DEL22Q11_2"] = "arr[GRCh38] 22q11.21(18912231_21465672)×1",
    };

    public static TheoryData<string> RuleIds => [.. _kb.Associations.Rules.Select(r => r.Id)];

    private static EditionResult Result(string input, bool include = true) =>
        Assert.Single(Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), IncludeAssociations = include }).Results);

    [Fact]
    public void Bundle_HasTheSeedRules() =>
        Assert.True(_kb.Associations.Rules.Length >= 7, "the T-108 seed has 7 rules; run 'uv run --directory tools kb build' and 'dotnet build'");

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void EveryRule_IsMatchedByASyntheticString(string ruleId)
    {
        Assert.True(_strings.TryGetValue(ruleId, out var input), $"add a synthetic string for {ruleId} to SeedAssociationTests");
        var match = Assert.Single(Result(input).Associations, m => m.RuleId == ruleId);
        Assert.NotEmpty(match.Evidence);
    }

    // T-110: in situ results reach the seed rules through con fusions and signal counts (probes placed by the probe table).
    [Theory]
    [InlineData("nuc ish(ABL1,BCR)×3(ABL1 con BCR×1)[400]", "ASSOC.T9_22_CML")]
    [InlineData("nuc ish(PML,RARA)×3(PML con RARA×2)[200]", "ASSOC.T15_17_APL")]
    [InlineData("nuc ish(RUNX1T1,RUNX1)×3(RUNX1T1 con RUNX1×2)[200]", "ASSOC.T8_21_AML")] // RUNX1T1 at 8q21.3
    [InlineData("nuc ish(TBX1×1)[30]", "ASSOC.DEL22Q11_2")]
    [InlineData("nuc ish(RUNX1×3)[50]", "ASSOC.TRISOMY21_DOWN")] // a 21q22 gene ×3: gain in 21q22
    [InlineData("arr[GRCh38] 21q22.13(36500000_37500000)×3", "ASSOC.TRISOMY21_DOWN")]
    public void InSituAndArrayResults_MatchTheSeedRules(string input, string ruleId) =>
        Assert.NotEmpty(Assert.Single(Result(input).Associations, m => m.RuleId == ruleId).Evidence);

    [Fact]
    public void CentromereProbeOf21_IsAWholeChromosomeGain()
    {
        // A Z D-number is placed by its chromosome only (T-101), so ×3 is a gain of all of 21 and covers 21q22 (kept, T-110).
        var evidence = Assert.Single(Assert.Single(Result("nuc ish(D21Z1×3)[100]").Associations, m => m.RuleId == "ASSOC.TRISOMY21_DOWN").Evidence);
        Assert.Equal(1.0, evidence.Overlap);
        Assert.Equal("×3 read as gain", evidence.Reading);
    }

    [Fact]
    public void EveryExactForm_Parses()
    {
        var patterns = new ExactPatterns(new Parser(_kb.Vocab), new Binder(_kb));
        Assert.All(_kb.Associations.ExactForms, form => Assert.NotNull(patterns.Get(form)));
    }

    [Fact]
    public void OffByDefault() =>
        Assert.Empty(Result("47,XY,+21", include: false).Associations);

    [Fact]
    public void NeverInTheDiagnosticsOrTranslation()
    {
        var result = Assert.Single(Validator.Default.Validate(
            "46,XY,t(9;22)(q34;q11.2)[20]",
            new ValidationOptions { Editions = EditionSelection.One("2016"), IncludeAssociations = true, IncludeTranslation = true }).Results);
        Assert.NotEmpty(result.Associations);
        Assert.DoesNotContain(result.Diagnostics, d => d.RuleId.StartsWith("ASSOC.", StringComparison.Ordinal));
        var sentences = string.Join(" ", result.Translation!.Items.Select(i => i.Sentence));
        Assert.DoesNotContain("leukaemia", sentences, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpansReferToTheCorrectedTextWithAutocorrection()
    {
        // A stray space is autocorrected (SYM.NO_OTHER_SPACE); the evidence span refers to the corrected string.
        var report = Validator.Default.Validate("46,XY,t(9;22)(q34; q11.2)[20]", new ValidationOptions { Editions = EditionSelection.One("2016"), IncludeAssociations = true, AutoCorrect = true });
        var result = Assert.Single(report.Results);
        var text = result.TextOf(report.Input);
        var evidence = result.Associations.First(m => m.RuleId == "ASSOC.T9_22_CML").Evidence[0];
        Assert.Equal(evidence.Text, text.Substring(evidence.Span.Start, evidence.Span.Length));
    }
}
