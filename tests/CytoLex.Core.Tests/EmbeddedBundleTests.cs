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
}
