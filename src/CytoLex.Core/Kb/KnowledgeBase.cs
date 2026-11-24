using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using CytoLex.Core.Context;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Kb;

/// <summary>The KB the validator reads at runtime: one <see cref="IEditionKb"/> per edition in the bundle (docs/design/core.md §6).</summary>
public interface IKnowledgeBase
{
    /// <summary>The editions the KB has data for, oldest first (e.g. <c>2016</c>).</summary>
    IReadOnlyList<string> Editions { get; }

    /// <summary>The KB of <paramref name="edition"/>; <c>null</c> if the KB has no data for it.</summary>
    IEditionKb? ForEdition(string edition);
}

/// <summary>The runtime KB of one ISCN edition: rule metadata, vocabularies, bands.</summary>
public interface IEditionKb
{
    /// <summary>The ISCN edition, e.g. <c>2016</c>.</summary>
    string Edition { get; }

    /// <summary>Every rule of the edition, in bundle order.</summary>
    IReadOnlyList<KbRule> Rules { get; }

    /// <summary>The rule with <paramref name="id"/>; <c>null</c> if the edition has none.</summary>
    KbRule? Rule(string id);

    /// <summary>The printed vocabularies (ch. 3 symbols, Table 2 banding codes).</summary>
    Vocabulary Vocab { get; }

    /// <summary>The band names; <c>null</c> when the bundle was built without <c>bands.yaml</c>.</summary>
    BandSet? Bands { get; }

    /// <summary>The literal-translation templates by language (T-057/T-058); empty when the bundle has none.</summary>
    IReadOnlyDictionary<string, TemplateSet> Translation { get; }
}

/// <summary>Loads the runtime KB bundle (<c>build/kb-runtime.json</c>, format in kb/README.md).</summary>
public static class KnowledgeBase
{
    private static readonly Lazy<IKnowledgeBase> _embedded = new(() => FromBundle(EmbeddedBundle.Document.RootElement));

    /// <summary>The KB embedded in this assembly at build time (loaded once; thread-safe).</summary>
    public static IKnowledgeBase LoadEmbedded() => _embedded.Value;

    /// <summary>A KB read from a runtime bundle stream (e.g. a test fixture).</summary>
    /// <exception cref="InvalidDataException">The bundle's <c>format_version</c> is not supported.</exception>
    public static IKnowledgeBase Load(Stream bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        using var doc = JsonDocument.Parse(bundle);
        return FromBundle(doc.RootElement);
    }

    /// <summary>Throws unless the bundle's <c>format_version</c> is <see cref="Vocabulary.SupportedFormatVersion"/>.</summary>
    internal static void CheckFormat(JsonElement root)
    {
        var version = root.GetProperty("format_version").GetInt32();
        if (version != Vocabulary.SupportedFormatVersion)
        {
            throw new InvalidDataException(
                $"KB runtime bundle format_version {version} is not supported (expected {Vocabulary.SupportedFormatVersion}); rebuild with 'uv run --directory tools kb build'.");
        }
    }

    private static LoadedKnowledgeBase FromBundle(JsonElement root)
    {
        CheckFormat(root);
        var editions = new List<EditionKb>();
        foreach (var ed in root.GetProperty("editions").EnumerateObject())
        {
            var rules = ed.Value.GetProperty("rules").EnumerateArray().Select(r => ReadRule(ed.Name, r)).ToImmutableArray();
            var bands = ed.Value.TryGetProperty("bands", out var b) ? BandSet.FromBundle(b) : null;
            var translation = ed.Value.TryGetProperty("translation", out var t)
                ? TemplateSet.FromBundle(t)
                : ImmutableDictionary<string, TemplateSet>.Empty;
            editions.Add(new EditionKb(ed.Name, rules, Vocabulary.FromBundle(root, ed.Name), bands, translation));
        }

        editions.Sort((a, b) => string.CompareOrdinal(a.Edition, b.Edition));
        return new LoadedKnowledgeBase(editions);
    }

    private static KbRule ReadRule(string edition, JsonElement r)
    {
        var review = r.GetProperty("review");
        return new KbRule(
            r.GetProperty("id").GetString()!,
            edition,
            r.GetProperty("section").GetString()!,
            r.GetProperty("title").GetString()!,
            ParseSeverity(r.GetProperty("severity").GetString()!),
            r.GetProperty("statement").GetString()!,
            [.. r.GetProperty("applies_to").EnumerateArray().Select(a => a.GetString()!)],
            r.TryGetProperty("context", out var ctx)
                ? [.. ctx.EnumerateArray().Select(c => MapContext(c.GetString()!)).OfType<SampleContext>().Distinct()]
                : [],
            r.TryGetProperty("fix", out var fix)
                ? new RuleFixHint(
                    fix.GetProperty("level").GetInt32(),
                    OptionalString(fix, "kind"),
                    OptionalString(fix, "hint"),
                    fix.TryGetProperty("auto", out var auto) && auto.GetBoolean())
                : null,
            review.GetProperty("needs_review").GetBoolean(),
            OptionalString(review, "note"),
            [.. r.GetProperty("cite").EnumerateArray().Select(ReadCitation)]);
    }

    private static Citation ReadCitation(JsonElement c) => new(
        c.GetProperty("section").GetString()!,
        c.GetProperty("page").GetInt32(),
        OptionalString(c, "para") ?? OptionalString(c, "example") ?? OptionalString(c, "float"),
        c.GetProperty("rendered").GetString()!);

    private static Severity ParseSeverity(string s) => s switch
    {
        "error" => Severity.Error,
        "warning" => Severity.Warning,
        "info" => Severity.Info,
        _ => throw new InvalidDataException($"Unknown rule severity '{s}' in the KB runtime bundle."),
    };

    /// <summary>KB <c>context</c> → sample context; <c>meiotic</c> is a notation type, not a sample context (core.md §3.3).</summary>
    private static SampleContext? MapContext(string c) => c switch
    {
        "constitutional" => SampleContext.Constitutional,
        "neoplastic" => SampleContext.Acquired,
        _ => null,
    };

    private static string? OptionalString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private sealed class LoadedKnowledgeBase(List<EditionKb> editions) : IKnowledgeBase
    {
        private readonly FrozenDictionary<string, EditionKb> _byEdition = editions.ToFrozenDictionary(e => e.Edition, StringComparer.Ordinal);

        public IReadOnlyList<string> Editions { get; } = [.. editions.Select(e => e.Edition)];

        public IEditionKb? ForEdition(string edition) => _byEdition.GetValueOrDefault(edition);
    }

    private sealed class EditionKb(
        string edition,
        ImmutableArray<KbRule> rules,
        Vocabulary vocab,
        BandSet? bands,
        ImmutableDictionary<string, TemplateSet> translation) : IEditionKb
    {
        private readonly FrozenDictionary<string, KbRule> _byId = rules.ToFrozenDictionary(r => r.Id, StringComparer.Ordinal);

        public string Edition => edition;

        public IReadOnlyList<KbRule> Rules => rules;

        public Vocabulary Vocab => vocab;

        public BandSet? Bands => bands;

        public IReadOnlyDictionary<string, TemplateSet> Translation => translation;

        public KbRule? Rule(string id) => _byId.GetValueOrDefault(id);
    }
}
