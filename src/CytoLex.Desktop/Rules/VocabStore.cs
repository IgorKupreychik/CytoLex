using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CytoLex.Desktop.Rules;

/// <summary>One meaning of a vocab item.</summary>
/// <param name="Text">The paraphrased meaning.</param>
/// <param name="Sections">The ISCN sections that use the item in this meaning (may be empty).</param>
public sealed record VocabMeaning(string Text, ImmutableArray<string> Sections);

/// <summary>Everything the KB database holds about one vocab item (a symbol, abbreviation or banding code) in one edition.</summary>
/// <param name="Edition">The ISCN edition.</param>
/// <param name="Vocabulary">The vocabulary, e.g. <c>symbols</c>.</param>
/// <param name="Label">The label as printed in the book's first column.</param>
/// <param name="Kind">The entry kind (<c>abbreviation</c>, <c>punctuation</c>, <c>typographic</c>, <c>code</c>).</param>
/// <param name="Section">The section of the vocab file (the printed list).</param>
/// <param name="Tokens">The tokens that appear in a notation string.</param>
/// <param name="PairOpen">The opening token of a pair, if any.</param>
/// <param name="PairClose">The closing token of a pair, if any.</param>
/// <param name="EquivalentForms">Whether the tokens are typed forms of the printed one (<c>-&gt;</c> for <c>→</c>).</param>
/// <param name="Meanings">The meanings, in book order.</param>
/// <param name="Note">A note, if any.</param>
/// <param name="Citation">The rendered citation of the printed row.</param>
/// <param name="File">The vocab file, relative to <c>curated/vocab/</c>.</param>
public sealed record VocabDetail(
    string Edition,
    string Vocabulary,
    string Label,
    string Kind,
    string Section,
    ImmutableArray<string> Tokens,
    string? PairOpen,
    string? PairClose,
    bool EquivalentForms,
    ImmutableArray<VocabMeaning> Meanings,
    string? Note,
    string Citation,
    string File)
{
    /// <summary>The KB ID of the item, <c>&lt;vocabulary&gt;/&lt;label&gt;</c> (the <c>vocab:</c> <c>KbRef</c> without its prefix).</summary>
    public string Id => $"{Vocabulary}/{Label}";

    /// <summary>The title shown: the first meaning without its full stop, like the core's <c>VocabEntry.Title</c>.</summary>
    public string Title => TitleOf(Label, Meanings);

    internal static string TitleOf(string label, ImmutableArray<VocabMeaning> meanings) =>
        meanings.IsDefaultOrEmpty ? label : meanings[0].Text.TrimEnd('.');
}

/// <summary>
/// The vocab items of <c>build/kb.sqlite</c> (tables <c>vocab</c>, <c>vocab_tokens</c>) for the rule browser (T-059):
/// listing, search and detail. There is no FTS index for vocab (about 150 rows per edition), so a query is matched in
/// memory with the same word semantics as <see cref="RuleStore.ToFtsQuery"/>: every word is a phrase of its letter/digit
/// tokens, the last one a prefix, words ANDed; a query equal to a label or token is an exact hit.
/// </summary>
internal sealed class VocabStore(SqliteConnection connection)
{
    /// <summary>A vocab item row, as listed in the browser.</summary>
    internal sealed record Row(string Vocabulary, string Label, string Kind, string Section, int Ord, ImmutableArray<string> Tokens, ImmutableArray<VocabMeaning> Meanings)
    {
        public string Id => $"{Vocabulary}/{Label}";

        public string Title => VocabDetail.TitleOf(Label, Meanings);

        public RuleHit ToHit(string edition, RuleMatch match) =>
            new(edition, Id, Title, Section, Kind, Tokens.IsDefaultOrEmpty ? null : "Tokens: " + string.Join("  ", Tokens), match, KbEntryKind.Vocab);
    }

    /// <summary>The sections of the vocab files of <paramref name="edition"/>.</summary>
    public IEnumerable<string> Sections(string edition) => Rows(edition, null).Select(r => r.Section).Distinct(StringComparer.Ordinal);

    /// <summary>Every vocab item of <paramref name="edition"/> (optionally of one section), in book order.</summary>
    public IEnumerable<Row> Rows(string edition, string? section)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT vocabulary, label, kind, section, ord, tokens, meanings FROM vocab WHERE version = $v"
            + (section is null ? string.Empty : " AND section = $s");
        cmd.Parameters.AddWithValue("$v", edition);
        if (section is not null)
        {
            cmd.Parameters.AddWithValue("$s", section);
        }

        var rows = new List<Row>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                rows.Add(new Row(
                    r.GetString(0),
                    r.GetString(1),
                    r.GetString(2),
                    r.IsDBNull(3) ? string.Empty : r.GetString(3),
                    r.GetInt32(4),
                    StringsOf(r.GetString(5)),
                    MeaningsOf(r.GetString(6))));
            }
        }

        return rows
            .OrderBy(x => x.Section, Comparer<string>.Create(RuleStore.CompareSections))
            .ThenBy(x => x.Vocabulary, StringComparer.Ordinal)
            .ThenBy(x => x.Ord);
    }

    /// <summary>The items matching <paramref name="query"/>: exact label/token hits and the other (word) hits, each in book order.</summary>
    public (List<Row> Exact, List<Row> Other) Search(string edition, string query, string? section)
    {
        var exact = new List<Row>();
        var other = new List<Row>();
        var trimmed = query.Trim();
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => RuleStore.Tokens(w).Select(t => t.ToLowerInvariant()).ToList())
            .Where(t => t.Count > 0)
            .ToList();
        foreach (var row in Rows(edition, section))
        {
            if (string.Equals(row.Label, trimmed, StringComparison.OrdinalIgnoreCase)
                || row.Tokens.Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                exact.Add(row);
            }
            else if (words.Count > 0 && Matches(row, words))
            {
                other.Add(row);
            }
        }

        return (exact, other);
    }

    /// <summary>The item <paramref name="label"/> of <paramref name="vocabulary"/>, or <c>null</c>.</summary>
    public VocabDetail? Detail(string edition, string vocabulary, string label)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT kind, section, tokens, pair_open, pair_close, equivalent_forms, meanings, note, cite_rendered, file "
            + "FROM vocab WHERE version = $v AND vocabulary = $voc AND label = $l";
        cmd.Parameters.AddWithValue("$v", edition);
        cmd.Parameters.AddWithValue("$voc", vocabulary);
        cmd.Parameters.AddWithValue("$l", label);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
        {
            return null;
        }

        return new VocabDetail(
            edition,
            vocabulary,
            label,
            Kind: r.GetString(0),
            Section: r.IsDBNull(1) ? string.Empty : r.GetString(1),
            Tokens: StringsOf(r.GetString(2)),
            PairOpen: r.IsDBNull(3) ? null : r.GetString(3),
            PairClose: r.IsDBNull(4) ? null : r.GetString(4),
            EquivalentForms: r.GetInt32(5) != 0,
            Meanings: MeaningsOf(r.GetString(6)),
            Note: r.IsDBNull(7) ? null : r.GetString(7),
            Citation: r.IsDBNull(8) ? string.Empty : r.GetString(8),
            File: r.GetString(9));
    }

    // Each query word (its tokens) must occur as consecutive words of the item's text, the last token as a prefix.
    private static bool Matches(Row row, List<List<string>> words)
    {
        var text = string.Join(' ', [row.Label, .. row.Tokens, .. row.Meanings.Select(m => m.Text)]);
        var doc = RuleStore.Tokens(text).Select(t => t.ToLowerInvariant()).ToList();
        return words.All(w => Enumerable.Range(0, Math.Max(0, doc.Count - w.Count + 1)).Any(i => PhraseAt(doc, i, w)));
    }

    private static bool PhraseAt(List<string> doc, int start, List<string> phrase)
    {
        for (var k = 0; k < phrase.Count; k++)
        {
            var ok = k == phrase.Count - 1
                ? doc[start + k].StartsWith(phrase[k], StringComparison.Ordinal)
                : doc[start + k] == phrase[k];
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<string> StringsOf(string json) =>
        [.. JsonSerializer.Deserialize<List<string>>(json) ?? []];

    private static ImmutableArray<VocabMeaning> MeaningsOf(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = ImmutableArray.CreateBuilder<VocabMeaning>();
        foreach (var m in doc.RootElement.EnumerateArray())
        {
            var sections = m.TryGetProperty("sections", out var s)
                ? s.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToImmutableArray()
                : [];
            list.Add(new VocabMeaning(m.GetProperty("text").GetString() ?? string.Empty, sections));
        }

        return list.ToImmutable();
    }
}
