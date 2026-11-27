using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Kb;

/// <summary>One phrase of a template: neutral text, plus one text per context where the context changes the meaning.</summary>
/// <param name="When">The variant name; <c>null</c> for a template with one form.</param>
/// <param name="Text">The neutral wording with <c>{slot}</c> placeholders.</param>
/// <param name="Constitutional">The constitutional reading, if the meaning depends on the context.</param>
/// <param name="Neoplastic">The acquired (neoplastic) reading, if the meaning depends on the context.</param>
public sealed record TemplateForm(string? When, string Text, string? Constitutional, string? Neoplastic)
{
    /// <summary>The form has per-context readings.</summary>
    public bool HasReadings => Constitutional is not null || Neoplastic is not null;
}

/// <summary>A phrase template of the literal translation (curated/translation/&lt;version&gt;/templates.&lt;language&gt;.yaml, T-057).</summary>
/// <param name="Construct">The construct of the semantic model it renders (<c>deletion</c>, <c>summary</c>, …).</param>
/// <param name="Key">The KB entry the phrase renders (a rule or vocab item).</param>
/// <param name="Slots">The placeholders the renderer fills.</param>
/// <param name="Forms">The phrase, or one phrase per variant.</param>
/// <param name="ReviewChecked">A person approved the wording (T-082); otherwise the text is a draft.</param>
public sealed record TranslationTemplate(
    string Construct,
    KbRef Key,
    ImmutableArray<string> Slots,
    ImmutableArray<TemplateForm> Forms,
    bool ReviewChecked)
{
    /// <summary>The form called <paramref name="when"/> (or the only form when <paramref name="when"/> is <c>null</c>); <c>null</c> if none.</summary>
    public TemplateForm? Form(string? when) =>
        when is null ? (Forms.Length == 1 ? Forms[0] : null) : Forms.FirstOrDefault(f => f.When == when);
}

/// <summary>The translation templates of one edition and language, by construct.</summary>
public sealed class TemplateSet
{
    private readonly FrozenDictionary<string, TranslationTemplate> _byConstruct;

    internal TemplateSet(string language, ImmutableArray<TranslationTemplate> templates)
    {
        Language = language;
        Templates = templates;
        _byConstruct = templates.ToFrozenDictionary(t => t.Construct, StringComparer.Ordinal);
    }

    /// <summary>The language, e.g. <c>en</c>.</summary>
    public string Language { get; }

    /// <summary>Every template, in file order.</summary>
    public ImmutableArray<TranslationTemplate> Templates { get; }

    /// <summary>The template of <paramref name="construct"/>; <c>null</c> if none.</summary>
    public TranslationTemplate? For(string construct) => _byConstruct.GetValueOrDefault(construct);

    /// <summary>Reads the <c>translation</c> object of an edition in the runtime bundle (<c>{ templates: { en: {…} } }</c>).</summary>
    internal static ImmutableDictionary<string, TemplateSet> FromBundle(JsonElement translation)
    {
        var result = ImmutableDictionary.CreateBuilder<string, TemplateSet>(StringComparer.Ordinal);
        if (translation.ValueKind != JsonValueKind.Object || !translation.TryGetProperty("templates", out var byLanguage))
        {
            return result.ToImmutable();
        }

        foreach (var lang in byLanguage.EnumerateObject())
        {
            var list = lang.Value.ValueKind == JsonValueKind.Object ? lang.Value.GetProperty("templates") : lang.Value;
            var templates = list.EnumerateArray().Select(ReadTemplate).ToImmutableArray();
            result[lang.Name] = new TemplateSet(lang.Name, templates);
        }

        return result.ToImmutable();
    }

    private static TranslationTemplate ReadTemplate(JsonElement t) => new(
        t.GetProperty("construct").GetString()!,
        KbRef.Parse(t.GetProperty("key").GetString()!),
        t.TryGetProperty("slots", out var slots) ? [.. slots.EnumerateArray().Select(s => s.GetString()!)] : [],
        [.. t.GetProperty("forms").EnumerateArray().Select(ReadForm)],
        t.GetProperty("review").GetProperty("checked").GetBoolean());

    private static TemplateForm ReadForm(JsonElement f)
    {
        string? constitutional = null, neoplastic = null;
        if (f.TryGetProperty("readings", out var r))
        {
            constitutional = r.TryGetProperty("constitutional", out var c) ? c.GetString() : null;
            neoplastic = r.TryGetProperty("neoplastic", out var n) ? n.GetString() : null;
        }

        return new TemplateForm(
            f.TryGetProperty("when", out var w) ? w.GetString() : null,
            f.GetProperty("text").GetString()!,
            constitutional,
            neoplastic);
    }
}
