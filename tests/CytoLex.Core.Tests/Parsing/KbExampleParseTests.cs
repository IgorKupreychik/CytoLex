using CytoLex.Core.Tests.KbExamples;

namespace CytoLex.Core.Tests.Parsing;

/// <summary>
/// T-027 acceptance: every valid karyotype/general KB example (and every <c>corrected</c> form) parses without a syntax
/// error, and the tree reproduces the input exactly. Fragments (<see cref="KbCatalog.IsFragment"/>) use the fragment entry.
/// </summary>
public class KbValidExampleParseTests
{
    public static IEnumerable<TheoryDataRow<string, bool>> Cases() =>
        ParseHelper.KbExamples()
            .Where(x => x.Kind != ExampleKind.Invalid)
            .Select(x => new TheoryDataRow<string, bool>(x.Text, x.Fragment) { TestDisplayName = $"{x.RuleId} {x.Kind}: {x.Text}" });

    [Theory]
    [MemberData(nameof(Cases))]
    public void ParsesWithoutDiagnostics(string text, bool fragment)
    {
        var trees = ParseHelper.Parse(text, fragment);
        Assert.NotEmpty(trees);
        Assert.All(trees, t => Assert.Equal(t.Segment.Span.Of(text), t.ToFullString()));
        Assert.Empty(trees.SelectMany(t => t.Diagnostics).Select(d => ParseHelper.Describe(text, d)));
    }
}

/// <summary>
/// T-027 acceptance: invalid karyotype/general KB examples produce exactly the syntax errors listed in
/// <see cref="InvalidExampleExpectations"/> (rule and span), or none when their fault is a rule's to find.
/// </summary>
public class KbInvalidExampleParseTests
{
    public static IEnumerable<TheoryDataRow<string, bool>> Cases() =>
        ParseHelper.KbExamples()
            .Where(x => x.Kind == ExampleKind.Invalid)
            .Select(x => new TheoryDataRow<string, bool>(x.Text, x.Fragment) { TestDisplayName = $"{x.RuleId} invalid: {x.Text}" });

    [Theory]
    [MemberData(nameof(Cases))]
    public void ReportsTheExpectedSyntaxErrors(string text, bool fragment)
    {
        Assert.True(InvalidExampleExpectations.Table.TryGetValue(text, out var expected),
            $"'{text}' is not in InvalidExampleExpectations: classify it (syntax errors with rule and span, or [] when a rule finds it).");

        var trees = ParseHelper.Parse(text, fragment);
        Assert.All(trees, t => Assert.Equal(t.Segment.Span.Of(text), t.ToFullString()));

        var actual = trees.SelectMany(t => t.Diagnostics).Select(d => (d.RuleId, d.Span.Of(text), d.Span.Start)).ToList();
        Assert.Equal(expected.Select(e => (e.Rule, e.Span, e.Start)), actual);
    }

    [Fact]
    public void EveryTableEntry_IsAKbInvalidExample()
    {
        var invalid = ParseHelper.KbExamples().Where(x => x.Kind == ExampleKind.Invalid).Select(x => x.Text).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(InvalidExampleExpectations.Table.Keys, k => !invalid.Contains(k));
    }

    [Fact]
    public void SomeInvalidExamples_HaveSeveralErrors()
    {
        Assert.Contains(InvalidExampleExpectations.Table.Values, v => v.Length > 1);
    }
}
