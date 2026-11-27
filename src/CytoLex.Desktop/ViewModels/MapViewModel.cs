using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Kb;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The Map view of one edition (T-101): the ideograms of the edition's band data (850 level) with the loci of the string
/// (<see cref="EditionResult.Loci"/>) as markers, the locus list and the unplaced probes. Marks follow the editor and the
/// abnormality list: <see cref="HighlightSpan"/> (mouse) and <see cref="SelectSpan"/> (selection).
/// </summary>
public sealed partial class MapViewModel : ObservableObject
{
    /// <summary>The map of <paramref name="result"/> on <paramref name="bands"/>; spans mapped to the editor by <paramref name="toEditor"/>.</summary>
    public MapViewModel(EditionResult result, BandSet? bands, CoordinateBuild build, Func<TextSpan, TextSpan>? toEditor = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        All = [.. result.Loci.Select(l => new LocusItemViewModel(l, toEditor))];
        Loci = [.. All.Where(i => i.Locus.IsPlaced)];
        Unplaced = [.. All.Where(i => !i.Locus.IsPlaced)];
        HasBands = bands is not null && !bands.Chromosomes.IsEmpty;
        AllChromosomes = bands is null
            ? []
            : [.. bands.Chromosomes.Select(c => new IdeogramChromosomeViewModel(bands, c, [.. Loci.Where(l => l.Locus.Chromosome == c)], build))];
        Build = build;
    }

    /// <summary>Every locus, placed and unplaced, in input order.</summary>
    public ImmutableArray<LocusItemViewModel> All { get; }

    /// <summary>The placed loci, in input order.</summary>
    public ImmutableArray<LocusItemViewModel> Loci { get; }

    /// <summary>Loci without a position in the string (probe names; probe table: T-103).</summary>
    public ImmutableArray<LocusItemViewModel> Unplaced { get; }

    /// <summary>Whether there are placed loci.</summary>
    public bool HasPlaced => !Loci.IsEmpty;

    /// <summary>Whether there are unplaced loci.</summary>
    public bool HasUnplaced => !Unplaced.IsEmpty;

    /// <summary>The unplaced section's header.</summary>
    public string UnplacedHeader => $"Unplaced ({Unplaced.Length}): no position in the string and no row in the probe table";

    /// <summary>Whether the string has loci at all.</summary>
    public bool HasLoci => !All.IsEmpty;

    /// <summary>Whether the edition has band data to draw.</summary>
    public bool HasBands { get; }

    /// <summary>The tab header: <c>Map (3)</c>.</summary>
    public string Header => All.IsEmpty ? "Map" : $"Map ({All.Length})";

    /// <summary>All chromosomes of the band data (1–22, X, Y).</summary>
    public ImmutableArray<IdeogramChromosomeViewModel> AllChromosomes { get; }

    /// <summary>The chromosomes drawn: all, or only those with loci.</summary>
    public IReadOnlyList<IdeogramChromosomeViewModel> Chromosomes =>
        ShowAllChromosomes ? AllChromosomes : [.. AllChromosomes.Where(c => c.IsInvolved)];

    /// <summary>Draw all chromosomes (default) or only the involved ones.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Chromosomes))]
    public partial bool ShowAllChromosomes { get; set; } = true;

    /// <summary>The build drawn.</summary>
    public CoordinateBuild Build { get; private set; }

    /// <summary>Lays the markers out for <paramref name="build"/> (both builds are in the result: no re-validation).</summary>
    public void SetBuild(CoordinateBuild build)
    {
        Build = build;
        foreach (var chromosome in AllChromosomes)
        {
            chromosome.SetBuild(build);
        }
    }

    /// <summary>Marks the loci the mouse points at: those inside <paramref name="span"/>, or whose span holds it; <c>null</c> clears.</summary>
    public void HighlightSpan(TextSpan? span)
    {
        foreach (var locus in All)
        {
            locus.IsHighlighted = span is { } s && Related(locus.Span, s);
        }
    }

    /// <summary>Marks the one locus under the mouse (a marker or a row); <c>null</c> clears.</summary>
    public void Highlight(LocusItemViewModel? item)
    {
        foreach (var locus in All)
        {
            locus.IsHighlighted = ReferenceEquals(locus, item);
        }
    }

    /// <summary>
    /// Marks the selected loci (those inside <paramref name="span"/>, or whose span holds it) and makes the chromosome of the
    /// first of them the scroll target (<see cref="IdeogramChromosomeViewModel.IsScrollTarget"/>).
    /// </summary>
    public void SelectSpan(TextSpan span)
    {
        foreach (var locus in All)
        {
            locus.IsSelected = Related(locus.Span, span);
        }

        var target = Loci.FirstOrDefault(l => l.IsSelected)?.Locus.Chromosome;
        foreach (var chromosome in AllChromosomes)
        {
            chromosome.IsScrollTarget = false;
        }

        if (AllChromosomes.FirstOrDefault(c => c.Name == target) is { } scrollTo)
        {
            scrollTo.IsScrollTarget = true;
        }
    }

    /// <summary>The span of the narrowest locus at editor <paramref name="offset"/>; <c>null</c> when none.</summary>
    public TextSpan? SpanAt(int offset) =>
        All.Where(l => offset >= l.Span.Start && offset < l.Span.End).OrderBy(l => l.Span.Length).Select(l => (TextSpan?)l.Span).FirstOrDefault();

    // A locus inside the span (a card, a translation part), or a non-empty span inside the locus (a breakpoint of a deletion).
    private static bool Related(TextSpan locus, TextSpan span) =>
        (locus.Start >= span.Start && locus.End <= span.End && span.Length > 0)
        || (span.Start >= locus.Start && span.End <= locus.End && span.Length > 0);
}
