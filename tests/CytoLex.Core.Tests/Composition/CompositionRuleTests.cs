using System.Collections.Immutable;
using CytoLex.Core.Rules.Checks;
using CytoLex.Core.Semantics;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Composition;

/// <summary>
/// T-086: the rules that read <see cref="Abnormality.Composition"/>: <c>STRUCT.DETAILED_MATCHES_SYMBOL</c> (a detailed
/// composition is the one its symbol defines) and <c>DER.COMPLEX_USE_DETAILED</c> (a short-system derivative the composer
/// can't place).
/// </summary>
public class CompositionRuleTests
{
    private const string _matches = "STRUCT.DETAILED_MATCHES_SYMBOL";
    private const string _complex = "DER.COMPLEX_USE_DETAILED";

    private static ImmutableArray<Diagnostic> Diagnostics(string input) =>
        Validator.Default.Validate(input, new ValidationOptions { Editions = EditionSelection.One("2016") }).Results.Single().Diagnostics;

    private static List<Diagnostic> Of(string input, string rule) => [.. Diagnostics(input).Where(d => d.RuleId == rule)];

    /// <summary>Every detailed form the book prints next to a short form is what its symbol defines.</summary>
    [Theory]
    [MemberData(nameof(CompositionPairTests.Cases), MemberType = typeof(CompositionPairTests))]
    public void BookDetailedForms_MatchTheirSymbol(string cite)
    {
        var pair = CompositionPairTests.Pairs.Single(p => p.Cite == cite);
        foreach (var detailed in pair.DetailedForms)
        {
            foreach (var a in CompositionPairTests.Items(detailed).OfType<Abnormality>())
            {
                Assert.True(DetailedSymbolMatch.Expected(a) is null, $"{detailed}: {DetailedSymbolMatch.Expected(a)}");
            }
        }
    }

    [Theory]
    [InlineData("46,XY,inv(3)(pter→p13::p13→q21::q21→qter)", "p13 and q21", "pter→p13::q21→p13::q21→qter")]
    [InlineData("46,XX,del(5)(pter→q33::q13→qter)", null, null)]
    [InlineData("46,XX,dup(1)(pter→q22::q25→qter)", null, null)]
    [InlineData("46,XX,i(17)(qter→q10::q10→pter)", "q10", "qter→q10::q10→qter")]
    [InlineData("46,XY,t(2;5)(2pter→2q21::5q31→5qter;5pter→5q31::2q31→2qter)", null, null)]
    [InlineData("46,XX,r(4)(::p16→q35::q35→p16::)", null, null)]
    public void Mismatch_IsReported(string input, string? breakpoints, string? composition)
    {
        var d = Assert.Single(Of(input, _matches));
        Assert.Equal(Severity.Error, d.Severity);
        if (breakpoints is not null)
        {
            Assert.Contains($"with breakpoints {breakpoints} it is {composition}", d.Detail, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("46,XY,inv(3)(pter→p13::q21→p13::q21→qter)")]
    [InlineData("46,XX,del(5)(pter→q13:)")]
    [InlineData("46,XX,del(5)(pter→q13::q13→qter)")]
    [InlineData("46,XY,dup(1)(pter→q25::q25→q22::q25→qter)")] // inverted duplication: dup(1)(q25q22)
    [InlineData("46,XX,der(1)(1qter→1p22::3q13.1→3qter)")] // orientation is STRUCT.COMPOSITION_ORDER's; der is not checked
    [InlineData("46,XX,del(5)(q13→q33)")] // short-system arrow: DEL.FORMAT only
    [InlineData("47,XX,+dup(10)(qter→q25::q25→q26→neo→q26→qter)")] // neocentromere: no composition model
    [InlineData("46,XX,inv(3)(pter→p1?3::q21→p1?3::q21→qter)")] // uncertain point
    public void NotReported(string input) => Assert.Empty(Of(input, _matches));

    [Fact]
    public void ReversedComposition_IsOnlyAnOrderFault()
    {
        var input = "46,XY,inv(3)(qter→q21::p13→q21::p13→pter)";
        Assert.Empty(Of(input, _matches));
    }

    [Theory]
    [InlineData("46,XX,der(1)t(1;3)(p22;q13.1)t(1;5)(p32;q31)", "t(1;5)(p32;q31)")]
    [InlineData("46,XX,der(1)t(1;3)(p32;q21)del(1)(p34)", "del(1)(p34)")]
    public void UnplaceableDerivative_IsAnInfo(string input, string span)
    {
        var d = Assert.Single(Of(input, _complex));
        Assert.Equal(Severity.Info, d.Severity);
        Assert.Equal(span, d.Span.Of(input));
    }

    /// <summary>No KB valid example (and no corrected form) of the 2016 edition is a derivative the short system can't describe.</summary>
    [Fact]
    public void KbValidExamples_HaveNoUnplaceableDerivative()
    {
        var hits = KbExamples.KbCatalog.Default.Cases
            .Where(c => c.Rule.Edition == "2016" && c.Kind != KbExamples.ExampleKind.Invalid && !c.IsFragment)
            .Select(c => c.CheckedText)
            .Distinct(StringComparer.Ordinal)
            .Where(text => Of(text, _complex).Count > 0)
            .ToList();
        Assert.Empty(hits);
    }

    [Theory]
    [InlineData("46,XX,der(1)t(1;3)(p32;q21)t(1;11)(q25;q13)")]
    [InlineData("46,XY,der(1)t(1;3)(p32;q21)t(3;7)(q28;q11.2)")] // a new rearrangement the short system still places
    [InlineData("46,XX,der(1)t(1;3)(p3?2;q21)t(1;5)(p34;q31)")] // uncertain: not a placement fault
    [InlineData("47,XX,+der(?)t(?;9)(?;p13)ins(?;7)(?;q11.2q32)")] // undefined shape (T-087)
    [InlineData("46,XX,der(9)t(9;11)(p22;q23)t(11;12)(p13;q22)c,der(11)t(9;11)t(11;12)c,der(12)t(11;12)c[20]")] // §11.3 E10: history of other chromosomes
    public void ComposedOrUndefinedDerivative_IsNotReported(string input) => Assert.Empty(Of(input, _complex));
}
