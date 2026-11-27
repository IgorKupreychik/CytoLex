using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Translation;

/// <summary>
/// Renders the literal translation of a string's karyotype, in situ and microarray parts (T-058, T-093, T-105) from the edition's templates
/// (decisions → Literal translation, T-054). A thin entry point: it wires the phrase renderers around one <see cref="RenderContext"/> and
/// renders each sentence once per reading. One instance per string (the context tracks state across items).
/// </summary>
internal sealed class KaryotypeRenderer
{
    private readonly RenderContext _ctx;
    private readonly HeaderRenderer _header;
    private readonly ItemRenderer _items;
    private readonly CompositionLine _compositionLine;
    private readonly InSituRenderer _inSitu;
    private readonly ArrRenderer _arr;

    public KaryotypeRenderer(TemplateSet templates, SampleContext? context, string input)
    {
        _ctx = new RenderContext(templates, context, input);
        var chromosomes = new ChromosomePhrases(_ctx);
        var bands = new BandPhrases(_ctx);
        var composition = new CompositionRenderer(_ctx, chromosomes, bands);
        var symbols = new SymbolDescriber(_ctx, chromosomes, bands, composition);
        var derivatives = new DerivativeDescriber(_ctx, chromosomes, bands, composition, symbols);
        _header = new HeaderRenderer(_ctx);
        _items = new ItemRenderer(_ctx, bands, composition, symbols, derivatives);
        _compositionLine = new CompositionLine(_ctx, chromosomes, composition);
        _inSitu = new InSituRenderer(_ctx, bands, _items, new LocusPhrases(_ctx));
        _arr = new ArrRenderer(_ctx, bands);
    }

    /// <summary>The composition line of an item (T-086), or <c>null</c> when it has no computed composition.</summary>
    public string? CompositionLine(KaryotypeItem item)
    {
        var line = _ctx.Render(() => _compositionLine.Line(item) ?? string.Empty).Text;
        return line.Length > 0 ? line : null;
    }

    /// <summary>Whether any phrase rendered so far came from a template not yet reviewed (T-082).</summary>
    public bool UsedDraft => _ctx.UsedDraft;

    /// <summary>The summary line of a karyotype model.</summary>
    public RenderedSentence Summary(KaryotypeModel model) => _ctx.Render(() => _header.Summary(model));

    /// <summary>The sentence of a cell line's header (two or more cell lines).</summary>
    public RenderedSentence Header(KaryotypeModel model, int index) => _ctx.Render(() => _header.Header(model, index));

    /// <summary>The sentence of an item of <paramref name="line"/>; afterwards its rearrangements count as given (short forms).</summary>
    public RenderedSentence Item(KaryotypeItem item, CellLine? line)
    {
        _ctx.Line = line;
        var sentence = _ctx.Render(() =>
        {
            _ctx.BeginItem(item);
            return _items.Text(item) + ".";
        });
        _ctx.Remember(item);
        return sentence;
    }

    /// <summary>The summary sentence of an in situ segment (T-093).</summary>
    public RenderedSentence InSituSummary(FishModel model) => _ctx.Render(() => _inSitu.Summary(model));

    /// <summary>The sentence of one in situ result of cell line <paramref name="lineIndex"/> (T-093).</summary>
    public RenderedSentence InSituItem(FishModel model, int lineIndex, FishItem item)
    {
        _ctx.Line = null;
        var sentence = _ctx.Render(() => _inSitu.Item(model, lineIndex, item) + ".");
        if (item.Abnormality is { } abnormality)
        {
            _ctx.Remember(abnormality);
        }

        return sentence;
    }

    /// <summary>The summary sentence of a microarray segment (T-105).</summary>
    public RenderedSentence ArrSummary(ArrModel model) => _ctx.Render(() => _arr.Summary(model));

    /// <summary>The sentence of one microarray result (T-105).</summary>
    public RenderedSentence ArrItem(ArrAberration aberration)
    {
        _ctx.Line = null;
        return _ctx.Render(() => _arr.Item(aberration) + ".");
    }

    /// <summary>The sentence of an untranslated part.</summary>
    public RenderedSentence NotTranslated(IReadOnlyList<string> ruleIds) => _ctx.Render(() => ruleIds.Count == 0
        ? _ctx.P("summary", "not_translated_unparsed") + "."
        : _ctx.P("summary", "not_translated", ("diagnostics", string.Join(", ", ruleIds))));

    /// <summary>The sentence of a part the renderer has no wording for (a renderer gap; never a crash of validation).</summary>
    public RenderedSentence RenderFailed() => _ctx.Render(() => _ctx.P("summary", "render_failed") + ".");

    /// <summary>The sentence of a segment whose notation type has no translation.</summary>
    public RenderedSentence NotImplemented(string type) => _ctx.Render(() => _ctx.P("summary", "not_implemented", ("type", type)));

    /// <summary>The banner of a string that is not valid in the edition.</summary>
    public string Banner(string edition) => _ctx.P("summary", "partial_banner", ("edition", edition));
}
