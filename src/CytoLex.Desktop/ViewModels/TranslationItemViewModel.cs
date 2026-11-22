using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Translation;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.ViewModels;

/// <summary>The genome build whose coordinates the translation view shows (T-059; both come with the result).</summary>
public enum CoordinateBuild
{
    /// <summary>GRCh38 (the default).</summary>
    Grch38,

    /// <summary>GRCh37.</summary>
    Grch37,
}

/// <summary>
/// A KB link of a translation part (T-059): the entry's title, its KB ID on hover. Rule and vocab links open the entry in the
/// rule browser; band links are not navigable (decided with the user, 2026-11-22) and show the band's coordinates on hover.
/// </summary>
public sealed partial class KbLinkViewModel : ObservableObject
{
    private readonly Action<KbRef>? _open;
    private readonly BandCoordinates? _band;

    public KbLinkViewModel(KbLink link, BandCoordinates? band, CoordinateBuild build, Action<KbRef>? open)
    {
        ArgumentNullException.ThrowIfNull(link);
        Link = link;
        _band = band;
        _open = open;
        Tip = TipOf(build);
    }

    /// <summary>The link.</summary>
    public KbLink Link { get; }

    /// <summary>The KB entry.</summary>
    public KbRef Ref => Link.Ref;

    /// <summary>The title shown.</summary>
    public string Title => Link.Title;

    /// <summary>Whether the link opens the rule browser (rule or vocab item).</summary>
    public bool IsNavigable => Link.Ref.Kind != KbRefKind.Band;

    /// <summary>Whether the link is a band (a plain chip).</summary>
    public bool IsBand => !IsNavigable;

    /// <summary>The tooltip: the KB ID (<c>rule:DEL.FORMAT</c>), plus the coordinates of a band in the chosen build.</summary>
    [ObservableProperty]
    public partial string Tip { get; private set; }

    /// <summary>Re-renders the band tooltip for another build.</summary>
    internal void SetBuild(CoordinateBuild build) => Tip = TipOf(build);

    /// <summary>Opens the entry in the rule browser.</summary>
    [RelayCommand]
    private void Open()
    {
        if (IsNavigable)
        {
            _open?.Invoke(Link.Ref);
        }
    }

    private string TipOf(CoordinateBuild build)
    {
        var id = Link.Ref.ToString();
        if (Link.Ref.Kind != KbRefKind.Band)
        {
            return id + " · open in the rule browser";
        }

        return id + " · " + (_band is null ? "coordinates: switch on \"Genomic coordinates\"" : CoordinateText.Band(_band, build));
    }
}

/// <summary>A link from a translation part to a diagnostic inside it (T-059); clicking selects the diagnostic.</summary>
public sealed partial class DiagnosticLinkViewModel(int index, Diagnostic diagnostic, Action<int>? select)
{
    /// <summary>The index into <c>EditionResult.Diagnostics</c>.</summary>
    public int Index { get; } = index;

    /// <summary>The rule ID shown.</summary>
    public string Label { get; } = diagnostic.RuleId;

    /// <summary>The severity and message.</summary>
    public string Tip { get; } = $"{DiagnosticItemViewModel.Label(diagnostic.Severity)}: {diagnostic.Message}";

    /// <summary>The severity colour.</summary>
    public IBrush Brush { get; } = SeverityBrushes.For(diagnostic.Severity);

    /// <summary>Selects the diagnostic in the list (which selects its span in the editor).</summary>
    [RelayCommand]
    private void Select() => select?.Invoke(Index);
}

/// <summary>The summary line or one part of a translation (T-059), with its links; a part also has a span and coordinates.</summary>
public sealed partial class TranslationItemViewModel : ObservableObject, ISpanItem
{
    private readonly ItemCoordinates? _coordinates;

    /// <summary>A part of the string.</summary>
    public TranslationItemViewModel(
        TranslationItem item,
        ImmutableArray<Diagnostic> diagnostics,
        CoordinateBuild build,
        Action<KbRef>? openKbRef,
        Action<int>? selectDiagnostic)
    {
        ArgumentNullException.ThrowIfNull(item);
        IsSummary = false;
        Span = item.Span;
        Text = item.Text;
        Sentence = item.Sentence;
        IsTranslated = item.IsTranslated;
        IsDraft = item.IsDraft;
        (ConstitutionalReading, AcquiredReading) = ReadingsOf(item.Readings);
        _coordinates = item.Coordinates;
        var bands = item.Coordinates?.Bands.ToDictionary(b => b.Band, StringComparer.Ordinal);
        Links = [.. item.Links.Select(l => new KbLinkViewModel(l, l.Ref.Kind == KbRefKind.Band ? bands?.GetValueOrDefault(l.Ref.Id) : null, build, openKbRef))];
        Diagnostics =
        [
            .. item.DiagnosticIndices
                .Where(i => i >= 0 && i < diagnostics.Length)
                .Select(i => new DiagnosticLinkViewModel(i, diagnostics[i], selectDiagnostic)),
        ];
        CoordinateLines = CoordinateText.Lines(_coordinates, build);
    }

    /// <summary>The summary line (no span).</summary>
    public TranslationItemViewModel(TranslationSummary summary, CoordinateBuild build, Action<KbRef>? openKbRef)
    {
        ArgumentNullException.ThrowIfNull(summary);
        IsSummary = true;
        Span = default;
        Text = string.Empty;
        Sentence = summary.Text;
        IsTranslated = true;
        IsDraft = summary.IsDraft;
        (ConstitutionalReading, AcquiredReading) = ReadingsOf(summary.Readings);
        Links = [.. summary.Links.Select(l => new KbLinkViewModel(l, null, build, openKbRef))];
        Diagnostics = [];
        CoordinateLines = [];
    }

    /// <summary>Whether this is the summary line.</summary>
    public bool IsSummary { get; }

    /// <inheritdoc/>
    public TextSpan Span { get; }

    /// <summary>The part as written (empty for the summary).</summary>
    public string Text { get; }

    /// <summary>Whether the part text is shown (not for the summary).</summary>
    public bool HasText => !IsSummary;

    /// <summary>The sentence (neutral when both readings are given); for an untranslated part, why not.</summary>
    public string Sentence { get; }

    /// <summary>Whether the part was translated (<c>false</c>: recovered or skipped input; shown faded).</summary>
    public bool IsTranslated { get; }

    /// <summary>The card opacity: faded when not translated.</summary>
    public double Opacity => IsTranslated ? 1.0 : 0.6;

    /// <summary>The sentence style: italic when not translated.</summary>
    public FontStyle SentenceStyle => IsTranslated ? FontStyle.Normal : FontStyle.Italic;

    /// <summary>Built from a template not yet reviewed (T-082).</summary>
    public bool IsDraft { get; }

    /// <summary><c>If constitutional: …</c> when the context is unknown and changes the meaning; else <c>null</c>.</summary>
    public string? ConstitutionalReading { get; }

    /// <summary><c>If acquired: …</c> when the context is unknown and changes the meaning; else <c>null</c>.</summary>
    public string? AcquiredReading { get; }

    /// <summary>Whether both readings are shown.</summary>
    public bool HasReadings => ConstitutionalReading is not null;

    /// <summary>The KB links, in first-seen order.</summary>
    public ImmutableArray<KbLinkViewModel> Links { get; }

    /// <summary>Whether there are links.</summary>
    public bool HasLinks => !Links.IsEmpty;

    /// <summary>The diagnostics inside the part.</summary>
    public ImmutableArray<DiagnosticLinkViewModel> Diagnostics { get; }

    /// <summary>Whether there are diagnostics inside the part.</summary>
    public bool HasDiagnostics => !Diagnostics.IsEmpty;

    /// <summary>The bands and segment sizes in the chosen build; empty unless coordinates were requested.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCoordinates))]
    public partial ImmutableArray<string> CoordinateLines { get; private set; }

    /// <summary>Whether coordinate lines are shown.</summary>
    public bool HasCoordinates => !CoordinateLines.IsDefaultOrEmpty;

    /// <summary>Whether the part is marked (hovered here or at its span in the editor).</summary>
    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }

    /// <summary>Re-renders the coordinates and band tooltips for another build.</summary>
    internal void SetBuild(CoordinateBuild build)
    {
        CoordinateLines = CoordinateText.Lines(_coordinates, build);
        foreach (var link in Links)
        {
            link.SetBuild(build);
        }
    }

    private static (string?, string?) ReadingsOf(ContextReadings? readings) =>
        readings is null ? (null, null) : ("If constitutional: " + readings.Constitutional, "If acquired: " + readings.Acquired);
}

/// <summary>Text forms of the optional genomic detail (T-059): <c>5q13: chr5:70,900,001–77,100,000 (GRCh38)</c>, <c>≈ 78.5 Mb</c>.</summary>
public static class CoordinateText
{
    /// <summary>The build's label, <c>GRCh38</c>.</summary>
    public static string Label(CoordinateBuild build) => build == CoordinateBuild.Grch37 ? "GRCh37" : "GRCh38";

    /// <summary>One band: its range in <paramref name="build"/>, or why there is none.</summary>
    public static string Band(BandCoordinates band, CoordinateBuild build)
    {
        ArgumentNullException.ThrowIfNull(band);
        var range = build == CoordinateBuild.Grch37 ? band.Grch37 : band.Grch38;
        if (range is null)
        {
            return band.IsBookOnly ? $"{band.Band}: no coordinates (book-only band)" : $"{band.Band}: no {Label(build)} coordinates";
        }

        return $"{band.Band}: chr{ChromosomeOf(band.Band)}:{Number(range.Start)}–{Number(range.End)} ({Label(build)})";
    }

    /// <summary>One segment: its approximate size in <paramref name="build"/>.</summary>
    public static string Segment(SegmentSize segment, CoordinateBuild build)
    {
        ArgumentNullException.ThrowIfNull(segment);
        var bases = build == CoordinateBuild.Grch37 ? segment.Grch37Bases : segment.Grch38Bases;
        return $"{segment.From}→{segment.To}: " + (bases is { } b ? $"≈ {Size(b)} ({Label(build)})" : "size unknown (a band has no coordinates)");
    }

    /// <summary>Every band, then every segment; empty without coordinates.</summary>
    public static ImmutableArray<string> Lines(ItemCoordinates? coordinates, CoordinateBuild build) =>
        coordinates is null
            ? []
            : [.. coordinates.Bands.Select(b => Band(b, build)), .. coordinates.Segments.Select(s => Segment(s, build))];

    /// <summary><c>78.5 Mb</c>, <c>850 kb</c>, <c>120 bp</c>.</summary>
    public static string Size(long bases) => bases switch
    {
        >= 1_000_000 => (bases / 1_000_000.0).ToString("0.0", CultureInfo.InvariantCulture) + " Mb",
        >= 1_000 => (bases / 1_000.0).ToString("0", CultureInfo.InvariantCulture) + " kb",
        _ => bases.ToString(CultureInfo.InvariantCulture) + " bp",
    };

    private static string Number(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    // The chromosome of a band name: everything before the arm letter (5q13 → 5, Xp22.3 → X).
    private static string ChromosomeOf(string band)
    {
        var arm = band.IndexOfAny(['p', 'q']);
        return arm > 0 ? band[..arm] : band;
    }
}
