using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>
/// Flattens the semantic model of karyotype segments into <see cref="EditionResult.CellLines"/> and
/// <see cref="EditionResult.Abnormalities"/> (docs/design/core.md §9, T-069).
/// </summary>
internal sealed class AbnormalityList(string input)
{
    /// <summary>The symbols of clone references (§11.1.4).</summary>
    private static readonly ImmutableHashSet<string> _cloneSymbols = ["idem", "sl", "sdl"];

    private readonly List<CellLineSummary> _cellLines = [];
    private readonly List<AbnormalityEntry> _entries = [];

    /// <summary>Adds the cell lines and items of one segment's model, in input order.</summary>
    public void Add(int segmentIndex, KaryotypeModel model)
    {
        var first = _cellLines.Count;
        foreach (var line in model.CellLines)
        {
            var lineIndex = _cellLines.Count;
            var before = _entries.Count;
            foreach (var item in line.Items)
            {
                if (Entry(item, lineIndex, _entries.Count) is { } entry)
                {
                    _entries.Add(entry);
                }
            }

            _cellLines.Add(new CellLineSummary(
                lineIndex,
                segmentIndex,
                line.Span,
                line.Span.Of(input),
                line.SeparatorBefore,
                model.Relation,
                line.Count is { IsMissing: false } c ? c.Span.Of(input) : null,
                line.Sex?.Span.Of(input),
                line.CellCount?.Cells,
                line.CellCount?.IsComposite ?? false,
                line.IsPartial,
                _entries.Count - before,
                line.BasedOn?.TargetIndex is { } target ? first + target : null));
        }
    }

    /// <summary>The cell lines and the entries, with the diagnostics inside each entry's span linked.</summary>
    public (ImmutableArray<CellLineSummary> CellLines, ImmutableArray<AbnormalityEntry> Entries) Build(ImmutableArray<Diagnostic> diagnostics) =>
        ([.. _cellLines], [.. _entries.Select(e => Link(e, diagnostics))]);

    private static AbnormalityEntry Link(AbnormalityEntry entry, ImmutableArray<Diagnostic> diagnostics) => entry with
    {
        Diagnostics = [.. Enumerable.Range(0, diagnostics.Length).Where(i => Inside(diagnostics[i].Span, entry.Span))],
        Readings = [.. entry.Readings.Select(r => Link(r, diagnostics))],
    };

    private static bool Inside(TextSpan inner, TextSpan outer) => inner.Start >= outer.Start && inner.End <= outer.End;

    private AbnormalityEntry? Entry(KaryotypeItem item, int lineIndex, int index) => item switch
    {
        WholeChromosomeChange w => Whole(w, lineIndex, index),
        Abnormality a => Symbolic(a, lineIndex, index),
        ChromosomeRegion r => Region(r, lineIndex, index),
        AlternativeItem alt => Alternatives(alt, lineIndex, index),
        BandComposition b => Composition(b, lineIndex, index),
        UnboundItem u => Basic(u, lineIndex, index, AbnormalityKind.Unrecognised, u.Text, Flags(u, null, false, false)),
        _ => null, // OmittedItems (…): nothing to list
    };

    private AbnormalityEntry Whole(WholeChromosomeChange w, int lineIndex, int index)
    {
        var kind = w.Sign switch
        {
            GainLoss.Gain => AbnormalityKind.Gain,
            GainLoss.Loss => AbnormalityKind.Loss,
            _ => AbnormalityKind.Unrecognised,
        };
        var name = (w.Chromosome.IsQuestioned ? "?" : string.Empty) + w.Chromosome.Name;
        return Basic(w, lineIndex, index, kind, SignText(w.Sign) + name, Flags(w, w, w.Chromosome.IsQuestioned, false)) with
        {
            Sign = w.Sign,
            Chromosomes = [w.Chromosome.Name],
        };
    }

    private AbnormalityEntry Symbolic(Abnormality a, int lineIndex, int index)
    {
        var first = a.Parts.IsEmpty ? null : a.Parts[0];
        var kind = a.Kind is { } k && _cloneSymbols.Contains(k) ? AbnormalityKind.CloneReference : AbnormalityKind.Structural;
        var leading = a.LeadingCount is { IsMissing: false } lc ? lc.Span.Of(input) : null;
        var shortForm = SignText(a.Sign) + leading + string.Concat(a.Parts.Select(PartShortForm));
        var breakpoints = a.Breakpoints.ToList();
        var questioned = a.IsQuestioned || a.Parts.Any(p => p.IsQuestioned);
        var uncertain = questioned || a.Parts.Any(p => p.Chromosomes.Any(c => c.IsQuestioned || c.IsUnknown)) || breakpoints.Any(IsUncertain);
        return Basic(a, lineIndex, index, kind, shortForm, Flags(a, a, questioned, uncertain) with { LeadingCount = leading }) with
        {
            Sign = a.Sign,
            Symbol = a.Kind,
            SymbolText = first is null ? null : string.Join(' ', first.Symbols.Select(s => s.Symbol)),
            Chromosomes = [.. a.Parts.SelectMany(p => p.Chromosomes).Select(ChromosomeText).Distinct(StringComparer.Ordinal)],
            Breakpoints = [.. breakpoints.Select(Summary)],
            Parts = a.Parts.Length > 1 ? [.. a.Parts.Select(Part)] : [],
            Kb = [.. a.Kb.Concat(a.Parts.SelectMany(p => p.Symbols).SelectMany(s => s.Kb)).Distinct()],
            Detailed = a.Composition?.Detailed,
        };
    }

    private AbnormalityEntry Region(ChromosomeRegion r, int lineIndex, int index)
    {
        var bands = r.Elements.Where(e => e.Band is not null).Select(e => e.Band!).ToList();
        return Basic(r, lineIndex, index, AbnormalityKind.Variant, r.Span.Of(input), Flags(r, r, r.IsQuestioned, r.IsQuestioned || bands.Any(IsUncertain))) with
        {
            Sign = r.Sign,
            Chromosomes = r.Chromosome is { } c ? [ChromosomeText(c)] : [],
            Breakpoints = [.. bands.Select(Summary)],
        };
    }

    private AbnormalityEntry Alternatives(AlternativeItem alt, int lineIndex, int index)
    {
        var readings = alt.Readings.Select(r => Entry(r, lineIndex, -1)).OfType<AbnormalityEntry>().ToImmutableArray();
        var shortForm = string.Join(" or ", readings.Select(r => r.ShortForm));
        return Basic(alt, lineIndex, index, AbnormalityKind.Alternatives, shortForm, Flags(alt, null, readings.Any(r => r.Flags.IsQuestioned), true)) with
        {
            Chromosomes = [.. readings.SelectMany(r => r.Chromosomes).Distinct(StringComparer.Ordinal)],
            Breakpoints = [.. readings.SelectMany(r => r.Breakpoints)],
            Readings = readings,
        };
    }

    private AbnormalityEntry Composition(BandComposition b, int lineIndex, int index)
    {
        var breakpoints = b.Groups.SelectMany(g => g.Breakpoints).ToList();
        return Basic(b, lineIndex, index, AbnormalityKind.Structural, b.Span.Of(input), Flags(b, null, false, breakpoints.Any(IsUncertain))) with
        {
            Chromosomes = [.. b.Groups.Select(g => g.Chromosome).OfType<ChromosomeRef>().Select(ChromosomeText).Distinct(StringComparer.Ordinal)],
            Breakpoints = [.. breakpoints.Select(Summary)],
        };
    }

    private AbnormalityEntry Basic(KaryotypeItem item, int lineIndex, int index, AbnormalityKind kind, string shortForm, AbnormalityFlags flags) => new(
        index,
        lineIndex,
        item.Span,
        item.Span.Of(input),
        kind,
        GainLoss.None,
        null,
        null,
        [],
        [],
        shortForm,
        [],
        [],
        flags,
        [],
        item.Kb);

    private static AbnormalityFlags Flags(KaryotypeItem item, SuffixedItem? suffixed, bool questioned, bool uncertain) => new(
        item.IsPartial,
        questioned,
        uncertain,
        suffixed?.Constitutional is not null,
        suffixed?.Constitutional?.IsQuestioned ?? false,
        suffixed?.Inheritance?.Symbol,
        suffixed?.Copies?.Count,
        null);

    private AbnormalityPartSummary Part(AbnormalityPart p) => new(
        p.Span,
        p.Span.Of(input),
        p.Head,
        [.. p.Chromosomes.Select(ChromosomeText)],
        [.. p.Breakpoints.Select(Summary)],
        PartShortForm(p),
        p.IsQuestioned);

    /// <summary>
    /// <c>symbol(chromosomes)</c>, with the arm appended when the part has one chromosome and all its breakpoints lie on
    /// one arm (<c>del(5q)</c>, <c>i(17q)</c>); <c>mar1</c>-style numbers kept; no parentheses without a list.
    /// </summary>
    internal static string PartShortForm(AbnormalityPart p)
    {
        var sb = new StringBuilder();
        if (p.IsQuestioned)
        {
            sb.Append('?');
        }

        sb.Append(string.Join(' ', p.Symbols.Select(s => s.Symbol)));
        if (p.SymbolNumber is { } n)
        {
            sb.Append(n.ToString(CultureInfo.InvariantCulture));
        }

        if (p.Chromosomes.IsEmpty)
        {
            return sb.ToString();
        }

        var names = p.Chromosomes.Select(ChromosomeText).ToList();
        if (p.Chromosomes.Length == 1 && p.Chromosomes[0].Arm is null)
        {
            var arms = p.Breakpoints.Select(b => b.Arm?.ToLowerInvariant()).Distinct().ToList();
            if (arms is [{ } arm])
            {
                names[0] += arm;
            }
        }

        return sb.Append('(').Append(string.Join(';', names)).Append(')').ToString();
    }

    private BreakpointSummary Summary(Breakpoint b) => new(
        b.Span,
        FullText(b),
        b.Chromosome?.Name,
        b.Arm,
        b.BandNumber,
        b.Landmark,
        b.Name,
        b.IsQuestioned,
        b.RangeEnd is { } end ? FullText(end) : null,
        [.. b.Alternatives.Select(FullText)]);

    private static string FullText(Breakpoint b) =>
        b.Chromosome is { IsUnknown: false } c && b.Landmark != "?" ? c.Name + b.Text : b.Text;

    private static bool IsUncertain(Breakpoint b) =>
        b.IsQuestioned || b.Landmark == "?" || b.RangeEnd is not null || !b.Alternatives.IsEmpty;

    private static string ChromosomeText(ChromosomeRef c) => (c.IsQuestioned ? "?" : string.Empty) + c.Name + c.Arm;

    private static string SignText(GainLoss sign) => sign switch
    {
        GainLoss.Gain => "+",
        GainLoss.Loss => "–",
        _ => string.Empty,
    };
}
