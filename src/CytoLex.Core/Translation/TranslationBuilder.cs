using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Translation;

/// <summary>
/// Builds the <see cref="TranslationResult"/> of one edition (T-058; in situ segments T-093, microarray segments T-105) from the segments' semantic models: the summary,
/// one item per translated part (the item list of the golden file, T-057), KB links with titles, the diagnostics inside
/// each part, and the optional coordinates. Parts with recovered or skipped input are not translated.
/// </summary>
internal static class TranslationBuilder
{
    /// <summary>The template language of the pilot.</summary>
    public const string Language = "en";

    /// <summary>The translation of <paramref name="segments"/>; <c>null</c> when the edition has no templates.</summary>
    public static TranslationResult? Build(
        IEditionKb kb,
        string input,
        IReadOnlyList<(Segment Segment, SegmentModel? Model)> segments,
        ImmutableArray<Diagnostic> diagnostics,
        ContextInfo context,
        bool isValid,
        bool includeCoordinates)
    {
        if (!kb.Translation.TryGetValue(Language, out var templates))
        {
            return null;
        }

        var renderer = new KaryotypeRenderer(templates, context.Effective, input);
        var items = ImmutableArray.CreateBuilder<TranslationItem>();
        RenderedSentence? summary = null;
        RenderedSentence? notImplemented = null;
        var summaryLinks = new List<KbLink>();
        var untranslated = false;

        foreach (var (segment, model) in segments)
        {
            if (model is FishModel fish)
            {
                summary = Join(summary, renderer.InSituSummary(fish));
                summaryLinks.AddRange(fish.Kb.Concat(Refs(fish.Prefixes)).Distinct().Select(r => new KbLink(r, Title(kb, r))));
                untranslated |= AddInSituParts(renderer, kb, input, fish, diagnostics, includeCoordinates, items);
                continue;
            }

            if (model is ArrModel arr)
            {
                summary = Join(summary, renderer.ArrSummary(arr));
                summaryLinks.AddRange(arr.Kb.Concat(Refs(new MeaningNode?[] { arr.Build }.OfType<MeaningNode>())).Distinct().Select(r => new KbLink(r, Title(kb, r))));
                untranslated |= AddArrParts(renderer, kb, input, arr, diagnostics, includeCoordinates, items);
                continue;
            }

            if (model is not KaryotypeModel karyotype)
            {
                var sentence = renderer.NotImplemented(segment.Type.ToKbName());
                notImplemented ??= sentence;
                items.Add(Untranslated(segment.Span, input, sentence, diagnostics));
                untranslated = true;
                continue;
            }

            summary = Join(summary, renderer.Summary(karyotype));
            summaryLinks.AddRange(SummaryLinks(kb, karyotype));

            foreach (var (span, nodes, line, index) in Parts(karyotype))
            {
                var inside = Inside(diagnostics, span);
                var partial = nodes.Any(n => n.IsPartial || n is UnboundItem);
                if (partial)
                {
                    var ids = inside.Select(i => diagnostics[i].RuleId).Distinct(StringComparer.Ordinal).ToList();
                    items.Add(new TranslationItem(span, Text(input, span), renderer.NotTranslated(ids).Text, null, Links(kb, nodes), false, false, inside, null));
                    untranslated = true;
                    continue;
                }

                var sentence = RenderPart(renderer, karyotype, nodes, line, index);
                items.Add(new TranslationItem(
                    span,
                    Text(input, span),
                    sentence.Text,
                    sentence.Readings,
                    Links(kb, nodes),
                    true,
                    sentence.IsDraft,
                    inside,
                    includeCoordinates ? Coordinates(kb, nodes) : null,
                    RenderCompositionLine(renderer, nodes)));
            }
        }

        summary ??= notImplemented ?? renderer.NotImplemented("?");
        var partialResult = untranslated || !isValid;
        var basis = context.Effective is null ? ReadingBasis.Unknown : context.Hint is not null ? ReadingBasis.Hint : ReadingBasis.Inferred;
        var s = summary.Value;
        return new TranslationResult(
            Language,
            new TranslationSummary(s.Text, s.Readings, [.. summaryLinks.DistinctBy(l => l.Ref)], s.IsDraft),
            items.ToImmutable(),
            partialResult,
            isValid ? null : renderer.Banner(kb.Edition),
            basis,
            renderer.UsedDraft);
    }

    /// <summary>
    /// Adds the parts of an in situ segment (T-093): one per result, in input order, then the text the parser skipped.
    /// Returns whether any part was left untranslated.
    /// </summary>
    private static bool AddInSituParts(
        KaryotypeRenderer renderer,
        IEditionKb kb,
        string input,
        FishModel model,
        ImmutableArray<Diagnostic> diagnostics,
        bool includeCoordinates,
        ImmutableArray<TranslationItem>.Builder items)
    {
        var untranslated = false;
        foreach (var (span, nodes, index) in Parts(model))
        {
            var inside = Inside(diagnostics, span);
            if (nodes.Any(n => n.IsPartial || n is UnboundItem) || nodes is not [FishItem item])
            {
                var ids = inside.Select(i => diagnostics[i].RuleId).Distinct(StringComparer.Ordinal).ToList();
                items.Add(new TranslationItem(span, Text(input, span), renderer.NotTranslated(ids).Text, null, Links(kb, nodes), false, false, inside, null));
                untranslated = true;
                continue;
            }

            RenderedSentence sentence;
            try
            {
                sentence = renderer.InSituItem(model, index, item);
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or InvalidOperationException or NullReferenceException)
            {
                sentence = renderer.RenderFailed();
            }

            items.Add(new TranslationItem(
                span,
                Text(input, span),
                sentence.Text,
                sentence.Readings,
                Links(kb, nodes),
                true,
                sentence.IsDraft,
                inside,
                includeCoordinates ? Coordinates(kb, nodes) : null,
                item.Abnormality is { } abnormality ? RenderCompositionLine(renderer, [abnormality]) : null));
        }

        return untranslated;
    }

    /// <summary>
    /// Adds the parts of a microarray segment (T-105): one per result, in input order, then the text the parser skipped.
    /// Returns whether any part was left untranslated.
    /// </summary>
    private static bool AddArrParts(
        KaryotypeRenderer renderer,
        IEditionKb kb,
        string input,
        ArrModel model,
        ImmutableArray<Diagnostic> diagnostics,
        bool includeCoordinates,
        ImmutableArray<TranslationItem>.Builder items)
    {
        var untranslated = false;
        foreach (var (span, nodes) in Parts(model))
        {
            var inside = Inside(diagnostics, span);
            if (nodes.Any(n => n.IsPartial || n is UnboundItem) || nodes is not [ArrAberration aberration])
            {
                var ids = inside.Select(i => diagnostics[i].RuleId).Distinct(StringComparer.Ordinal).ToList();
                items.Add(new TranslationItem(span, Text(input, span), renderer.NotTranslated(ids).Text, null, Links(kb, nodes), false, false, inside, null));
                untranslated = true;
                continue;
            }

            RenderedSentence sentence;
            try
            {
                sentence = renderer.ArrItem(aberration);
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or InvalidOperationException or NullReferenceException)
            {
                sentence = renderer.RenderFailed();
            }

            items.Add(new TranslationItem(
                span,
                Text(input, span),
                sentence.Text,
                sentence.Readings,
                Links(kb, nodes),
                true,
                sentence.IsDraft,
                inside,
                includeCoordinates ? Coordinates(kb, nodes) : null));
        }

        return untranslated;
    }

    /// <summary>The translated parts of a microarray model (T-105), in input order: one per result, then the text the parser skipped.</summary>
    public static IEnumerable<(TextSpan Span, IReadOnlyList<MeaningNode> Nodes)> Parts(ArrModel model)
    {
        foreach (var aberration in model.Aberrations)
        {
            yield return (aberration.Span, [aberration]);
        }

        foreach (var unbound in model.Unbound)
        {
            yield return (unbound.Span, [unbound]);
        }
    }

    /// <summary>
    /// The translated parts of an in situ model (T-093), in input order: one per result (its span includes the cells),
    /// with the index of its cell line; then the text the parser skipped (untranslated).
    /// </summary>
    public static IEnumerable<(TextSpan Span, IReadOnlyList<MeaningNode> Nodes, int Line)> Parts(FishModel model)
    {
        for (var index = 0; index < model.CellLines.Length; index++)
        {
            foreach (var item in model.CellLines[index].Items)
            {
                yield return (item.Span, [item], index);
            }
        }

        foreach (var unbound in model.Unbound)
        {
            yield return (unbound.Span, [unbound], -1);
        }
    }

    /// <summary>Two summary sentences joined with a space, readings included (a karyotype and its in situ segments).</summary>
    private static RenderedSentence Join(RenderedSentence? first, RenderedSentence second)
    {
        if (first is not { } a)
        {
            return second;
        }

        ContextReadings? readings = a.Readings is null && second.Readings is null
            ? null
            : new ContextReadings(
                (a.Readings?.Constitutional ?? a.Text) + " " + (second.Readings?.Constitutional ?? second.Text),
                (a.Readings?.Acquired ?? a.Text) + " " + (second.Readings?.Acquired ?? second.Text));
        return new RenderedSentence(a.Text + " " + second.Text, readings, a.IsDraft || second.IsDraft);
    }

    /// <summary>
    /// Renders one part. A gap in the renderer (an unexpected shape of a valid model) must not break validation: the part
    /// then says it has no wording yet; the KB-example sweep in the tests keeps this from happening for book strings.
    /// </summary>
    private static RenderedSentence RenderPart(KaryotypeRenderer renderer, KaryotypeModel model, IReadOnlyList<MeaningNode> nodes, CellLine? line, int index)
    {
        try
        {
            return nodes is [KaryotypeItem item] ? renderer.Item(item, line) : renderer.Header(model, index);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or InvalidOperationException or NullReferenceException)
        {
            return renderer.RenderFailed();
        }
    }

    /// <summary>The composition line of a one-item part (T-086); a renderer gap leaves it out.</summary>
    private static string? RenderCompositionLine(KaryotypeRenderer renderer, IReadOnlyList<MeaningNode> nodes)
    {
        try
        {
            return nodes is [KaryotypeItem item] ? renderer.CompositionLine(item) : null;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or InvalidOperationException or NullReferenceException)
        {
            return null;
        }
    }

    /// <summary>
    /// The translated parts of a karyotype model, in input order (the golden file's item list): with two or more cell
    /// lines, each line's header (count, ploidy, sex chromosomes) followed by its items; with one, only its items.
    /// Text the parser skipped between items is a part too (untranslated).
    /// </summary>
    public static IEnumerable<(TextSpan Span, IReadOnlyList<MeaningNode> Nodes, CellLine? Line, int Index)> Parts(KaryotypeModel model)
    {
        var several = model.CellLines.Length >= 2;
        for (var index = 0; index < model.CellLines.Length; index++)
        {
            var line = model.CellLines[index];
            if (several)
            {
                MeaningNode?[] header = [line.Count, line.Ploidy, line.Sex];
                var parts = header.OfType<MeaningNode>().ToList();
                if (parts.Count > 0)
                {
                    var start = parts[0].Span.Start;
                    var end = parts[^1].Span.Start + parts[^1].Span.Length;
                    yield return (new TextSpan(start, end - start), parts, line, index);
                }
            }

            foreach (var item in line.Items)
            {
                yield return (item.Span, [item], line, index);
            }
        }

        foreach (var unbound in model.Unbound)
        {
            yield return (unbound.Span, [unbound], null, -1);
        }
    }

    /// <summary>The KB refs of nodes and everything below them, in first-seen order.</summary>
    public static IReadOnlyList<KbRef> Refs(IEnumerable<MeaningNode> nodes) =>
        [.. nodes.SelectMany(n => n.DescendantsAndSelf()).SelectMany(n => n.Kb).Distinct()];

    /// <summary>The KB links of the summary: the model's own refs, its prefixes and (one cell line) the line's header.</summary>
    private static ImmutableArray<KbLink> SummaryLinks(IEditionKb kb, KaryotypeModel model)
    {
        IEnumerable<MeaningNode> nodes = model.Prefixes;
        if (model.CellLines is [var line])
        {
            nodes = nodes.Concat(new MeaningNode?[] { line.Count, line.Ploidy, line.Sex, line.CellCount }.OfType<MeaningNode>());
        }

        return [.. model.Kb.Concat(Refs(nodes)).Distinct().Select(r => new KbLink(r, Title(kb, r)))];
    }

    private static ImmutableArray<KbLink> Links(IEditionKb kb, IEnumerable<MeaningNode> nodes) =>
        [.. Refs(nodes).Select(r => new KbLink(r, Title(kb, r)))];

    private static string Title(IEditionKb kb, KbRef r) => r.Kind switch
    {
        KbRefKind.Rule => kb.Rule(r.Id)?.Title ?? r.Id,
        KbRefKind.Vocab => kb.Vocab.ByRefId(r.Id)?.Title ?? r.Id,
        _ => r.Id,
    };

    private static TranslationItem Untranslated(TextSpan span, string input, RenderedSentence sentence, ImmutableArray<Diagnostic> diagnostics) =>
        new(span, Text(input, span), sentence.Text, null, [], false, sentence.IsDraft, Inside(diagnostics, span), null);

    private static string Text(string input, TextSpan span) => input.Substring(span.Start, span.Length);

    private static ImmutableArray<int> Inside(ImmutableArray<Diagnostic> diagnostics, TextSpan span) =>
        [.. diagnostics.Select((d, i) => (d, i))
            .Where(x => x.d.Span.Start >= span.Start && x.d.Span.Start + x.d.Span.Length <= span.Start + span.Length)
            .Select(x => x.i)];

    /// <summary>The bands of a part with their coordinates, and the sizes of its one-chromosome segments.</summary>
    private static ItemCoordinates Coordinates(IEditionKb kb, IReadOnlyList<MeaningNode> nodes)
    {
        var bands = ImmutableArray.CreateBuilder<BandCoordinates>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in nodes.SelectMany(n => n.DescendantsAndSelf()).OfType<Breakpoint>())
        {
            if (b.Name is { } name && seen.Add(name))
            {
                bands.Add(Coordinate(kb, name));
            }
        }

        var segments = ImmutableArray.CreateBuilder<SegmentSize>();
        foreach (var part in nodes.SelectMany(n => n.DescendantsAndSelf()).OfType<AbnormalityPart>())
        {
            if (part.Head is not ("del" or "dup" or "inv" or "trp" or "qdp" or "r" or "ins") || part.IsDetailed)
            {
                continue;
            }

            foreach (var group in part.Groups.Where(g => g.Breakpoints.Length >= 2))
            {
                var (from, to) = part.Head == "ins" && part.Chromosomes.Length == 1 && group.Breakpoints.Length == 3
                    ? (group.Breakpoints[1], group.Breakpoints[2])
                    : (group.Breakpoints[0], group.Breakpoints[^1]);
                if (from.Name is { } f && to.Name is { } t)
                {
                    var a = kb.Bands?.Find(f);
                    var z = kb.Bands?.Find(t);
                    segments.Add(new SegmentSize(f, t, Size(a?.Grch37, z?.Grch37), Size(a?.Grch38, z?.Grch38)));
                }
            }
        }

        return new ItemCoordinates(bands.ToImmutable(), segments.ToImmutable());
    }

    private static BandCoordinates Coordinate(IEditionKb kb, string name)
    {
        var band = kb.Bands?.Find(name);
        return new BandCoordinates(name, band?.Grch37, band?.Grch38, band is { Book: true, Grch37: null, Grch38: null });
    }

    private static long? Size(GenomicRange? a, GenomicRange? b) =>
        a is null || b is null ? null : Math.Max(a.End, b.End) - Math.Min(a.Start, b.Start) + 1;
}
