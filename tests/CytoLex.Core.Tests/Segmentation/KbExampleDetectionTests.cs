using CytoLex.Core.Segmentation;
using CytoLex.Core.Tests.KbExamples;

namespace CytoLex.Core.Tests.Segmentation;

/// <summary>
/// T-026 acceptance: every KB example is split losslessly and one of its segment types is its rule's <c>applies_to</c>
/// (any segment since T-015: a combined string such as <c>46,XY.ish …</c> illustrates an ish rule).
/// </summary>
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
            var types = Segmenter.Split(c.CheckedText).Select(s => s.Type.ToKbName()).Distinct(StringComparer.Ordinal).ToList();
            if (!types.Any(c.Rule.AppliesTo.Contains))
            {
                mismatches.Add($"{c.DisplayName}: detected {string.Join(", ", types)}, rule applies to {string.Join(", ", c.Rule.AppliesTo)}");
            }
        }

        Assert.NotEqual(0, checkedCases);
        Assert.Empty(mismatches);
    }

    [Fact]
    public void KbExamples_CoverTheirNotationTypes()
    {
        // The 2016 KB has whole-string examples of these types; rsa and seq are covered by
        // SegmenterTests until their chapters (T-019) add examples.
        var detected = KbCatalog.Default.Cases
            .Where(c => !c.IsFragment)
            .SelectMany(c => Segmenter.Split(c.CheckedText).Select(s => s.Type.ToKbName()))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("karyotype", detected);
        Assert.Contains("breakage", detected);
        Assert.Contains("meiotic", detected);
        Assert.Contains("ish", detected);
        Assert.Contains("nuc_ish", detected);
        Assert.Contains("fib_ish", detected);
        Assert.Contains("rev_ish", detected);
        Assert.Contains("cgh", detected);
        Assert.Contains("arr", detected);
    }
}
