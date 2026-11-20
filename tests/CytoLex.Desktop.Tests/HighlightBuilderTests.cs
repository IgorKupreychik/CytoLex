using System.Text.Json;
using CytoLex.Core.Context;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.Tests;

public class HighlightBuilderTests
{
    private static (string Input, EditionResult Result, IReadOnlyList<Highlight> Highlights) Run(string input, SampleContext? hint = null)
    {
        var report = Validator.Default.Validate(input, new ValidationOptions { ContextHint = hint });
        var result = report.Results[0];
        return (input, result, HighlightBuilder.Build(input, result, report.Context.Effective));
    }

    [Fact]
    public void Valid_NoHighlights()
    {
        var (_, result, highlights) = Run("46,XY,del(5)(q13q33)");
        Assert.Empty(result.Diagnostics);
        Assert.Empty(highlights);
    }

    [Fact]
    public void MissingComma_InsertMarkerWithExpectedText()
    {
        var (_, result, highlights) = Run("46XX");
        var d = Assert.Single(result.Diagnostics, d => d.RuleId == "KARYO.COUNT_SEX_ORDER");
        var h = Assert.Single(highlights, h => h.DiagnosticIndex == result.Diagnostics.IndexOf(d));
        Assert.Equal(HighlightKind.Insert, h.Kind);
        Assert.Equal(new TextSpan(2, 0), h.Span);
        Assert.Equal(",", h.InsertText);
        Assert.Equal(Severity.Error, h.Severity);
    }

    [Fact]
    public void ExtraSpace_StruckThrough()
    {
        var (_, result, highlights) = Run("46,XX,del (5)(q13q33)");
        var d = Assert.Single(result.Diagnostics, d => d.RuleId == "SYM.NO_SPACE_AT_PARENTHESIS");
        var h = Assert.Single(highlights, h => h.DiagnosticIndex == result.Diagnostics.IndexOf(d));
        Assert.Equal(HighlightKind.Strike, h.Kind);
        Assert.Equal(new TextSpan(9, 1), h.Span);
        Assert.Null(h.InsertText);
    }

    [Fact]
    public void Reorder_UnderlinesEditedSpan()
    {
        // Chromosome order: the lower number comes first (STRUCT.CHROMOSOME_ORDER) → a replace of the covered span.
        var (input, result, highlights) = Run("46,XY,t(5;2)(q13;p11)");
        var d = Assert.Single(result.Diagnostics, d => d.Fixes.Any(f => f.IsEdit && f.Edits.Any(e => e.NewText.Length > 0 && e.Span.Length > 0)));
        var fix = d.Fixes.First(f => f.IsEdit);
        var own = highlights.Where(h => h.DiagnosticIndex == result.Diagnostics.IndexOf(d)).ToList();
        Assert.All(own, h => Assert.Equal(HighlightKind.Underline, h.Kind));
        Assert.Equal(fix.Edits.Select(e => e.Span), own.Select(h => h.Span));
        Assert.Equal(fix.Preview, Fixer.Apply(input, fix));
    }

    [Fact]
    public void ExplanationOnly_UnderlinesDiagnosticSpan()
    {
        var (_, result, highlights) = Run("46,XX,del(5q)");
        var d = Assert.Single(result.Diagnostics, d => d.Span.Length > 0 && !d.Fixes.Any(f => f.IsEdit));
        var h = Assert.Single(highlights, h => h.DiagnosticIndex == result.Diagnostics.IndexOf(d));
        Assert.Equal(HighlightKind.Underline, h.Kind);
        Assert.Equal(d.Span, h.Span);
    }

    [Fact]
    public void ConditionalDiagnostic_IsHighlighted()
    {
        var (_, result, highlights) = Run("47,XXX");
        var d = Assert.Single(result.Diagnostics, d => d.IsConditional);
        Assert.Contains(highlights, h => h.DiagnosticIndex == result.Diagnostics.IndexOf(d));

        // With a constitutional hint there is no finding and so nothing to highlight.
        var (_, firm, none) = Run("47,XXX", SampleContext.Constitutional);
        Assert.Empty(firm.Diagnostics);
        Assert.Empty(none);
    }

    [Fact]
    public void AllOccurrencesFix_NotUsedForHighlight()
    {
        var input = "46,XX,del (5)(q13q33),dup (1)(q21q32)";
        var (_, result, highlights) = Run(input);
        var spaces = result.Diagnostics.Where(d => d.RuleId == "SYM.NO_SPACE_AT_PARENTHESIS").ToList();
        Assert.Equal(2, spaces.Count);
        foreach (var d in spaces)
        {
            var h = Assert.Single(highlights, h => h.DiagnosticIndex == result.Diagnostics.IndexOf(d));
            Assert.Equal(HighlightKind.Strike, h.Kind);
            Assert.Equal(d.Span, h.Span);
        }
    }

    /// <summary>
    /// "Highlighting matches diagnostic ranges" over every KB example string: each diagnostic has at least one highlight,
    /// inside the input, on exactly its own fix's edit spans, or else on its own span.
    /// </summary>
    [Fact]
    public void EveryKbExample_HighlightsMatchDiagnostics()
    {
        var checkedDiagnostics = 0;
        foreach (var text in KbExampleStrings())
        {
            var report = Validator.Default.Validate(text);
            var result = report.Results[0];
            var highlights = HighlightBuilder.Build(text, result, report.Context.Effective);
            Assert.All(highlights, h => Assert.InRange(h.Span.End, h.Span.Start, text.Length));

            for (var i = 0; i < result.Diagnostics.Length; i++)
            {
                var d = result.Diagnostics[i];
                var own = highlights.Where(h => h.DiagnosticIndex == i).ToList();
                Assert.NotEmpty(own);
                Assert.All(own, h => Assert.Equal(d.Severity, h.Severity));
                var fix = HighlightBuilder.OwnEditFix(d, report.Context.Effective);
                IEnumerable<TextSpan> expected = fix is null ? [d.Span] : fix.Edits.Select(e => e.Span);
                Assert.Equal(expected, own.Select(h => h.Span));
                checkedDiagnostics++;
            }
        }

        Assert.True(checkedDiagnostics > 100, $"only {checkedDiagnostics} diagnostics checked");
    }

    private static SortedSet<string> KbExampleStrings()
    {
        using var stream = typeof(HighlightBuilderTests).Assembly.GetManifestResourceStream("CytoLex.Tests.kb.json")!;
        using var doc = JsonDocument.Parse(stream);
        var texts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var edition in doc.RootElement.GetProperty("editions").EnumerateObject())
        {
            foreach (var rule in edition.Value.GetProperty("rules").EnumerateArray())
            {
                if (!rule.TryGetProperty("examples", out var examples))
                {
                    continue;
                }

                foreach (var kind in examples.EnumerateObject())
                {
                    foreach (var example in kind.Value.EnumerateArray())
                    {
                        texts.Add(example.GetProperty("text").GetString()!);
                        if (example.TryGetProperty("corrected", out var corrected) && corrected.GetString() is { } c)
                        {
                            texts.Add(c);
                        }
                    }
                }
            }
        }

        return texts;
    }
}
