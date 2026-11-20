using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
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

    public AbnormalityItemViewModel(AbnormalityEntry entry, ImmutableArray<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Entry = entry;
        Span = entry.Span;
        Text = entry.Text;
        ShortForm = entry.ShortForm;
        KindLabel = KindLabelOf(entry);
        KindBrush = KindBrushOf(entry.Kind);
        Tags = TagsOf(entry.Chromosomes, entry.Breakpoints);
        Flags = FlagsOf(entry);
        Parts = entry.Kind == AbnormalityKind.Alternatives
            ? [.. entry.Readings.Select(r => new AbnormalityPartViewModel(r.Span, r.Text, r.ShortForm, TagsOf(r.Chromosomes, r.Breakpoints)))]
            : [.. entry.Parts.Select(p => new AbnormalityPartViewModel(p.Span, p.Text, p.ShortForm, TagsOf(p.Chromosomes, p.Breakpoints)))];
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

    /// <inheritdoc/>
    public TextSpan Span { get; }

    /// <summary>As written.</summary>
    public string Text { get; }

    /// <summary>The short form (<c>t(9;22)</c>).</summary>
    public string ShortForm { get; }

    /// <summary>Whether the short form differs from the written text (otherwise it is not shown twice).</summary>
    public bool ShowShortForm => !string.Equals(ShortForm, Text, StringComparison.Ordinal);

    /// <summary>The kind chip: <c>gain</c>, <c>loss</c>, the symbol (<c>del</c>, <c>der</c>), <c>variant</c>, <c>or</c>, <c>clone</c>, <c>?</c>.</summary>
    public string KindLabel { get; }

    /// <summary>The chip colour.</summary>
    public IBrush KindBrush { get; }

    /// <summary>Chromosome tags (<c>chr 9</c>) and band tags (<c>9q34</c>).</summary>
    public ImmutableArray<string> Tags { get; }

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
    public double Opacity => IsPartial ? 0.5 : 1.0;

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
        _ => "?",
    };

    private static IBrush KindBrushOf(AbnormalityKind kind) => kind switch
    {
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

    internal static ImmutableArray<FlagViewModel> FlagsOf(AbnormalityEntry entry)
    {
        var f = entry.Flags;
        var flags = ImmutableArray.CreateBuilder<FlagViewModel>();
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

    /// <summary>The groups of <paramref name="result"/>, one per cell line.</summary>
    public static ImmutableArray<CellLineGroupViewModel> From(EditionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var many = result.CellLines.Length > 1;
        var groups = ImmutableArray.CreateBuilder<CellLineGroupViewModel>();
        var donor = false;
        int? segment = null;
        foreach (var line in result.CellLines)
        {
            // Transplant chimera: the cell lines before // are the recipient's, those after it the donor's.
            donor = segment == line.SegmentIndex && (donor || line.SeparatorBefore == CellLineSeparator.DoubleSlash);
            segment = line.SegmentIndex;
            var relation = line.Relation switch
            {
                CellLineRelation.Mosaic => "mos",
                CellLineRelation.Chimera => "chi",
                CellLineRelation.Transplant => donor ? "donor" : "recipient",
                _ => null,
            };
            groups.Add(new CellLineGroupViewModel(
                HeaderOf(line, many, relation),
                [.. result.Abnormalities.Where(a => a.CellLine == line.Index).Select(a => new AbnormalityItemViewModel(a, result.Diagnostics))]));
        }

        return groups.ToImmutable();
    }

    internal static string HeaderOf(CellLineSummary line, bool numbered, string? relation)
    {
        var parts = new List<string>();
        var title = numbered ? $"Cell line {line.Index + 1}" : "Cell line";
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
