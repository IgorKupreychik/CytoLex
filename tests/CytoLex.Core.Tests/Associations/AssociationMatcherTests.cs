using System.Collections.Immutable;
using System.Text.Json;
using CytoLex.Core.Associations;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Associations;

/// <summary>T-109: matching association rules (leaves, combinators, context, evidence) with rules written in the test.</summary>
public class AssociationMatcherTests
{
    private static readonly IEditionKb _kb = KnowledgeBase.LoadEmbedded().ForEdition("2016")!;
    private static readonly ExactPatterns _patterns = new(new Parser(_kb.Vocab), new Binder(_kb));

    // 22q11.2 deletion region as in the seed rule (GRCh38).
    private const string _del22Region = """{"copy": {"effect": "loss", "chrom": "22", "grch38": [18912231, 21465672]}}""";

    internal static string Rules(string match, string context = "any", bool reviewed = false) => $$$"""
        [{"id": "ASSOC.TEST", "condition": {"name": "Test condition", "ids": [{"system": "other", "code": "X:1"}]},
          "context": "{{{context}}}", "strength": "recurrent", "source": "other", "citation": "test citation",
          "match": {{{match}}}, "review": {"reviewed": {{{(reviewed ? "true" : "false")}}}}}]
        """;

    private static ImmutableArray<AssociationMatch> Match(string rules, string input, SampleContext? hint = SampleContext.Acquired)
    {
        var parsed = AssociationRules.FromBundle(JsonDocument.Parse(rules).RootElement);
        var report = Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016"), ContextHint = hint });
        var result = Assert.Single(report.Results);
        return new AssociationMatcher(parsed, _kb.Bands, _patterns.Get).Match(report.Context, result.Abnormalities, result.Loci);
    }

    private static AssociationMatch Single(string match, string input, string context = "any", SampleContext? hint = SampleContext.Acquired) =>
        Assert.Single(Match(Rules(match, context), input, hint));

    private static void None(string match, string input, string context = "any", SampleContext? hint = SampleContext.Acquired) =>
        Assert.Empty(Match(Rules(match, context), input, hint));

    // --- exact ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Exact_WholeChromosomeGain()
    {
        var match = Single("""{"exact": "+21"}""", "47,XY,+21", hint: SampleContext.Constitutional);
        var evidence = Assert.Single(match.Evidence);
        Assert.Equal((AssociationLeafKind.Exact, "exact +21", "+21", 0, (int?)null), (evidence.Leaf, evidence.LeafText, evidence.Text, evidence.Abnormality, evidence.Locus));
        Assert.Equal("47,XY,+21".IndexOf("+21", StringComparison.Ordinal), evidence.Span.Start);
        Assert.Equal(("ASSOC.TEST", "Test condition", "test citation", true), (match.RuleId, match.Condition.Name, match.Citation, match.IsUnreviewed));
    }

    [Fact]
    public void Exact_SignAndChromosomeMustMatch()
    {
        None("""{"exact": "+21"}""", "45,XY,-21");
        None("""{"exact": "+21"}""", "47,XY,+8");
    }

    [Fact]
    public void Exact_CopiesAreNotCompared() =>
        Single("""{"exact": "+21"}""", "48,XY,+21×2");

    [Fact]
    public void Exact_FinerWrittenBandMatches() =>
        Single("""{"exact": "t(9;22)(q34;q11.2)"}""", "46,XY,t(9;22)(q34.1;q11.21)[20]");

    [Fact]
    public void Exact_CoarserWrittenBandDoesNotMatch() =>
        None("""{"exact": "t(9;22)(q34.1;q11.21)"}""", "46,XY,t(9;22)(q34;q11.2)[20]");

    [Fact]
    public void Exact_QuestionedBreakpointDoesNotMatch() =>
        None("""{"exact": "t(9;22)(q34;q11.2)"}""", "46,XY,t(9;22)(q3?4;q11.2)[20]");

    [Fact]
    public void Exact_DerivativeIsNotTheTranslocation() =>
        None("""{"exact": "t(9;22)(q34;q11.2)"}""", "46,XY,der(22)t(9;22)(q34;q11.2)[20]");

    [Fact]
    public void Exact_UnparseableFormNeverMatches() =>
        None("""{"exact": "t(9;22"}""", "46,XY,t(9;22)(q34;q11.2)[20]");

    // --- fusion --------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("46,XY,t(9;22)(q34;q11.2)[20]")]
    [InlineData("46,XY,t(9;22)(q34.1;q11.21)[20]")]
    [InlineData("46,XY,der(22)t(9;22)(q34;q11.2)[20]")]
    [InlineData("46,XY,t(9;22;12)(q34;q11.2;p13)[20]")]
    public void Fusion_Rearrangements(string input)
    {
        var evidence = Assert.Single(Single("""{"fusion": ["9q34", "22q11.2"]}""", input).Evidence);
        Assert.Equal(AssociationLeafKind.Fusion, evidence.Leaf);
    }

    [Fact]
    public void Fusion_BandOrderOfTheRuleDoesNotMatter() =>
        Single("""{"fusion": ["22q11.2", "9q34"]}""", "46,XY,t(9;22)(q34;q11.2)[20]");

    [Fact]
    public void Fusion_InversionWithinOneChromosome() =>
        Single("""{"fusion": ["16p13.1", "16q22"]}""", "46,XX,inv(16)(p13.1q22)[20]");

    [Fact]
    public void Fusion_Insertion() =>
        Single("""{"fusion": ["5q31", "2p13"]}""", "46,XY,ins(5;2)(q31;p13p23)[20]");

    [Fact]
    public void Fusion_NotAcrossTwoRearrangementsOfADerivative() =>
        None("""{"fusion": ["3q21", "9q34"]}""", "46,XX,der(1)t(1;3)(p32;q21)t(1;9)(q21;q34)[20]");

    [Fact]
    public void Fusion_OtherBandsDoNotMatch()
    {
        None("""{"fusion": ["9q34", "22q11.2"]}""", "46,XY,t(9;22)(q22;q11.2)[20]");
        None("""{"fusion": ["9q34", "22q11.2"]}""", "47,XY,+9[20]");
    }

    [Fact]
    public void Fusion_InSituConSignal()
    {
        const string input = "nuc ish(ABL1,BCR)×3(ABL1 con BCR×1)[400]";
        var evidence = Assert.Single(Single("""{"fusion": ["9q34", "22q11.2"]}""", input).Evidence);
        Assert.Equal("(ABL1 con BCR×1)", evidence.Text);
        Assert.NotNull(evidence.Locus);
    }

    [Fact]
    public void Fusion_InSituWithoutConDoesNotMatch() =>
        None("""{"fusion": ["9q34", "22q11.2"]}""", "nuc ish(ABL1,BCR)×2[400]");

    // --- copy ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Copy_KaryotypeDeletion()
    {
        var evidence = Assert.Single(Single(_del22Region, "46,XX,del(22)(q11.21q11.23)", hint: SampleContext.Constitutional).Evidence);
        Assert.Equal((AssociationLeafKind.Copy, (string?)null), (evidence.Leaf, evidence.Reading));
        Assert.NotNull(evidence.Locus);
        Assert.Equal(1.0, evidence.Overlap); // 22q11.21–q11.23 covers the whole region
    }

    [Fact]
    public void Copy_ArrCopyNumberReadAgainstTwo()
    {
        var evidence = Assert.Single(Single(_del22Region, "arr[GRCh38] 22q11.21(18912231_21465672)×1").Evidence);
        Assert.Equal(("×1 read as loss", 1.0), (evidence.Reading, evidence.Overlap));
    }

    [Fact]
    public void Copy_PartialOverlapCarriesTheShare()
    {
        // 18912231–21465672 is 2,553,442 bp; 20000000–21465672 covers 1,465,673 of them.
        var evidence = Assert.Single(Single(_del22Region, "arr[GRCh38] 22q11.21(20000000_21465672)×1").Evidence);
        Assert.Equal(Math.Round(1_465_673d / 2_553_442d, 3), evidence.Overlap);
    }

    [Fact]
    public void Copy_GainIsNotALoss()
    {
        None(_del22Region, "arr[GRCh38] 22q11.21(18912231_21465672)×3");
        Single("""{"copy": {"effect": "gain", "chrom": "22", "grch38": [18912231, 21465672]}}""", "arr[GRCh38] 22q11.21(18912231_21465672)×3");
    }

    [Fact]
    public void Copy_TwoCopiesAndRangesOverTwoAreNotRead()
    {
        None(_del22Region, "arr[GRCh38] 22q11.21(18912231_21465672)×2");
        None(_del22Region, "arr[GRCh38] 22q11.21(18912231_21465672)×1~2");
    }

    [Fact]
    public void Copy_NoOverlap() =>
        None(_del22Region, "arr[GRCh38] 22q13.33(49000000_50000000)×1");

    [Fact]
    public void Copy_SexChromosomesAreNotRead() =>
        None("""{"copy": {"effect": "loss", "chrom": "X", "grch38": [31000000, 33000000]}}""", "arr[GRCh38] Xp21.2(31000000_33000000)×1");

    [Fact]
    public void Copy_WholeChromosomeLossOverlaps() =>
        Single(_del22Region, "45,XX,-22", hint: SampleContext.Constitutional);

    [Theory]
    [InlineData("×1", CopyEffect.Loss)]
    [InlineData("×0", CopyEffect.Loss)]
    [InlineData("×3", CopyEffect.Gain)]
    [InlineData("×2~4", null)]
    [InlineData("×3~4", CopyEffect.Gain)]
    [InlineData("×~>50", CopyEffect.Gain)]
    [InlineData("amp", CopyEffect.Gain)]
    [InlineData("–", CopyEffect.Loss)]
    [InlineData("+", null)]
    [InlineData("+×2", null)]
    [InlineData("cx", null)]
    public void Copy_CountReading(string copy, CopyEffect? expected) =>
        Assert.Equal(expected, CopyMatcher.ReadCount(copy));

    // --- combinators ---------------------------------------------------------------------------------------------------

    private const string _plusEightWithoutSeven = """{"all": [{"exact": "+8"}, {"not": {"exact": "-7"}}]}""";

    [Fact]
    public void All_WithNot()
    {
        var match = Single(_plusEightWithoutSeven, "47,XY,+8[20]");
        Assert.Equal("+8", Assert.Single(match.Evidence).Text); // a not adds no evidence
        None(_plusEightWithoutSeven, "46,XY,-7,+8[20]");
    }

    [Fact]
    public void All_NeedsEveryPart()
    {
        const string both = """{"all": [{"exact": "+8"}, {"exact": "+21"}]}""";
        Assert.Equal(["+8", "+21"], Single(both, "48,XY,+8,+21[20]").Evidence.Select(e => e.Text));
        None(both, "47,XY,+8[20]");
    }

    [Fact]
    public void Any_NeedsOnePart_AndCollectsEvidenceOfEach()
    {
        const string either = """{"any": [{"exact": "t(9;22)(q34;q11.2)"}, {"fusion": ["9q34", "22q11.2"]}]}""";
        var match = Single(either, "46,XY,t(9;22)(q34;q11.2)[20]");
        Assert.Equal([AssociationLeafKind.Exact, AssociationLeafKind.Fusion], match.Evidence.Select(e => e.Leaf).Order());
        Assert.Equal(AssociationLeafKind.Fusion, Assert.Single(Single(either, "46,XY,der(22)t(9;22)(q34;q11.2)[20]").Evidence).Leaf);
    }

    [Fact]
    public void Match_IsStringWide_AcrossCellLines() =>
        Single("""{"all": [{"exact": "+8"}, {"exact": "+21"}]}""", "47,XY,+8[10]/47,XY,+21[10]");

    // --- context -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Context_NeoplasticRuleNotInAConstitutionalString()
    {
        None("""{"exact": "t(9;22)(q34;q11.2)"}""", "46,XY,t(9;22)(q34;q11.2)", context: "neoplastic", hint: SampleContext.Constitutional);
        var match = Single("""{"exact": "t(9;22)(q34;q11.2)"}""", "46,XY,t(9;22)(q34;q11.2)", context: "neoplastic", hint: SampleContext.Acquired);
        Assert.Equal([SampleContext.Acquired], match.Contexts);
        Assert.True(match.IsContextKnown);
    }

    [Fact]
    public void Context_UnknownListsTheReadingsItHoldsIn()
    {
        var neoplastic = Single("""{"exact": "+21"}""", "47,XY,+21", context: "neoplastic", hint: null);
        Assert.Equal([SampleContext.Acquired], neoplastic.Contexts);
        Assert.False(neoplastic.IsContextKnown);

        var any = Single("""{"exact": "+21"}""", "47,XY,+21", context: "any", hint: null);
        Assert.Equal([SampleContext.Constitutional, SampleContext.Acquired], any.Contexts);
    }

    [Fact]
    public void Context_ConstitutionalMarkMatchesAConstitutionalRuleInANeoplasticString()
    {
        var match = Single("""{"exact": "+21"}""", "48,XY,+8,+21c[20]", context: "constitutional", hint: SampleContext.Acquired);
        Assert.Equal("+21c", Assert.Single(match.Evidence).Text);
        None("""{"exact": "+21"}""", "47,XY,+21[20]", context: "constitutional", hint: SampleContext.Acquired);
    }

    // --- rule data -----------------------------------------------------------------------------------------------------

    [Fact]
    public void RuleData_CarriedIntoTheMatch()
    {
        const string rules = """
            [{"id": "ASSOC.TEST2", "condition": {"name": "Other"}, "context": "neoplastic", "strength": "characteristic",
              "defining": [{"source": "WHO-HAEM5", "citation": "entry"}], "source": "WHO-HAEM5", "citation": "c",
              "match": {"exact": "+8"}, "review": {"reviewed": true}}]
            """;
        var match = Assert.Single(Match(rules, "47,XY,+8[20]"));
        Assert.Equal((AssociationContext.Neoplastic, AssociationStrength.Characteristic, "WHO-HAEM5", false), (match.Context, match.Strength, match.Source, match.IsUnreviewed));
        Assert.Equal(new DefiningCriterion("WHO-HAEM5", "entry"), Assert.Single(match.Defining));
        Assert.Empty(match.Condition.Ids);
    }

    [Fact]
    public void BandNesting_ByName()
    {
        Assert.True(BandNesting.ByName("22q11.21", "22q11.2"));
        Assert.True(BandNesting.ByName("9q34.12", "9q34"));
        Assert.False(BandNesting.ByName("9q341", "9q34"));
        Assert.False(BandNesting.ByName("22q11.2", "22q11.21"));
        Assert.False(BandNesting.ByName("1q21", "1q2"));
    }
}
