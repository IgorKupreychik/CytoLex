using System.Globalization;
using System.Text;
using CytoLex.Core.Context;
using CytoLex.Core.Translation;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Tests.Translation;

/// <summary>
/// T-058 acceptance: the renderer reproduces every entry of the translation golden file (T-057,
/// <c>curated/translation/2016/golden.yaml</c>; drafts count too): summary, items (span and text), sentences, readings and KB links.
/// </summary>
public class RendererGoldenTests
{
    public static TheoryData<string> Inputs2016 => [.. GoldenFile.Entries2016.Keys];

    /// <summary>The translation of a golden entry, validated with the entry's context as the hint.</summary>
    internal static TranslationResult Translate(GoldenEntry entry)
    {
        var report = Validator.Default.Validate(entry.Input, new ValidationOptions
        {
            Editions = EditionSelection.One("2016"),
            IsFragment = entry.IsFragment,
            IncludeTranslation = true,
            ContextHint = entry.Context switch
            {
                "constitutional" => SampleContext.Constitutional,
                "neoplastic" => SampleContext.Acquired,
                _ => null,
            },
        });
        return report.Results.Single().Translation!;
    }

    /// <summary>The differences between the rendered translation and the golden entry; empty when they agree.</summary>
    internal static List<string> Differences(GoldenEntry entry, TranslationResult t)
    {
        var diffs = new List<string>();
        if (t.Summary.Text != entry.Summary)
        {
            diffs.Add($"summary\n    expected: {entry.Summary}\n    actual:   {t.Summary.Text}");
        }

        var actual = t.Items.ToList();
        if (actual.Count != entry.Items.Count)
        {
            diffs.Add($"items: expected {entry.Items.Count}, actual {actual.Count}: {string.Join(" | ", actual.Select(i => i.Text + " => " + i.Sentence))}");
            return diffs;
        }

        foreach (var (e, a) in entry.Items.Zip(actual))
        {
            if (e.Start != a.Span.Start || e.Text != a.Text)
            {
                diffs.Add($"span: expected {e.Start}:{e.Text}, actual {a.Span.Start}:{a.Text}");
            }

            if (e.Sentence != a.Sentence)
            {
                diffs.Add($"sentence '{e.Text}'\n    expected: {e.Sentence}\n    actual:   {a.Sentence}");
            }

            if (e.Constitutional != a.Readings?.Constitutional || e.Neoplastic != a.Readings?.Acquired)
            {
                diffs.Add($"readings '{e.Text}'\n    expected: {e.Constitutional} | {e.Neoplastic}\n    actual:   {a.Readings?.Constitutional} | {a.Readings?.Acquired}");
            }

            var links = a.Links.Select(l => l.Ref.ToString()).ToList();
            if (!e.Kb.SequenceEqual(links))
            {
                diffs.Add($"kb '{e.Text}'\n    expected: {string.Join(", ", e.Kb)}\n    actual:   {string.Join(", ", links)}");
            }
        }

        return diffs;
    }

    [Theory]
    [MemberData(nameof(Inputs2016))]
    public void Entry_IsRendered(string input)
    {
        var entry = GoldenFile.Entries2016[input];
        var t = Translate(entry);
        var diffs = Differences(entry, t);
        Assert.True(diffs.Count == 0, $"'{input}':\n" + string.Join("\n", diffs));
        Assert.True(t.Items.All(i => i.IsTranslated), "every golden item is translated");
        Assert.Null(t.Banner);
    }

    /// <summary>Opt-in (CYTOLEX_GOLDEN_DIFF=&lt;file&gt;): writes every mismatch to one report, for working through them.</summary>
    [Fact]
    public void WriteDifferenceReport()
    {
        var path = Environment.GetEnvironmentVariable("CYTOLEX_GOLDEN_DIFF");
        if (path is null)
        {
            return;
        }

        var sb = new StringBuilder();
        var failing = 0;
        var index = 0;
        foreach (var entry in GoldenFile.Entries2016.Values)
        {
            List<string> diffs;
            try
            {
                diffs = Differences(entry, Translate(entry));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
            {
                diffs = [$"EXCEPTION {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}"];
            }

            if (diffs.Count > 0)
            {
                failing++;
                sb.AppendLine(CultureInfo.InvariantCulture, $"## [{index}] {entry.Input}  ({entry.Context})");
                foreach (var d in diffs)
                {
                    sb.AppendLine(d);
                }
            }

            index++;
        }

        sb.Insert(0, $"failing: {failing} of {GoldenFile.Entries2016.Count}\n\n");
        File.WriteAllText(path, sb.ToString());

        // Machine-readable: the rendered wording per mismatching summary/sentence (to update draft golden entries).
        var rows = new List<object>();
        foreach (var entry in GoldenFile.Entries2016.Values)
        {
            TranslationResult t;
            try
            {
                t = Translate(entry);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (t.Summary.Text != entry.Summary)
            {
                rows.Add(new { input = entry.Input, field = "summary", expected = entry.Summary, actual = t.Summary.Text });
            }

            if (t.Items.Length == entry.Items.Count)
            {
                foreach (var (e, a) in entry.Items.Zip(t.Items))
                {
                    if (e.Sentence != a.Sentence)
                    {
                        rows.Add(new { input = entry.Input, field = "sentence", expected = e.Sentence, actual = a.Sentence });
                    }
                }
            }
        }

        File.WriteAllText(path + ".json", System.Text.Json.JsonSerializer.Serialize(rows));
    }
}
