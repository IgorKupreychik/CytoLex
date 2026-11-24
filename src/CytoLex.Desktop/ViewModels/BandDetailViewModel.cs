using System.Collections.Immutable;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using CytoLex.Desktop.Rules;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The detail of one band in the rule browser (T-084): levels, stain, book status, coordinates in both builds (the same text
/// as the translation's band tooltip, <see cref="CoordinateText.Band"/>), parent and sub-band links and the cited band
/// exceptions that name it.
/// </summary>
public sealed class BandDetailViewModel
{
    public BandDetailViewModel(BandDetail detail, Action<string>? openBand)
    {
        ArgumentNullException.ThrowIfNull(detail);
        Detail = detail;
        var band = detail.Band;
        Header = $"ISCN {detail.Edition} · chromosome {band.Chromosome}, {(band.Arm == "p" ? "short" : "long")} arm ({band.Arm})";
        LevelsText = band.Levels.IsDefaultOrEmpty
            ? "Band levels: none of its own (the name of a group of sub-bands)"
            : "Band levels: " + string.Join(", ", band.Levels);
        StainText = detail.Stain is { } stain ? "Stain: " + stain : null;
        BookText = !band.Book
            ? "Not in the book's idiograms: a genome-assembly band kept valid by a band exception."
            : detail.Coordinates.IsBookOnly
                ? "Book-only band: printed in the book's idiograms, without NCBI coordinates."
                : null;
        Coordinates =
        [
            CoordinateText.Band(detail.Coordinates, CoordinateBuild.Grch38),
            CoordinateText.Band(detail.Coordinates, CoordinateBuild.Grch37),
        ];
        Parent = band.Parent is { } parent ? new BandLinkViewModel(parent, openBand) : null;
        Children = [.. detail.Children.Select(c => new BandLinkViewModel(c, openBand))];
        Exceptions = [.. detail.Exceptions.Select(x => new BandDecisionViewModel(x))];
    }

    /// <summary>The chip colour of bands (neutral, not a severity).</summary>
    public static IBrush KindBrush { get; } = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(0x6B, 0x5B, 0x95));

    /// <summary>The band.</summary>
    public BandDetail Detail { get; }

    /// <summary>The KB ID shown, <c>band:5q13</c>.</summary>
    public string Id => "band:" + Detail.Name;

    /// <summary>The band name.</summary>
    public string Name => Detail.Name;

    /// <summary><c>ISCN 2016 · chromosome 5, long arm (q)</c>.</summary>
    public string Header { get; }

    /// <summary>The band levels the name exists at.</summary>
    public string LevelsText { get; }

    /// <summary>The stain, if known.</summary>
    public string? StainText { get; }

    /// <summary>Whether <see cref="StainText"/> is set.</summary>
    public bool HasStain => StainText is not null;

    /// <summary>A note for a band that isn't in the book's idiograms or has no coordinates.</summary>
    public string? BookText { get; }

    /// <summary>Whether <see cref="BookText"/> is set.</summary>
    public bool HasBookText => BookText is not null;

    /// <summary>The coordinates in GRCh38, then GRCh37.</summary>
    public ImmutableArray<string> Coordinates { get; }

    /// <summary>The enclosing band, if any.</summary>
    public BandLinkViewModel? Parent { get; }

    /// <summary>Whether <see cref="Parent"/> is set.</summary>
    public bool HasParent => Parent is not null;

    /// <summary>The sub-bands.</summary>
    public ImmutableArray<BandLinkViewModel> Children { get; }

    /// <summary>Whether there are sub-bands.</summary>
    public bool HasChildren => !Children.IsEmpty;

    /// <summary>The band exceptions that name the band.</summary>
    public ImmutableArray<BandDecisionViewModel> Exceptions { get; }

    /// <summary>Whether there are band exceptions.</summary>
    public bool HasExceptions => !Exceptions.IsEmpty;
}

/// <summary>A link to another band in the band detail (parent, sub-band).</summary>
public sealed partial class BandLinkViewModel(string name, Action<string>? open)
{
    /// <summary>The band name.</summary>
    public string Name { get; } = name;

    /// <summary>Opens the band in the browser.</summary>
    [RelayCommand]
    private void Open() => open?.Invoke(Name);
}

/// <summary>One band exception in the band detail.</summary>
public sealed class BandDecisionViewModel(BandDecision exception)
{
    /// <summary>The exception.</summary>
    public BandDecision Exception { get; } = exception;

    /// <summary><c>Level 850 · region 6p24 · the book wins</c>.</summary>
    public string Title { get; } = $"Level {exception.Level} · region {exception.Region} · "
        + (exception.Resolution == "book" ? "the book wins" : "NCBI kept");

    /// <summary>The names on each side (and the kept ones).</summary>
    public string Names { get; } = Join(exception);

    /// <summary>The decision, in words.</summary>
    public string Note => Exception.Note;

    /// <summary>The rendered book citation.</summary>
    public string Citation => Exception.Citation;

    private static string Join(BandDecision x)
    {
        var parts = new List<string>
        {
            "Book: " + (x.Book.IsDefaultOrEmpty ? "–" : string.Join(", ", x.Book)),
            "NCBI: " + (x.Ncbi.IsDefaultOrEmpty ? "–" : string.Join(", ", x.Ncbi)),
        };
        if (!x.Keep.IsDefaultOrEmpty)
        {
            parts.Add("kept: " + string.Join(", ", x.Keep));
        }

        return string.Join(" · ", parts);
    }
}
