using CytoLex.Core.Syntax;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Validation;

/// <summary>
/// T-042: underlined homologues (U+0332 after each underlined character). Underlining a chromosome identifier is
/// validated; underlining anywhere else stays a not-implemented construct.
/// </summary>
public sealed class UnderliningTests
{
    private static EditionResult Validate(string input) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016") }).Results.Single();

    /// <summary>The underlined KB examples of FRA/DER/TRANSLOC.HOMOLOGUE_UNDERLINING (the fra fragment as a whole karyotype).</summary>
    public static TheoryData<string> BookStrings => new(
        [
            "46,XX,fra(10)(q22.1),fra(1̲0̲)(q25.2)", // the book fragment with a count and sex field
            "46,XX,der(1)t(1;3)(p34.3;q21),der(1̲)t(1̲;3)(p34.3;q21)",
            "46,XX,der(1)t(1;3)(p34.3;q21)[20]/46,XX,der(1̲)t(1̲;3)(p34.3;q21)[10]",
            "46,XX,der(1̲)t(1;1̲)(p31;q32)",
            "46,XX,der(1)t(1;1̲)(p31;q32)",
            "46,XX,t(2;7;7̲)(q21;q22;p13)",
            "46,XX,t(2;7;7̲)(2pter→2q21::7̲p13→7̲pter;7pter→7q22::2q21→2qter;7qter→7q22::7̲p13→7̲qter)",
            "46,XX,t(3;9;9̲;22)(p13;q22;q34;q11.2)",
            "46,XX,t(3;9;9̲;22)(22qter→22q11.2::3p13→3qter;9pter→9q22::3p13→3pter;9̲pter→9̲q34::9q22→9qter;22pter→22q11.2::9̲q34→9̲qter)",
        ]);

    [Theory]
    [MemberData(nameof(BookStrings))]
    public void UnderlinedChromosome_IsValidAndComplete(string input)
    {
        var result = Validate(input);

        Assert.True(result.Validity.IsValid, string.Join("; ", result.Diagnostics.Select(d => d.RuleId)));
        Assert.True(result.Validity.IsComplete);
        Assert.All(result.Segments, s => Assert.Empty(s.Constructs));
    }

    [Theory]
    [InlineData("46,XX,del(1)(q2̲5)", "2̲5")]
    [InlineData("4̲6,XX", "4̲6")]
    public void UnderliningElsewhere_StaysNotImplemented(string input, string spanText)
    {
        var result = Validate(input);

        Assert.False(result.Validity.IsComplete);
        var construct = Assert.Single(result.Segments.SelectMany(s => s.Constructs));
        Assert.Equal(ConstructKind.Underlining, construct.Kind);
        Assert.Equal(spanText, construct.Span.Of(input));
        Assert.Contains("outside a chromosome number", construct.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Binder_ListsUnderlinedChromosomes()
    {
        const string input = "46,XX,der(1̲)t(1̲;3)(p34.3;q21)";
        var model = Semantics.BindHelper.BindOne(input);

        Assert.Equal(["1̲", "1̲"], model.UnderlinedChromosomes.Select(s => s.Of(input)));
        Assert.Equal([10, 15], model.UnderlinedChromosomes.Select(s => s.Start));
    }
}
