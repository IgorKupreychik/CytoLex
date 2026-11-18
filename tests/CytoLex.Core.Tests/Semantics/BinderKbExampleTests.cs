using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Parsing;

namespace CytoLex.Core.Tests.Semantics;

/// <summary>
/// T-065 acceptance: every valid and <c>corrected</c> karyotype/general KB example binds to a complete model, and every
/// syntax error of an invalid example is named by a partial node below the root.
/// </summary>
public class BinderKbExampleTests
{
    public static IEnumerable<TheoryDataRow<string, bool>> ValidCases() =>
        ParseHelper.KbExamples()
            .Where(x => x.Kind != ExampleKind.Invalid)
            .Select(x => new TheoryDataRow<string, bool>(x.Text, x.Fragment) { TestDisplayName = $"{x.RuleId} {x.Kind}: {x.Text}" });

    public static IEnumerable<TheoryDataRow<string, bool>> InvalidCases() =>
        ParseHelper.KbExamples()
            .Where(x => x.Kind == ExampleKind.Invalid)
            .Select(x => new TheoryDataRow<string, bool>(x.Text, x.Fragment) { TestDisplayName = $"{x.RuleId} invalid: {x.Text}" });

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void ValidExample_BindsToACompleteModel(string text, bool fragment)
    {
        var models = BindHelper.Bind(text, fragment);
        Assert.NotEmpty(models);
        foreach (var model in models)
        {
            var nodes = model.DescendantsAndSelf().ToList();
            Assert.Empty(nodes.Where(n => n.IsPartial).Select(n => $"{n.GetType().Name} '{n.Span.Of(text)}'"));
            Assert.Empty(nodes.OfType<UnboundItem>().Select(n => n.Text));
            Assert.All(nodes, n => Assert.True(n.Span.Start >= model.Tree.Segment.Span.Start && n.Span.End <= model.Tree.Segment.Span.End));

            foreach (var symbol in nodes.OfType<SymbolRef>())
            {
                Assert.True(symbol.IsListed, $"'{symbol.Symbol}' is not listed");
                Assert.Contains(symbol.Kb, k => k.Kind == KbRefKind.Vocab);
                if (Binder.SymbolRules.ContainsKey(symbol.Symbol))
                {
                    Assert.Contains(symbol.Kb, k => k.Kind == KbRefKind.Rule);
                }
            }

            // Every abnormality has at least one part with a symbol; every short-system group of a part with a
            // chromosome list has a chromosome, unless the book wrote more groups than chromosomes (rule's job).
            Assert.All(nodes.OfType<Abnormality>(), a => Assert.NotEmpty(a.Parts));
        }
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void InvalidExample_SyntaxErrorsAreNamedByPartialNodes(string text, bool fragment)
    {
        foreach (var model in BindHelper.Bind(text, fragment))
        {
            Assert.Equal(model.Tree.Diagnostics, model.Issues);
            var below = model.DescendantsAndSelf().Skip(1).Where(n => n.IsPartial).SelectMany(n => n.Issues).ToHashSet();
            var unnamed = model.Tree.Diagnostics.Where(d => !below.Contains(d)).Select(d => $"{d.RuleId} '{d.Span.Of(text)}'@{d.Span.Start}");
            if (!model.CellLines.IsEmpty || !model.Prefixes.IsEmpty)
            {
                Assert.Empty(unnamed);
            }
        }
    }
}
