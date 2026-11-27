using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Kb;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>One locus of the Map view (T-101): a row of the locus list and a marker on its ideogram.</summary>
public sealed partial class LocusItemViewModel : ObservableObject, ISpanItem
{
    // The colours of the abnormality cards: gain, loss, structural (breaks), arr copy number, in situ, normal, variant.
    private static readonly IImmutableSolidColorBrush _gain = Rgb(0x10, 0x7C, 0x10);
    private static readonly IImmutableSolidColorBrush _loss = Rgb(0xB1, 0x46, 0xC2);
    private static readonly IImmutableSolidColorBrush _break = Rgb(0x00, 0x5A, 0x9E);
    private static readonly IImmutableSolidColorBrush _copyNumber = Rgb(0x3B, 0x5B, 0x78);
    private static readonly IImmutableSolidColorBrush _signal = Rgb(0x2E, 0x6B, 0x5E);
    private static readonly IImmutableSolidColorBrush _normal = Rgb(0x7A, 0x7A, 0x7A);
    private static readonly IImmutableSolidColorBrush _variant = Rgb(0x60, 0x5E, 0x5C);

    /// <summary>A row for <paramref name="locus"/>, its span mapped to the editor by <paramref name="toEditor"/>.</summary>
    public LocusItemViewModel(LocusEntry locus, Func<TextSpan, TextSpan>? toEditor = null)
    {
        ArgumentNullException.ThrowIfNull(locus);
        Locus = locus;
        Span = (toEditor ?? (static s => s))(locus.Span);
        Place = PlaceOf(locus);
        EffectLabel = EffectLabelOf(locus);
        Brush = BrushOf(locus.Effect);
        Details = string.Join(" · ", DetailsOf(locus));
        Tip = TipOf(locus, Place, EffectLabel);
    }

    /// <summary>The core locus.</summary>
    public LocusEntry Locus { get; }

    /// <inheritdoc/>
    public TextSpan Span { get; }

    /// <summary>Where it is: <c>5q13→5q33</c>, <c>chromosome 8</c>, <c>16q</c>, <c>unplaced</c>.</summary>
    public string Place { get; }

    /// <summary>The effect chip: <c>loss</c>, <c>gain</c>, <c>break</c>, <c>×1</c>, <c>signal</c>, <c>normal</c>, <c>variant</c>.</summary>
    public string EffectLabel { get; }

    /// <summary>The effect colour (marker and chip).</summary>
    public IImmutableSolidColorBrush Brush { get; }

    /// <summary>The text or probe, cells and qualifiers on one line.</summary>
    public string Details { get; }

    /// <summary>The marker and row tooltip.</summary>
    public string Tip { get; }

    /// <summary>The index of its abnormality entry.</summary>
    public int Abnormality => Locus.Abnormality;

    /// <summary>A point (a breakpoint) rather than a region: drawn as a tick across the chromosome.</summary>
    public bool IsPoint => Locus.Effect == LocusEffect.Break;

    /// <summary>Marked by the mouse over the editor, a card or the marker.</summary>
    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }

    /// <summary>Marked by a selection (a clicked marker, row or card).</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>The coordinates drawn for <paramref name="build"/>.</summary>
    public GenomicRange? RangeIn(CoordinateBuild build) =>
        Locus.In(build == CoordinateBuild.Grch37 ? ReferenceBuild.Grch37 : ReferenceBuild.Grch38);

    internal static string PlaceOf(LocusEntry l) => l.Extent switch
    {
        LocusExtent.Unplaced => "unplaced",
        LocusExtent.Chromosome => $"chromosome {l.Chromosome}",
        LocusExtent.Arm => l.Chromosome + l.From,
        LocusExtent.Band => l.From ?? $"chromosome {l.Chromosome}",
        _ when l.To is { } to => $"{l.From}→{to}",
        _ => l.From ?? $"chromosome {l.Chromosome}",
    };

    internal static string EffectLabelOf(LocusEntry l) => l.Effect switch
    {
        LocusEffect.Gain => "gain",
        LocusEffect.Loss => "loss",
        LocusEffect.Break => "break",
        LocusEffect.CopyNumber => l.Copy ?? "copy number",
        LocusEffect.Signal => "signal",
        LocusEffect.Normal => "normal",
        _ => "variant",
    };

    private static IEnumerable<string> DetailsOf(LocusEntry l)
    {
        yield return l.Probe is { } probe ? probe + l.Copy : l.Text;
        if (l.Cells is { } cells)
        {
            yield return cells;
        }

        if (l.IsUncertain)
        {
            yield return "uncertain";
        }

        if (l.IsAlternative)
        {
            yield return "one reading of several (or)";
        }

        if (l.PositionsBuild is { } build)
        {
            yield return "positions " + build;
        }
    }

    private static IImmutableSolidColorBrush BrushOf(LocusEffect effect) => effect switch
    {
        LocusEffect.Gain => _gain,
        LocusEffect.Loss => _loss,
        LocusEffect.Break => _break,
        LocusEffect.CopyNumber => _copyNumber,
        LocusEffect.Signal => _signal,
        LocusEffect.Normal => _normal,
        _ => _variant,
    };

    private static string TipOf(LocusEntry l, string place, string effect)
    {
        var lines = new List<string> { $"{place} · {effect}", l.Text };
        if (l.PositionsBuild is { } build)
        {
            lines.Add($"Positions as written ({build}); in the other build from the bands.");
        }
        else if (l.IsPlaced)
        {
            lines.Add("Coordinates from the band data (band edges are estimates).");
        }

        if (l.Effect == LocusEffect.CopyNumber)
        {
            lines.Add("Copy number as written, not read as a gain or loss.");
        }

        if (l.UnplacedReason is { } reason)
        {
            lines.Add(char.ToUpperInvariant(reason[0]) + reason[1..] + ".");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ImmutableSolidColorBrush Rgb(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
