using System.Text.Json;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Tests.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Tests.Translation;

/// <summary>One item of a golden entry: where it is, its sentence and readings, and the KB entries it links to.</summary>
internal sealed record GoldenItem(int Start, string Text, IReadOnlyList<string> Kb, string Sentence = "", string? Constitutional = null, string? Neoplastic = null);

/// <summary>One entry of <c>curated/translation/&lt;version&gt;/golden.yaml</c> (T-057), as embedded in <c>kb.json</c>.</summary>
internal sealed record GoldenEntry(string Input, bool IsFragment, bool IsSynthetic, string Context, IReadOnlyList<GoldenItem> Items, string Summary = "");

/// <summary>
/// The translation golden file (T-057) and the item list it is written against: which parts of a string get a sentence
/// of their own. The renderer (T-058) must produce exactly these items.
/// </summary>
internal static class GoldenFile
{
    private static readonly Lazy<IReadOnlyDictionary<string, GoldenEntry>> _entries2016 = new(() => Load("2016"));

    /// <summary>The ISCN 2016 golden entries by input.</summary>
    public static IReadOnlyDictionary<string, GoldenEntry> Entries2016 => _entries2016.Value;

    private static Dictionary<string, GoldenEntry> Load(string edition)
    {
        using var stream = typeof(KbCatalog).Assembly.GetManifestResourceStream(KbCatalog.ResourceName)!;
        using var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, GoldenEntry>(StringComparer.Ordinal);
        if (!doc.RootElement.GetProperty("editions").GetProperty(edition).TryGetProperty("translation", out var translation))
        {
            return result;
        }

        foreach (var e in translation.GetProperty("golden").EnumerateArray())
        {
            var items = e.GetProperty("items").EnumerateArray().Select(i => new GoldenItem(
                i.GetProperty("span").GetProperty("start").GetInt32(),
                i.GetProperty("span").GetProperty("text").GetString()!,
                [.. i.GetProperty("kb").EnumerateArray().Select(k => k.GetString()!)],
                i.GetProperty("sentence").GetString()!,
                Reading(i, "constitutional"),
                Reading(i, "neoplastic"))).ToList();
            var input = e.GetProperty("input").GetString()!;
            var fragment = e.TryGetProperty("fragment", out var f) && f.GetBoolean();
            var synthetic = e.TryGetProperty("origin", out var o) && o.GetString() == "synthetic";
            result[input] = new GoldenEntry(input, fragment, synthetic, e.GetProperty("context").GetString()!, items, e.GetProperty("summary").GetString()!);
        }

        return result;
    }

    private static string? Reading(JsonElement item, string context) =>
        item.TryGetProperty("readings", out var r) && r.TryGetProperty(context, out var text) ? text.GetString() : null;

    /// <summary>
    /// The translated items of a karyotype model, in input order: with two or more cell lines, each line's header
    /// (count, ploidy and sex chromosomes; its cell count is part of the sentence, not the span) followed by its items;
    /// with one cell line just its items (the header goes into the summary). Prefixes (<c>mos</c>, <c>chi</c>, <c>end</c>)
    /// go into the summary; text the parser skipped gets no item.
    /// </summary>
    public static IEnumerable<(TextSpan Span, IReadOnlyList<MeaningNode> Nodes)> Items(KaryotypeModel model)
    {
        var several = model.CellLines.Length >= 2;
        foreach (var line in model.CellLines)
        {
            if (several)
            {
                MeaningNode?[] header = [line.Count, line.Ploidy, line.Sex];
                var parts = header.OfType<MeaningNode>().ToList();
                if (parts.Count > 0)
                {
                    var start = parts[0].Span.Start;
                    var end = parts[^1].Span.Start + parts[^1].Span.Length;
                    yield return (new TextSpan(start, end - start), parts);
                }
            }

            foreach (var item in line.Items.Where(i => i is not UnboundItem))
            {
                yield return (item.Span, [item]);
            }
        }
    }

    /// <summary>
    /// The translated items of an in situ model (T-093): one per result, its span including the cells, in input order;
    /// text the parser skipped gets no item.
    /// </summary>
    public static IEnumerable<(TextSpan Span, IReadOnlyList<MeaningNode> Nodes)> Items(FishModel model) =>
        model.CellLines.SelectMany(l => l.Items).Select(i => (i.Span, (IReadOnlyList<MeaningNode>)[i]));

    /// <summary>The translated items of a microarray model (T-105): one per result, in input order; text the parser skipped gets no item.</summary>
    public static IEnumerable<(TextSpan Span, IReadOnlyList<MeaningNode> Nodes)> Items(ArrModel model) =>
        model.Aberrations.Select(a => (a.Span, (IReadOnlyList<MeaningNode>)[a]));

    /// <summary>The translated items of a string's segments, in segment order.</summary>
    public static IEnumerable<(TextSpan Span, IReadOnlyList<MeaningNode> Nodes)> Items(IReadOnlyList<SegmentModel> models) =>
        models.SelectMany(m => m switch
        {
            KaryotypeModel k => Items(k),
            FishModel f => Items(f),
            ArrModel a => Items(a),
            _ => [],
        });

    /// <summary>The KB refs of nodes and everything below them, in first-seen order.</summary>
    public static IReadOnlyList<string> Refs(IEnumerable<MeaningNode> nodes) =>
        [.. nodes.SelectMany(n => n.DescendantsAndSelf()).SelectMany(n => n.Kb).Select(k => k.ToString()).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The segment models of a golden input: karyotype, in situ (T-093) and microarray (T-105) segments; <c>null</c> when
    /// a segment is of another type or the string has none.
    /// </summary>
    public static IReadOnlyList<SegmentModel>? Models(string input, bool fragment)
    {
        var models = BindHelper.BindAll(input, fragment);
        return models.Count > 0 && models.All(m => m is KaryotypeModel or FishModel or ArrModel) ? [.. models.Cast<SegmentModel>()] : null;
    }
}
