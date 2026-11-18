using CytoLex.Core.Segmentation;
using CytoLex.Core.Tests.KbExamples;

namespace CytoLex.Core.Tests.Segmentation;

/// <summary>T-026 acceptance: every KB example is split losslessly and its type is detected as its rule's <c>applies_to</c>.</summary>
public class KbExampleDetectionTests
{
    [Fact]
    public void EveryKbExample_IsSplitLosslessly()
    {
        var texts = KbCatalog.Default.Cases.Select(c => c.Kind == ExampleKind.Corrected ? c.Corrected! : c.Text).Distinct(StringComparer.Ordinal).ToList();
        Assert.NotEmpty(texts);

        foreach (var text in texts)
        {
            SegmenterTests.AssertLossless(text, Segmenter.Split(text));
        }
    }

    [Fact]
    public void WholeStringKbExamples_AreDetectedAsTheirRulesType()
    {
        var checkedCases = 0;
        var mismatches = new List<string>();
        foreach (var c in KbCatalog.Default.Cases)
        {
            // General rules apply to every type; fragments (inv(2), …) are not whole strings.
            if (c.IsFragment || c.Rule.AppliesTo.Contains("general"))
            {
                continue;
            }

            checkedCases++;
            var type = Segmenter.Split(c.CheckedText)[0].Type.ToKbName();
            if (!c.Rule.AppliesTo.Contains(type))
            {
                mismatches.Add($"{c.DisplayName}: detected {type}, rule applies to {string.Join(", ", c.Rule.AppliesTo)}");
            }
        }

        Assert.NotEqual(0, checkedCases);
        Assert.Empty(mismatches);
    }

    [Fact]
    public void KbExamples_CoverKaryotypeAndArr()
    {
        // The 2016 KB (ch. 2–9 so far) has whole-string examples of these types; the other types are
        // covered by SegmenterTests until their chapters (T-015–T-019) add examples.
        var detected = KbCatalog.Default.Cases
            .Where(c => !c.IsFragment)
            .SelectMany(c => Segmenter.Split(c.CheckedText).Select(s => s.Type.ToKbName()))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("karyotype", detected);
        Assert.Contains("arr", detected);
    }
}
