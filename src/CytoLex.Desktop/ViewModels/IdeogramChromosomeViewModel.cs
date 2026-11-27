using System.Collections.Immutable;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Kb;
using CytoLex.Desktop.Ideograms;

namespace CytoLex.Desktop.ViewModels;

/// <summary>A marker on an ideogram: its locus and where it is drawn.</summary>
/// <param name="Locus">The locus.</param>
/// <param name="Span">Fractions of the chromosome length and the lane.</param>
public sealed record IdeogramMarker(LocusItemViewModel Locus, IdeogramSpan Span);

/// <summary>One chromosome of the Map view (T-101): its 850-level bands and the markers of its loci in the shown build.</summary>
public sealed partial class IdeogramChromosomeViewModel : ObservableObject
{
    private readonly BandSet _bands;

    /// <summary>Chromosome <paramref name="name"/> with <paramref name="loci"/> (the placed loci on it, in input order).</summary>
    public IdeogramChromosomeViewModel(BandSet bands, string name, ImmutableArray<LocusItemViewModel> loci, CoordinateBuild build)
    {
        ArgumentNullException.ThrowIfNull(bands);
        _bands = bands;
        Loci = loci;
        Name = name;
        foreach (var locus in loci)
        {
            locus.PropertyChanged += OnLocusChanged;
        }

        SetBuild(build);
    }

    /// <summary>The chromosome (<c>1</c>…<c>22</c>, <c>X</c>, <c>Y</c>).</summary>
    public string Name { get; }

    /// <summary>The loci on it.</summary>
    public ImmutableArray<LocusItemViewModel> Loci { get; }

    /// <summary>Whether a locus lies on it.</summary>
    public bool IsInvolved => !Loci.IsEmpty;

    /// <summary>The build drawn.</summary>
    public CoordinateBuild Build { get; private set; }

    /// <summary>The bands drawn, pter → qter.</summary>
    public ImmutableArray<IdeogramBand> Bands { get; private set; } = [];

    /// <summary>Its length relative to chromosome 1.</summary>
    public double RelativeLength { get; private set; }

    /// <summary>The centromere as a fraction of the length; <c>null</c> without bands.</summary>
    public double? Centromere { get; private set; }

    /// <summary>The markers drawn; loci without coordinates in the build are left out (they stay in the list).</summary>
    public ImmutableArray<IdeogramMarker> Markers { get; private set; } = [];

    /// <summary>The number of marker lanes beside the chromosome.</summary>
    public int LaneCount => Markers.IsEmpty ? 0 : Markers.Max(m => m.Span.Lane) + 1;

    /// <summary>
    /// The chromosome of the first selected locus (T-101): the Map scrolls it into view. Set again (false, then true) on
    /// every selection, so selecting a locus on the same chromosome scrolls back to it.
    /// </summary>
    [ObservableProperty]
    public partial bool IsScrollTarget { get; set; }

    /// <summary>Bumped when anything drawn changes (the build, a highlight, a selection); the ideogram control redraws.</summary>
    [ObservableProperty]
    public partial int Version { get; set; }

    /// <summary>Lays the bands and markers out for <paramref name="build"/>.</summary>
    public void SetBuild(CoordinateBuild build)
    {
        Build = build;
        var reference = build == CoordinateBuild.Grch37 ? ReferenceBuild.Grch37 : ReferenceBuild.Grch38;
        Bands = IdeogramLayout.Bands(_bands, Name, reference);
        RelativeLength = IdeogramLayout.RelativeLength(_bands, Name, reference);
        Centromere = IdeogramLayout.Centromere(Bands);
        var whole = _bands.ChromosomeRange(Name, reference);
        var placed = whole is null
            ? []
            : Loci.Select(l => (Locus: l, At: IdeogramLayout.Place(l.RangeIn(build), whole))).Where(x => x.At is not null).ToList();
        var lanes = IdeogramLayout.Lanes([.. placed.Select(p => p.At!.Value)]);
        Markers = [.. placed.Select((p, i) => new IdeogramMarker(p.Locus, new IdeogramSpan(p.At!.Value.From, p.At!.Value.To, lanes[i])))];
        OnPropertyChanged(nameof(LaneCount));
        Version++;
    }

    /// <summary>The marker at <paramref name="fraction"/> of the length in <paramref name="lane"/>; <c>null</c> when none.</summary>
    public IdeogramMarker? MarkerAt(double fraction, int lane, double tolerance)
    {
        var i = IdeogramLayout.HitTest([.. Markers.Select(m => m.Span)], fraction, lane, tolerance);
        return i < 0 ? null : Markers[i];
    }

    private void OnLocusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocusItemViewModel.IsHighlighted) or nameof(LocusItemViewModel.IsSelected))
        {
            Version++;
        }
    }
}
