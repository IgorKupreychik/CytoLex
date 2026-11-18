using System.Text.Json;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;

namespace CytoLex.Core.Tests.Segmentation;

/// <summary>The type-detection keywords trace to the KB (CLAUDE.md: validation logic traces to the KB).</summary>
public class KeywordVocabTests
{
    /// <summary>Mirror of the <c>applies_to</c> enum in <c>kb/schema/common.schema.json</c>.</summary>
    private static readonly string[] _appliesTo =
        ["general", "karyotype", "breakage", "meiotic", "ish", "nuc_ish", "fib_ish", "rev_ish", "cgh", "arr", "rsa", "seq"];

    [Fact]
    public void EveryKeywordWord_IsASymbolInThe2016Vocab()
    {
        using var stream = EmbeddedBundle.Open();
        using var doc = JsonDocument.Parse(stream);
        var symbols = doc.RootElement.GetProperty("editions").GetProperty("2016").GetProperty("vocab").GetProperty("symbols");
        var entries = symbols.GetProperty("entries").EnumerateArray()
            .ToDictionary(e => e.GetProperty("label").GetString()!, StringComparer.Ordinal);

        foreach (var (words, _) in Segmenter.Keywords)
        {
            foreach (var word in words)
            {
                Assert.True(entries.TryGetValue(word, out var entry), $"'{word}' is not a symbols vocab label");
                Assert.Contains(word, entry.GetProperty("tokens").EnumerateArray().Select(t => t.GetString()));
            }
        }
    }

    [Fact]
    public void EveryNotationType_HasAnAppliesToName()
    {
        foreach (var type in Enum.GetValues<NotationType>())
        {
            Assert.Contains(type.ToKbName(), _appliesTo);
        }
    }
}
