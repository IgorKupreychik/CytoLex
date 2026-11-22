using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using static CytoLex.Core.Translation.Phrasing;

namespace CytoLex.Core.Translation;

/// <summary>A template or form the renderer needs is missing from the KB templates (a KB/renderer mismatch).</summary>
public sealed class TranslationTemplateException(string message) : InvalidOperationException(message);

/// <summary>A rendered sentence, plus both readings when the context is unknown and a phrase depends on it.</summary>
internal readonly record struct RenderedSentence(string Text, ContextReadings? Readings, bool IsDraft);

/// <summary>
/// The description of an abnormality: the head (sentence start), the rest of the sentence, the noun used after
/// "An additional …" (default: the head with a lower-case first letter), the plural used after a leading count, and
/// whether the item is always a gain (markers: no "additional").
/// </summary>
internal sealed record AbnormalityDescription(string Head, string Rest = "", string? GainNoun = null, string? Plural = null, bool AlwaysGained = false);

/// <summary>
/// The state of one translation (T-058), shared by the phrase renderers: the templates, the reading mode of the sentence
/// being rendered, the draft and readings flags, the homologues of the current item, the rearrangements written with
/// breakpoints so far, and the current cell line. Every phrase comes from <see cref="P"/>. One instance per string.
/// </summary>
internal sealed class RenderContext(TemplateSet templates, SampleContext? context, string input)
{
    private Mode _mode;
    private bool _readingsUsed;
    private bool _draft;

    // Rearrangements written with breakpoints so far (symbol + chromosome list), for "as given at its first mention".
    private readonly HashSet<string> _withBreakpoints = new(StringComparer.Ordinal);

    private enum Mode
    {
        Neutral,
        Constitutional,
        Acquired,
    }

    /// <summary>Whether any phrase rendered so far came from a template not yet reviewed (T-082).</summary>
    public bool UsedDraft { get; private set; }

    /// <summary>Chromosome names written both underlined and plain in the current item (homologue distinction, ch. 4 / T-042).</summary>
    public IReadOnlySet<string> Homologues { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The cell line of the item being rendered (clone references, §11.1.4).</summary>
    public CellLine? Line { get; set; }

    /// <summary>
    /// Renders <paramref name="build"/> once for the known context, or neutral and (if a phrase depends on the context)
    /// once per context. <paramref name="build"/> may only change state through <see cref="P"/> and <see cref="BeginItem"/>.
    /// </summary>
    public RenderedSentence Render(Func<string> build)
    {
        _draft = false;
        _readingsUsed = false;
        if (context is { } known)
        {
            _mode = known == SampleContext.Constitutional ? Mode.Constitutional : Mode.Acquired;
            var text = build();
            return new RenderedSentence(text, null, _draft);
        }

        _mode = Mode.Neutral;
        var neutral = build();
        ContextReadings? readings = null;
        if (_readingsUsed)
        {
            _mode = Mode.Constitutional;
            var constitutional = build();
            _mode = Mode.Acquired;
            var acquired = build();
            _mode = Mode.Neutral;
            readings = new ContextReadings(constitutional, acquired);
        }

        return new RenderedSentence(neutral, readings, _draft);
    }

    /// <summary>The text of a template form with its slots filled, in the reading of the current mode.</summary>
    public string P(string construct, string? when, params (string Slot, string Value)[] slots)
    {
        var template = templates.For(construct)
            ?? throw new TranslationTemplateException($"No translation template for construct '{construct}'.");
        var form = template.Form(when)
            ?? throw new TranslationTemplateException($"Translation template '{construct}' has no form '{when ?? "(single)"}'.");
        if (!template.ReviewChecked)
        {
            _draft = true;
            UsedDraft = true;
        }

        var text = form.Text;
        if (form.HasReadings)
        {
            _readingsUsed = true;
            text = _mode switch
            {
                Mode.Constitutional => form.Constitutional ?? text,
                Mode.Acquired => form.Neoplastic ?? text,
                _ => text,
            };
        }

        foreach (var (slot, value) in slots)
        {
            text = text.Replace("{" + slot + "}", value, StringComparison.Ordinal);
        }

        if (text.Contains('{', StringComparison.Ordinal))
        {
            throw new TranslationTemplateException($"Translation template '{construct}' form '{when}' has an unfilled slot: {text}");
        }

        return text;
    }

    /// <summary>Starts a new item: finds the homologues written both underlined and plain in it.</summary>
    public void BeginItem(MeaningNode item)
    {
        var chromosomes = item.DescendantsAndSelf().OfType<ChromosomeRef>().Where(c => !c.IsUnknown).ToList();
        Homologues = chromosomes.GroupBy(c => c.Name, StringComparer.Ordinal)
            .Where(g => g.Any(c => c.IsUnderlined) && g.Any(c => !c.IsUnderlined))
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Remembers the rearrangements of an item written with breakpoints, for later short forms (<c>der(22)t(9;22)</c>).</summary>
    public void Remember(KaryotypeItem item)
    {
        foreach (var part in item.DescendantsAndSelf().OfType<AbnormalityPart>().Where(p => !p.Groups.IsEmpty))
        {
            _withBreakpoints.Add(Key(part));
        }
    }

    /// <summary>Whether the same rearrangement (symbol and chromosomes) was written earlier with breakpoints.</summary>
    public bool GivenEarlier(AbnormalityPart part) => _withBreakpoints.Contains(Key(part));

    /// <summary><c>breakpoints not given</c>, or <c>breakpoints as given at its first mention</c> when written earlier with breakpoints.</summary>
    public string NoneGiven(AbnormalityPart part) => P("band", GivenEarlier(part) ? "as_first_given" : "none_given");

    /// <summary>A leading count: <c>3</c>, <c>1 to 5</c>, <c>about 14</c>.</summary>
    public string Count(NumberRange r) => r switch
    {
        { IsRange: true, From: { } from, To: { } to } => P("chromosome_count", "range_number", ("from", N(from)), ("to", N(to))),
        { IsRange: true, To: { } to } => P("chromosome_count", "approximate_number", ("count", N(to))),
        { From: { } n } => N(n),
        _ => "?",
    };

    /// <summary>The text of the input at a node.</summary>
    public string Written(MeaningNode n) => input.Substring(n.Span.Start, n.Span.Length);

    private static string Key(AbnormalityPart part) =>
        part.Head + "(" + string.Join(";", part.Chromosomes.Select(c => c.Name + (c.IsUnderlined ? "_" : ""))) + ")";
}
