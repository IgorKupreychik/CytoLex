using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;

namespace CytoLex.Core.Kb;

/// <summary>
/// One printed vocabulary item of an edition (a ch. 3 symbol or a Table 2 banding code), as stored in the
/// runtime bundle (kb/README.md, <c>build/kb-runtime.json</c>).
/// </summary>
/// <param name="Vocabulary">The vocabulary key: <c>symbols</c> or <c>banding_codes</c>.</param>
/// <param name="Label">The entry label, unique within its vocabulary (<c>del</c>, <c>arrow (→ or -&gt;)</c>).</param>
/// <param name="Kind">The entry kind: <c>abbreviation</c>, <c>punctuation</c>, <c>code</c>, <c>typographic</c>.</param>
/// <param name="Tokens">The token forms; with <paramref name="HasEquivalentForms"/> the first is printed and the rest typed (T-063).</param>
/// <param name="HasEquivalentForms">Whether the forms after the first are typed equivalents of the printed one.</param>
/// <param name="Sections">The ISCN sections of the entry's meanings (e.g. <c>9.2.2</c>).</param>
public sealed record VocabEntry(
    string Vocabulary,
    string Label,
    string Kind,
    ImmutableArray<string> Tokens,
    bool HasEquivalentForms,
    ImmutableArray<string> Sections)
{
    /// <summary>The entry as a <c>KbRef</c> string (docs/design/core.md §5), e.g. <c>vocab:symbols/del</c>.</summary>
    public string KbRef => $"vocab:{Vocabulary}/{Label}";

    /// <summary>Whether <paramref name="token"/> is a typed (not printed) form of this entry.</summary>
    public bool IsTypedForm(string token) =>
        HasEquivalentForms && Tokens.Length > 0 && !string.Equals(Tokens[0], token, StringComparison.Ordinal);
}

/// <summary>
/// The printed vocabularies of one ISCN edition, read from the runtime KB bundle (docs/design/core.md §6).
/// The lexer builds its symbol set from it, so tokens trace to the KB. <see cref="IEditionKb.Vocab"/> exposes it.
/// </summary>
public sealed class Vocabulary
{
    /// <summary>Runtime bundle format the core understands (<c>RUNTIME_FORMAT_VERSION</c> in <c>tools/src/cytolex_tools/kb/build.py</c>).</summary>
    public const int SupportedFormatVersion = 1;

    private readonly FrozenDictionary<string, ImmutableArray<VocabEntry>> _byToken;

    private Vocabulary(string edition, ImmutableArray<VocabEntry> entries)
    {
        Edition = edition;
        Entries = entries;
        _byToken = entries
            .SelectMany(e => e.Tokens.Select(t => (Token: t, Entry: e)))
            .GroupBy(x => x.Token, StringComparer.Ordinal)
            .ToFrozenDictionary(g => g.Key, g => g.Select(x => x.Entry).ToImmutableArray(), StringComparer.Ordinal);
    }

    /// <summary>The ISCN edition, e.g. <c>2016</c>.</summary>
    public string Edition { get; }

    /// <summary>All entries: symbols first, then banding codes, each in bundle order.</summary>
    public ImmutableArray<VocabEntry> Entries { get; }

    /// <summary>Every distinct token form.</summary>
    public IEnumerable<string> Tokens => _byToken.Keys;

    /// <summary>The entries listing <paramref name="token"/> (exact, case-sensitive); empty if none.</summary>
    public ImmutableArray<VocabEntry> Lookup(string token) =>
        _byToken.TryGetValue(token, out var entries) ? entries : [];

    /// <summary>
    /// The single entry listing <paramref name="token"/>, preferring a symbol over a banding code;
    /// <c>null</c> if none, or if several symbols share it (a <c>shared_token</c> such as <c>.</c>).
    /// </summary>
    public VocabEntry? Find(string token)
    {
        var entries = Lookup(token);
        var symbols = entries.Where(e => e.Vocabulary == "symbols").ToList();
        return symbols.Count switch
        {
            1 => symbols[0],
            0 when entries.Length == 1 => entries[0],
            _ => null,
        };
    }

    /// <summary>The vocabularies of <paramref name="edition"/> from the bundle embedded in this assembly.</summary>
    public static Vocabulary LoadEmbedded(string edition) => FromBundle(EmbeddedBundle.Document.RootElement, edition);

    /// <summary>The vocabularies of <paramref name="edition"/> from a runtime bundle stream (e.g. a test fixture).</summary>
    public static Vocabulary Load(Stream bundle, string edition)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        using var doc = JsonDocument.Parse(bundle);
        return FromBundle(doc.RootElement, edition);
    }

    internal static Vocabulary FromBundle(JsonElement root, string edition)
    {
        ArgumentNullException.ThrowIfNull(edition);
        KnowledgeBase.CheckFormat(root);

        if (!root.GetProperty("editions").TryGetProperty(edition, out var ed))
        {
            throw new KeyNotFoundException($"The KB runtime bundle has no edition '{edition}'.");
        }

        var entries = ImmutableArray.CreateBuilder<VocabEntry>();
        foreach (var key in new[] { "symbols", "banding_codes" })
        {
            if (!ed.GetProperty("vocab").TryGetProperty(key, out var vocab))
            {
                continue;
            }

            foreach (var e in vocab.GetProperty("entries").EnumerateArray())
            {
                entries.Add(new VocabEntry(
                    key,
                    e.GetProperty("label").GetString()!,
                    e.GetProperty("kind").GetString()!,
                    [.. e.GetProperty("tokens").EnumerateArray().Select(t => t.GetString()!)],
                    e.TryGetProperty("equivalent_forms", out var eq) && eq.GetBoolean(),
                    [.. e.GetProperty("meanings").EnumerateArray()
                        .SelectMany(m => m.TryGetProperty("sections", out var s) ? s.EnumerateArray().Select(x => x.GetString()!) : [])]));
            }
        }

        return new Vocabulary(edition, entries.ToImmutable());
    }
}
