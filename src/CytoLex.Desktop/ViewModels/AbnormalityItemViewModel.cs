using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.ViewModels;

/// <summary>Something in the abnormalities panel that selects a span of the input when clicked.</summary>
public interface ISpanItem
{
    /// <summary>The span in the input.</summary>
    TextSpan Span { get; }
}

/// <summary>A small flag glyph on an abnormality card (<c>?</c>, <c>c</c>, <c>mat</c>, <c>×2</c>) with its explanation.</summary>
/// <param name="Glyph">The glyph shown.</param>
/// <param name="Tip">The tooltip.</param>
public sealed record FlagViewModel(string Glyph, string Tip);

/// <summary>A part of a derivative (<c>der(1)</c>, <c>t(1;3)(p32;q21)</c>) or a reading of an <c>or</c> item.</summary>
/// <param name="Span">The part in the input.</param>
/// <param name="Text">As written.</param>
/// <param name="ShortForm">E.g. <c>t(1;3)</c>.</param>
/// <param name="Tags">Chromosome and band tags.</param>
public sealed record AbnormalityPartViewModel(TextSpan Span, string Text, string ShortForm, ImmutableArray<string> Tags) : ISpanItem
{
    /// <summary>The tags on one line.</summary>
    public string TagsText => string.Join(" · ", Tags);
}

/// <summary>One card of the abnormalities panel (T-069): an <see cref="AbnormalityEntry"/> rendered for display.</summary>
public sealed class AbnormalityItemViewModel : ISpanItem
{
    private static readonly IImmutableSolidColorBrush _gain = Brush(0x10, 0x7C, 0x10);
    private static readonly IImmutableSolidColorBrush _loss = Brush(0xB1, 0x46, 0xC2);
    private static readonly IImmutableSolidColorBrush _structural = Brush(0x00, 0x5A, 0x9E);
    private static readonly IImmutableSolidColorBrush _variant = Brush(0x60, 0x5E, 0x5C);
    private static readonly IImmutableSolidColorBrush _alternatives = Brush(0x8A, 0x6D, 0x00);
    private static readonly IImmutableSolidColorBrush _clone = Brush(0x03, 0x83, 0x87);
    private static readonly IImmutableSolidColorBrush _unrecognised = Brush(0x8A, 0x8A, 0x8A);

    // Arr results (T-072): one neutral colour per result kind, no gain/loss colours (the copy number is not interpreted).
    private static readonly IImmutableSolidColorBrush _arrCopyNumber = Brush(0x3B, 0x5B, 0x78);
    private static readonly IImmutableSolidColorBrush _arrOther = Brush(0x6B, 0x4E, 0x2E);

    // In situ results (T-092): one neutral colour for region results; normal results are grey and muted.
    private static readonly IImmutableSolidColorBrush _inSitu = Brush(0x2E, 0x6B, 0x5E);
    private static readonly IImmutableSolidColorBrush _normal = Brush(0x7A, 0x7A, 0x7A);

    public AbnormalityItemViewModel(AbnormalityEntry entry, ImmutableArray<Diagnostic> diagnostics)
        : this(entry, diagnostics, null)
    {
    }

    /// <summary>A card whose spans are mapped to the editor text by <paramref name="toEditor"/> (T-097: autocorrection; <c>null</c> = as they are).</summary>
    public AbnormalityItemViewModel(AbnormalityEntry entry, ImmutableArray<Diagnostic> diagnostics, Func<TextSpan, TextSpan>? toEditor)
    {
        ArgumentNullException.ThrowIfNull(entry);

        toEditor ??= static s => s;
        Entry = entry;
        Span = toEditor(entry.Span);
        Text = entry.Text;
        ShortForm = entry.ShortForm;
        Detailed = entry.Detailed;
        KindLabel = KindLabelOf(entry);
        KindBrush = KindBrushOf(entry);
        Tags = entry.Arr is { } arr ? ArrTagsOf(entry, arr) : TagsOf(entry.Chromosomes, entry.Breakpoints);
        PositionsText = entry.Arr is { } a ? PositionsOf(a) : null;
        LociText = entry.InSitu is { } s ? LociOf(s) : null;
        Flags = FlagsOf(entry);
        Parts = entry.Kind == AbnormalityKind.Alternatives
            ? [.. entry.Readings.Select(r => new AbnormalityPartViewModel(toEditor(r.Span), r.Text, r.ShortForm, TagsOf(r.Chromosomes, r.Breakpoints)))]
            : [.. entry.Parts.Select(p => new AbnormalityPartViewModel(toEditor(p.Span), p.Text, p.ShortForm, TagsOf(p.Chromosomes, p.Breakpoints)))];
        PartsHeader = entry.Kind == AbnormalityKind.Alternatives ? $"{Parts.Length} readings" : $"{Parts.Length} parts";

        var linked = entry.Diagnostics.Where(i => i >= 0 && i < diagnostics.Length).Select(i => diagnostics[i]).ToList();
        if (linked.Count > 0)
        {
            var worst = linked.Max(d => d.Severity);
            DiagnosticsBrush = SeverityBrushes.For(worst);
            DiagnosticsTip = string.Join("\n", linked.Select(d => $"{DiagnosticItemViewModel.Label(d.Severity)}: {d.RuleId} — {d.Message}"));
        }
    }

    /// <summary>The entry shown.</summary>
    public AbnormalityEntry Entry { get; }

    /// <summary>The expandable reference of an <c>idem</c>/<c>sl</c>/<c>sdl</c> card (T-070); <c>null</c> for other cards.</summary>
    public CloneReferenceViewModel? Reference { get; init; }

    /// <summary>Whether <see cref="Reference"/> is set.</summary>
    public bool HasReference => Reference is not null;

    /// <inheritdoc/>
    public TextSpan Span { get; }

    /// <summary>As written.</summary>
    public string Text { get; }

    /// <summary>The short form (<c>t(9;22)</c>).</summary>
    public string ShortForm { get; }

    /// <summary>Whether the short form differs from the written text (otherwise it is not shown twice).</summary>
    public bool ShowShortForm => !string.Equals(ShortForm, Text, StringComparison.Ordinal);

    /// <summary>The abnormality in the detailed system (T-066): <c>der(9)(9pter→9q34::22q11.2→22qter)</c>; <c>null</c> without a band composition.</summary>
    public string? Detailed { get; }

    /// <summary>Whether there is a detailed form that differs from the written text (a string already in the detailed system isn't shown twice).</summary>
    public bool ShowDetailed => Detailed is not null && !string.Equals(Detailed, Text, StringComparison.Ordinal);

    /// <summary>The kind chip: <c>gain</c>, <c>loss</c>, the symbol (<c>del</c>, <c>der</c>), <c>variant</c>, <c>or</c>, <c>clone</c>, <c>?</c>.</summary>
    public string KindLabel { get; }

    /// <summary>The chip colour.</summary>
    public IBrush KindBrush { get; }

    /// <summary>Chromosome tags (<c>chr 9</c>) and band tags (<c>9q34</c>).</summary>
    public ImmutableArray<string> Tags { get; }

    /// <summary>
    /// The nucleotide positions of an arr result (T-072): <c>GRCh38 · chr4:163,146,681–183,022,312</c>, <c>or</c> readings
    /// joined, detailed-system items with their copy numbers; <c>null</c> without positions.
    /// </summary>
    public string? PositionsText { get; }

    /// <summary>Whether there are positions to show.</summary>
    public bool HasPositions => PositionsText is not null;

    /// <summary>
    /// The loci of an in situ result (T-092), one part per locus group: <c>ABL1×2, BCR×2 · ABL1 con BCR×1</c> (a shared
    /// count is given to each locus of its group; a signal position group as written); <c>null</c> without loci.
    /// </summary>
    public string? LociText { get; }

    /// <summary>Whether there are loci to show.</summary>
    public bool HasLoci => LociText is not null;

    /// <summary>A normal in situ result (T-092): shown muted with a "normal" flag.</summary>
    public bool IsNormal => Entry.InSitu?.IsNormal ?? false;

    /// <summary>Flag glyphs.</summary>
    public ImmutableArray<FlagViewModel> Flags { get; }

    /// <summary>Whether there are flags to show.</summary>
    public bool HasFlags => !Flags.IsEmpty;

    /// <summary>The parts of a derivative, or the readings of an <c>or</c> item.</summary>
    public ImmutableArray<AbnormalityPartViewModel> Parts { get; }

    /// <summary>Whether there are parts to expand.</summary>
    public bool HasParts => !Parts.IsEmpty;

    /// <summary>The expander header, e.g. <c>3 parts</c>.</summary>
    public string PartsHeader { get; }

    /// <summary>Built from recovered input: shown faded.</summary>
    public bool IsPartial => Entry.Flags.IsPartial;

    /// <summary>The card opacity.</summary>
    public double Opacity => IsPartial ? 0.5 : IsNormal ? 0.6 : 1.0;

    /// <summary>The worst severity of the diagnostics inside the span; <c>null</c> without diagnostics.</summary>
    public IBrush? DiagnosticsBrush { get; }

    /// <summary>Whether the diagnostics dot is shown.</summary>
    public bool HasDiagnostics => DiagnosticsBrush is not null;

    /// <summary>The diagnostics inside the span, one per line.</summary>
    public string? DiagnosticsTip { get; }

    internal static string KindLabelOf(AbnormalityEntry entry) => entry.Kind switch
    {
        AbnormalityKind.Gain => "gain",
        AbnormalityKind.Loss => "loss",
        AbnormalityKind.Structural => entry.SymbolText ?? "bands",
        AbnormalityKind.Variant => "variant",
        AbnormalityKind.Alternatives => "or",
        AbnormalityKind.CloneReference => "clone",
        AbnormalityKind.ArrResult => entry.Arr?.CopyText ?? "arr",
        AbnormalityKind.InSituRegion => entry.InSitu is { } s ? NotationLabel(s.Notation) : "ish",
        _ => "?",
    };

    private static IBrush KindBrushOf(AbnormalityEntry entry) => entry.Kind switch
    {
        _ when entry.InSitu?.IsNormal ?? false => _normal,
        AbnormalityKind.InSituRegion => _inSitu,
        AbnormalityKind.ArrResult => entry.Arr?.Kind is ArrResultKind.CopyNumber or null ? _arrCopyNumber : _arrOther,
        AbnormalityKind.Gain => _gain,
        AbnormalityKind.Loss => _loss,
        AbnormalityKind.Structural => _structural,
        AbnormalityKind.Variant => _variant,
        AbnormalityKind.Alternatives => _alternatives,
        AbnormalityKind.CloneReference => _clone,
        _ => _unrecognised,
    };

    internal static ImmutableArray<string> TagsOf(ImmutableArray<string> chromosomes, ImmutableArray<BreakpointSummary> breakpoints) =>
        [
            .. chromosomes.Select(c => "chr " + c),
            .. breakpoints.Select(b => b.Text).Distinct(StringComparer.Ordinal),
        ];

    /// <summary>
    /// Arr tags: the chromosome group as written (<c>chr 1–22,X</c>) for a whole-chromosome result, otherwise the
    /// chromosomes and each region's bands as written (<c>chr 4</c>, <c>4q32.2q35.1</c>).
    /// </summary>
    internal static ImmutableArray<string> ArrTagsOf(AbnormalityEntry entry, ArrResultSummary arr)
    {
        if (arr.IsWholeChromosomes)
        {
            var close = entry.ShortForm.IndexOf(')', StringComparison.Ordinal);
            return close > 1 ? ["chr " + entry.ShortForm[1..close]] : [.. entry.Chromosomes.Select(c => "chr " + c)];
        }

        return
        [
            .. entry.Chromosomes.Select(c => "chr " + c),
            .. arr.Regions.SelectMany(r => new[] { r }.Concat(r.Alternatives)).Select(r => r.Bands).Distinct(StringComparer.Ordinal),
        ];
    }

    /// <summary>The positions line of an arr result; <c>null</c> when no region gives positions.</summary>
    internal static string? PositionsOf(ArrResultSummary arr)
    {
        var regions = arr.Regions.Where(r => !r.Positions.IsEmpty).Select(RegionPositions).ToList();
        if (regions.Count == 0)
        {
            return null;
        }

        var text = string.Join("; ", regions);
        return arr.Build is { } build ? $"{build} · {text}" : text;
    }

    private static string RegionPositions(ArrRegionSummary region)
    {
        var chromosomes = new[] { region }.Concat(region.Alternatives).Select(r => r.Chromosome).ToList();
        return string.Join(", ", region.Positions.Select(p =>
            string.Join(" or ", new[] { p }.Concat(p.Alternatives).Select((x, i) =>
                (i < chromosomes.Count && chromosomes[i] is { } c ? $"chr{c}:" : string.Empty) + PositionText(x)))));
    }

    private static string PositionText(ArrPositionSummary p)
    {
        var text = p.Start is { } s ? s.ToString("N0", CultureInfo.InvariantCulture) : "?";
        if (p.End is { } e)
        {
            text += "–" + e.ToString("N0", CultureInfo.InvariantCulture);
        }

        return p.CopyText is { } copy ? text + " " + copy : text;
    }

    /// <summary>The keyword of an in situ notation: <c>ish</c>, <c>nuc ish</c>, <c>fib ish</c>, <c>rev ish</c>, <c>cgh</c>.</summary>
    internal static string NotationLabel(NotationType notation) => notation switch
    {
        NotationType.NucIsh => "nuc ish",
        NotationType.FibIsh => "fib ish",
        NotationType.RevIsh => "rev ish",
        NotationType.Cgh => "cgh",
        _ => "ish",
    };

    /// <summary>The loci line of an in situ result; <c>null</c> when it has no loci.</summary>
    internal static string? LociOf(InSituResultSummary inSitu)
    {
        var groups = inSitu.Groups.Where(g => !g.Loci.IsEmpty).Select(GroupText).ToList();
        return groups.Count == 0 ? null : string.Join(" · ", groups);
    }

    private static string GroupText(LocusGroupSummary group)
    {
        var text = new StringBuilder();
        for (var i = 0; i < group.Loci.Length; i++)
        {
            var locus = group.Loci[i];
            text.Append(locus.Name).Append(locus.Status);
            if ((locus.CountText ?? (group.IsPosition ? null : group.SharedCount)) is { } count)
            {
                text.Append('×').Append(count);
            }

            foreach (var q in locus.Qualifiers)
            {
                text.Append(' ').Append(q);
            }

            if (i < group.Separators.Length)
            {
                text.Append(group.Separators[i] switch
                {
                    "," => ", ",
                    ";" => "; ",
                    "/" => "/",
                    "" => " ",
                    var word => $" {word} ",
                });
            }
        }

        if (group.IsPosition && group.SharedCount is { } shared)
        {
            text.Append('×').Append(shared);
        }

        foreach (var q in group.Qualifiers)
        {
            text.Append(' ').Append(q);
        }

        return text.ToString();
    }

    internal static ImmutableArray<FlagViewModel> FlagsOf(AbnormalityEntry entry)
    {
        var f = entry.Flags;
        var flags = ImmutableArray.CreateBuilder<FlagViewModel>();
        if (entry.InSitu is { } inSitu)
        {
            if (inSitu.IsNormal)
            {
                flags.Add(new("normal", $"Normal result ({inSitu.NormalRule})"));
            }

            if (inSitu.Cells is { } cells)
            {
                flags.Add(new($"[{cells.Text}]", cells.Total is null
                    ? $"Cells with this result ([{cells.Text}])"
                    : $"Cells with this result over the cells scored ([{cells.Text}])"));
            }
        }

        if (entry.Arr is { } arr)
        {
            if (arr.Proportion is { } proportion)
            {
                flags.Add(new($"[{proportion}]", $"Proportion of cells with the result ([{proportion}])"));
            }

            if (arr.Zygosity is { } zygosity)
            {
                flags.Add(new(zygosity, zygosity == "hmz" ? "Homozygous (hmz)" : "Heterozygous (htz)"));
            }

            if (arr.IsMosaic)
            {
                flags.Add(new("mos", "Mosaic (mos)"));
            }
        }

        if (f.IsQuestioned)
        {
            flags.Add(new("?", "Questionable identification (?)"));
        }
        else if (f.IsUncertain)
        {
            flags.Add(new("~", "Uncertain breakpoint or chromosome (?, ~ or or)"));
        }

        if (f.Constitutional)
        {
            flags.Add(new(f.ConstitutionalQuestioned ? "c?" : "c", f.ConstitutionalQuestioned ? "Possibly constitutional (?c)" : "Constitutional (c)"));
        }

        if (f.Inheritance is { } inh)
        {
            flags.Add(new(inh, inh switch
            {
                "mat" => "Maternal origin (mat)",
                "pat" => "Paternal origin (pat)",
                "inh" => "Inherited (inh)",
                "dn" => "De novo (dn)",
                _ => inh,
            }));
        }

        if (f.Copies is { } n)
        {
            flags.Add(new(string.Create(CultureInfo.InvariantCulture, $"×{n}"), $"{n} copies (×{n})"));
        }

        if (f.IsPartial)
        {
            flags.Add(new("partial", "Built from recovered input: see the diagnostics"));
        }

        return flags.ToImmutable();
    }

    private static ImmutableSolidColorBrush Brush(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));
}

/// <summary>A cell line of the abnormalities panel: a header and its abnormality cards.</summary>
/// <param name="Header">E.g. <c>Cell line 2 (mos) · 47,XXX · [5] · 1 abnormality</c>.</param>
/// <param name="Items">The cards.</param>
public sealed record CellLineGroupViewModel(string Header, ImmutableArray<AbnormalityItemViewModel> Items)
{
    /// <summary>Whether the cell line has no abnormalities.</summary>
    public bool IsEmpty => Items.IsEmpty;

    /// <summary>The line written out without its <c>idem</c>/<c>sl</c>/<c>sdl</c> (T-070, <see cref="CellLineSummary.FullForm"/>); <c>null</c> otherwise.</summary>
    public string? FullForm { get; init; }

    /// <summary>Whether <see cref="FullForm"/> is set.</summary>
    public bool HasFullForm => FullForm is not null;

    /// <summary>The groups of <paramref name="result"/>, one per cell line.</summary>
    public static ImmutableArray<CellLineGroupViewModel> From(EditionResult result) => From(result, null);

    /// <summary>The groups of <paramref name="result"/>, with spans mapped to the editor text by <paramref name="toEditor"/> (T-097).</summary>
    public static ImmutableArray<CellLineGroupViewModel> From(EditionResult result, Func<TextSpan, TextSpan>? toEditor)
    {
        ArgumentNullException.ThrowIfNull(result);
        var many = result.CellLines.Count(l => l.Technique == SegmentTechnique.Karyotype) > 1;
        var lineNumbers = result.CellLines.Where(l => l.Technique == SegmentTechnique.Karyotype)
            .Select((l, k) => (l.Index, Number: k + 1))
            .ToDictionary(x => x.Index, x => x.Number);
        int LineNumber(int index) => lineNumbers.TryGetValue(index, out var n) ? n : index + 1;
        var inSituLines = result.CellLines.Where(l => l.Technique == SegmentTechnique.InSitu)
            .GroupBy(l => l.SegmentIndex)
            .ToDictionary(g => g.Key, g => g.Count());
        var groups = ImmutableArray.CreateBuilder<CellLineGroupViewModel>();
        var donor = false;
        int? segment = null;
        var number = 0;
        var inSituNumber = 0;
        foreach (var line in result.CellLines)
        {
            if (line.Technique == SegmentTechnique.Arr)
            {
                groups.Add(new CellLineGroupViewModel(ArrHeaderOf(line), Cards(result, line, toEditor)));
                continue;
            }

            // Transplant chimera: the cell lines before // are the recipient's, those after it the donor's (an in situ
            // segment may start with //: donor cells only, §13.3.1 E19).
            var sameSegment = segment == line.SegmentIndex;
            donor = (sameSegment && donor) || line.SeparatorBefore == CellLineSeparator.DoubleSlash;
            inSituNumber = sameSegment ? inSituNumber + 1 : 1;
            segment = line.SegmentIndex;
            var relation = line.Relation switch
            {
                CellLineRelation.Mosaic => "mos",
                CellLineRelation.Chimera => "chi",
                CellLineRelation.Transplant => donor ? "donor" : "recipient",
                _ => null,
            };
            var cards = Cards(result, line, toEditor);
            if (line.Technique == SegmentTechnique.InSitu)
            {
                int? numbered = inSituLines[line.SegmentIndex] > 1 ? inSituNumber : null;
                var cells = cards.IsEmpty ? null : cards[^1].Entry.InSitu?.Cells?.Text;
                groups.Add(new CellLineGroupViewModel(InSituHeaderOf(line, numbered, relation, cells), cards));
                continue;
            }

            number++;
            if (line.Reference is { } reference)
            {
                var row = new CloneReferenceViewModel(line, result, LineNumber, toEditor ?? (static s => s));
                cards = [.. cards.Select(c => c.Entry.Kind == AbnormalityKind.CloneReference && c.Entry.Span == reference.Span
                    ? new AbnormalityItemViewModel(c.Entry, result.Diagnostics, toEditor) { Reference = row }
                    : c)];
            }

            groups.Add(new CellLineGroupViewModel(HeaderOf(line, many ? number : null, relation), cards) { FullForm = line.FullForm });
        }

        return groups.ToImmutable();
    }

    private static ImmutableArray<AbnormalityItemViewModel> Cards(EditionResult result, CellLineSummary line, Func<TextSpan, TextSpan>? toEditor) =>
        [.. result.Abnormalities.Where(a => a.CellLine == line.Index).Select(a => new AbnormalityItemViewModel(a, result.Diagnostics, toEditor))];

    /// <summary>The header of an in situ cell line (T-092): <c>In situ (nuc ish) · cell line 2 (donor) · [50/400] · 1 result</c>.</summary>
    /// <param name="line">The cell line.</param>
    /// <param name="number">Its number in its segment (1-based); <c>null</c> when the segment has one cell line.</param>
    /// <param name="relation"><c>mos</c>, <c>chi</c>, <c>recipient</c> or <c>donor</c>.</param>
    /// <param name="cells">The cells after the line's last result, as written.</param>
    internal static string InSituHeaderOf(CellLineSummary line, int? number, string? relation, string? cells)
    {
        var parts = new List<string> { $"In situ ({AbnormalityItemViewModel.NotationLabel(line.Notation ?? NotationType.Ish)})" };
        if (number is not null || relation is not null)
        {
            var title = number is { } n ? $"cell line {n}" : "cell line";
            parts.Add(relation is null ? title : $"{title} ({relation})");
        }

        if (cells is not null)
        {
            parts.Add($"[{cells}]");
        }

        parts.Add(line.AbnormalityCount == 1 ? "1 result" : $"{line.AbnormalityCount} results");
        return string.Join(" · ", parts);
    }

    /// <summary>The header of an arr group (T-072): <c>Microarray (arr) · GRCh38 · 3 results</c>.</summary>
    internal static string ArrHeaderOf(CellLineSummary line)
    {
        var parts = new List<string> { "Microarray (arr)" };
        if (line.Build is { } build)
        {
            parts.Add(build);
        }

        parts.Add(line.AbnormalityCount == 1 ? "1 result" : $"{line.AbnormalityCount} results");
        return string.Join(" · ", parts);
    }

    /// <param name="line">The cell line.</param>
    /// <param name="number">Its number among the karyotype cell lines (1-based); <c>null</c> when there is only one.</param>
    /// <param name="relation"><c>mos</c>, <c>chi</c>, <c>recipient</c> or <c>donor</c>.</param>
    internal static string HeaderOf(CellLineSummary line, int? number, string? relation)
    {
        var parts = new List<string>();
        var title = number is { } n ? $"Cell line {n}" : "Cell line";
        parts.Add(relation is null ? title : $"{title} ({relation})");

        var karyotype = string.Join(",", new[] { line.Count, line.Sex }.OfType<string>());
        if (karyotype.Length > 0)
        {
            parts.Add(karyotype);
        }

        if (line.Cells is { } cells)
        {
            parts.Add(line.IsComposite ? $"[cp{cells}]" : $"[{cells}]");
        }

        parts.Add(line.AbnormalityCount == 1 ? "1 abnormality" : $"{line.AbnormalityCount} abnormalities");
        return string.Join(" · ", parts);
    }
}
