using System.Text.Json;
using CytoLex.Core.Kb;

namespace CytoLex.Core.Tests;

public class EmbeddedBundleTests
{
    [Fact]
    public void EmbeddedBundle_IsPresentAndParses()
    {
        using var stream = EmbeddedBundle.Open();
        using var doc = JsonDocument.Parse(stream);

        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("format_version", out var formatVersion));
        Assert.Equal(JsonValueKind.Number, formatVersion.ValueKind);
        Assert.True(root.GetProperty("editions").TryGetProperty("2016", out _));
    }

    [Fact]
    public void EmbeddedBundle_IsTheLeanRuntimeBundle()
    {
        using var stream = EmbeddedBundle.Open();
        using var doc = JsonDocument.Parse(stream);

        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("format_version").GetInt32());
        var edition = root.GetProperty("editions").GetProperty("2016");

        // Rule metadata with rendered citations, but no quotes or example lists (T-060).
        Assert.False(edition.TryGetProperty("quotes", out _));
        var rules = edition.GetProperty("rules");
        Assert.NotEqual(0, rules.GetArrayLength());
        foreach (var rule in rules.EnumerateArray())
        {
            Assert.False(rule.TryGetProperty("examples", out _));
            Assert.NotEqual(0, rule.GetProperty("cite").GetArrayLength());
            Assert.StartsWith(
                "ISCN 2016 §",
                rule.GetProperty("cite")[0].GetProperty("rendered").GetString(),
                StringComparison.Ordinal);
        }

        Assert.True(edition.GetProperty("vocab").TryGetProperty("symbols", out _));
        Assert.NotEqual(0, edition.GetProperty("bands").GetProperty("bands").GetArrayLength());

        // The probe table (T-102): edition-independent, top level; every row names its source.
        var probes = root.GetProperty("probes");
        Assert.NotEqual(0, probes.GetArrayLength());
        foreach (var probe in probes.EnumerateArray())
        {
            Assert.False(string.IsNullOrEmpty(probe.GetProperty("name").GetString()));
            Assert.False(string.IsNullOrEmpty(probe.GetProperty("source").GetString()));
            _ = probe.GetProperty("reviewed").GetBoolean();
        }
    }
}
