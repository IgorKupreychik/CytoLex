using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using CytoLex.Core.Composing;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Semantics;

namespace CytoLex.Core.Tests.Composition;

/// <summary>
/// T-066 acceptance: for every KB example block printed in both systems (valid examples with the same section, page and
/// example number, one without and one with arrows), the composition computed from the short form equals the bound
/// detailed form. Items are aligned by position in the string; an item counts when the detailed form has a written
/// composition there.
/// </summary>
public class CompositionPairTests
{
    /// <summary>One example block: its cite and its short and detailed forms.</summary>
    public sealed record Pair(string Cite, ImmutableArray<string> ShortForms, ImmutableArray<string> DetailedForms);

    public static IReadOnlyList<Pair> Pairs { get; } = LoadPairs();

    public static IEnumerable<TheoryDataRow<string>> Cases() =>
        Pairs.Select(p => new TheoryDataRow<string>(p.Cite) { TestDisplayName = $"{p.Cite}: {string.Join(" | ", p.ShortForms)}" });

    [Fact]
    public void Pairs_AreFound() => Assert.True(Pairs.Count >= 89, $"only {Pairs.Count} example blocks in both systems");

    [Theory]
    [MemberData(nameof(Cases))]
    public void ShortForm_ComposesToThePrintedDetailedForm(string cite)
    {
        var pair = Pairs.Single(p => p.Cite == cite);
        var failures = new List<string>();
        var compared = 0;
        foreach (var shortForm in pair.ShortForms)
        {
            var computed = Items(shortForm).Select(i => (i as Abnormality)?.Composition).ToList();
            var printed = pair.DetailedForms.Select(d => Items(d).Select(Written).ToList()).ToList();
            for (var i = 0; i < computed.Count; i++)
            {
                var expected = printed.Where(p => i < p.Count && p[i] is not null).Select(p => p[i]!.Value).ToList();
                if (expected.Count == 0)
                {
                    continue;
                }

                compared++;
                var actual = computed[i];
                if (actual is null || actual.Source != CompositionSource.Computed || !expected.Any(e => e.SequenceEqual(actual.Products)))
                {
                    failures.Add($"{shortForm} item {i}: computed {(actual is null ? "none" : string.Join(";", actual.Products))}, printed {string.Join(" or ", expected.Select(e => string.Join(";", e)))}");
                }
            }
        }

        PendingCompositions.SkipOrFailIfFixed(cite, failures.Count == 0 && compared > 0);
        Assert.True(compared > 0, "no item to compare");
        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DetailedText_BindsBackToTheSameComposition(string cite)
    {
        var pair = Pairs.Single(p => p.Cite == cite);
        foreach (var shortForm in pair.ShortForms)
        {
            foreach (var a in Items(shortForm).OfType<Abnormality>().Where(a => a.Composition is not null))
            {
                var rebound = Assert.IsType<Abnormality>(Assert.Single(Items(a.Composition!.Detailed, fragment: true)));
                Assert.NotNull(rebound.Composition);
                Assert.Equal(CompositionSource.Written, rebound.Composition.Source);
                Assert.Equal(a.Composition.Products, rebound.Composition.Products);
            }
        }
    }

    /// <summary>The items of every cell line of the string's karyotype segments, in order.</summary>
    internal static List<KaryotypeItem> Items(string text, bool? fragment = null) =>
        [.. BindHelper.Bind(text, fragment ?? KbCatalog.IsFragment(text)).SelectMany(m => m.CellLines).SelectMany(l => l.Items)];

    /// <summary>The written composition of a detailed item (an abnormality or a band-composition fragment).</summary>
    private static ImmutableArray<ComposedChromosome>? Written(KaryotypeItem item) => item switch
    {
        Abnormality { Composition: { Source: CompositionSource.Written } c } => c.Products,
        BandComposition b when b.Groups.All(g => g.Structure is not null) && b.Groups.Select(g => ComposedChromosome.FromWritten(g.Structure!)).ToList() is var list && list.All(x => x is not null) =>
            [.. list.OfType<ComposedChromosome>()],
        _ => null,
    };

    private static List<Pair> LoadPairs()
    {
        using var stream = typeof(CompositionPairTests).Assembly.GetManifestResourceStream(KbCatalog.ResourceName)
            ?? throw new InvalidOperationException("kb.json not embedded");
        using var doc = JsonDocument.Parse(stream);
        var blocks = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var rule in doc.RootElement.GetProperty("editions").GetProperty("2016").GetProperty("rules").EnumerateArray())
        {
            if (!rule.GetProperty("examples").TryGetProperty("valid", out var valid))
            {
                continue;
            }

            foreach (var example in valid.EnumerateArray())
            {
                if (!example.TryGetProperty("cite", out var c) || !c.TryGetProperty("example", out var ex))
                {
                    continue;
                }

                var key = $"{c.GetProperty("section").GetString()} p.{c.GetProperty("page").GetInt32()} {ex.GetString()}";
                var text = example.GetProperty("text").GetString()!;
                var list = blocks.TryGetValue(key, out var l) ? l : blocks[key] = [];
                if (!list.Contains(text))
                {
                    list.Add(text);
                }
            }
        }

        static bool IsDetailed(string s) => s.Contains('→', StringComparison.Ordinal) || s.Contains("->", StringComparison.Ordinal);
        return
        [
            .. blocks
                .Where(b => b.Value.Any(IsDetailed) && b.Value.Any(s => !IsDetailed(s)))
                .Select(b => new Pair(b.Key, [.. b.Value.Where(s => !IsDetailed(s))], [.. b.Value.Where(IsDetailed)])),
        ];
    }
}

/// <summary>
/// Example blocks whose short form doesn't compose to the printed detailed form yet, each with the reason. A pending
/// block that now passes fails its test, so the entry is removed then (same pattern as <see cref="PendingExamples"/>).
/// </summary>
internal static class PendingCompositions
{
    public static readonly IReadOnlyDictionary<string, string> Table =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["9.2.3 p.64 E28"] = "insertion into the unknown chromosome at an unknown site (ins(?;7)(?;q11.2q32) → ?→cen→?::7q11.2→7q32::?): no KB rule places it or orients the segment (T-087)",
            ["9.2.3 p.64 E29"] = "hsr(?) without a band; the book writes the unknown chromosome as ?→cen→?::hsr→?, which no KB rule defines (T-087)",
        };

    public static void SkipOrFailIfFixed(string cite, bool passes)
    {
        if (!Table.TryGetValue(cite, out var reason))
        {
            return;
        }

        Assert.False(passes, $"'{cite}' now passes: remove it from {nameof(PendingCompositions)}.");
        Assert.Skip($"pending - {reason}");
    }
}
