using System.Collections.Immutable;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Kb;
using CytoLex.Desktop.Rules;

namespace CytoLex.Desktop.ViewModels;

/// <summary>An entry of the Bands tab's chromosome filter.</summary>
/// <param name="Chromosome">The chromosome (<c>1</c>–<c>22</c>, <c>X</c>, <c>Y</c>); <c>null</c> = every chromosome.</param>
/// <param name="Label">The text shown.</param>
public sealed record ChromosomeChoice(string? Chromosome, string Label)
{
    /// <summary>"All chromosomes".</summary>
    public static ChromosomeChoice All { get; } = new(null, "All chromosomes");

    /// <inheritdoc/>
    public override string ToString() => Label;
}

/// <summary>An entry of the Bands tab's band-level filter.</summary>
/// <param name="Level">The band level (300, 400, 550, 700, 850); <c>null</c> = every band name.</param>
/// <param name="Label">The text shown.</param>
public sealed record LevelChoice(int? Level, string Label)
{
    /// <summary>"All levels": every band name, including the names of groups of sub-bands.</summary>
    public static LevelChoice All { get; } = new(null, "All levels");

    /// <inheritdoc/>
    public override string ToString() => Label;
}

/// <summary>
/// The Bands tab (T-084): the band set of one edition from <c>build/kb.sqlite</c> (<see cref="RuleStore.Bands"/>, the same
/// bands the core embeds), listed pter → qter per chromosome and indented by nesting (<c>1p36</c> › <c>1p36.3</c> ›
/// <c>1p36.33</c>), with filters for the chromosome, the band level (the band set at that resolution), bands not in the
/// book's idiograms, and a name prefix; the selected band's detail is <see cref="Detail"/>. <see cref="OpenBand"/> is the
/// target of the translation's band links. A missing or stale database only disables this tab (<see cref="ErrorText"/>).
/// </summary>
public partial class BandBrowserViewModel : ObservableObject, IDisposable
{
    private readonly RuleStore? _store;
    private ImmutableArray<BandRowViewModel> _all = [];
    private bool _suspendRefresh;

    /// <summary>The browser over the database at <paramref name="databasePath"/>.</summary>
    /// <param name="databasePath">The <c>kb.sqlite</c> file.</param>
    /// <param name="defaultEdition">The edition shown first if the database has it (else the newest one).</param>
    public BandBrowserViewModel(string databasePath, string? defaultEdition)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        try
        {
            _store = RuleStore.Open(databasePath);
        }
        catch (RuleStoreException ex)
        {
            ErrorText = ex.Message;
            Editions = [];
            return;
        }

        Editions = _store.Editions;
        _suspendRefresh = true;
        SelectedEdition = defaultEdition is not null && Editions.Contains(defaultEdition) ? defaultEdition : Editions.LastOrDefault();
        _suspendRefresh = false;
        Refresh();
    }

    /// <summary>Why the tab is unavailable (database missing or of another format); <c>null</c> when it works.</summary>
    public string? ErrorText { get; }

    /// <summary>Whether <see cref="ErrorText"/> is set.</summary>
    public bool HasError => ErrorText is not null;

    /// <summary>Whether the tab works.</summary>
    public bool IsAvailable => ErrorText is null;

    /// <summary>The editions in the database.</summary>
    public ImmutableArray<string> Editions { get; }

    /// <summary>The edition browsed; changing it reloads the bands.</summary>
    [ObservableProperty]
    public partial string? SelectedEdition { get; set; }

    /// <summary>The chromosome filter entries ("All chromosomes" first).</summary>
    [ObservableProperty]
    public partial ImmutableArray<ChromosomeChoice> Chromosomes { get; private set; } = [ChromosomeChoice.All];

    /// <summary>The chromosome filter.</summary>
    [ObservableProperty]
    public partial ChromosomeChoice? SelectedChromosome { get; set; } = ChromosomeChoice.All;

    /// <summary>The band-level filter entries ("All levels" first).</summary>
    [ObservableProperty]
    public partial ImmutableArray<LevelChoice> Levels { get; private set; } = [LevelChoice.All];

    /// <summary>The band-level filter: only the band names that exist at that level.</summary>
    [ObservableProperty]
    public partial LevelChoice? SelectedLevel { get; set; } = LevelChoice.All;

    /// <summary>Only the bands that the book's idiograms don't show (genome-assembly bands kept by a band decision).</summary>
    [ObservableProperty]
    public partial bool NotInBookOnly { get; set; }

    /// <summary>A band name or its start (<c>5q1</c>, <c>xp22</c>), case-insensitive.</summary>
    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>The bands shown.</summary>
    [ObservableProperty]
    public partial ImmutableArray<BandRowViewModel> Items { get; private set; } = [];

    /// <summary>The band selected in the list; its detail is <see cref="Detail"/>.</summary>
    [ObservableProperty]
    public partial BandRowViewModel? SelectedItem { get; set; }

    /// <summary>The detail of the selected band.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial BandDetailViewModel? Detail { get; set; }

    /// <summary>Whether a band is shown.</summary>
    public bool HasDetail => Detail is not null;

    /// <summary><c>1165 band(s)</c>, <c>12 of 1165 band(s)</c>, or a note on a band that could not be opened.</summary>
    [ObservableProperty]
    public partial string ResultText { get; set; } = string.Empty;

    /// <summary>
    /// Shows band <paramref name="name"/> of <paramref name="edition"/> (a translation link, a parent or sub-band link): switches
    /// the edition, clears the filters if they hide the band, and selects it.
    /// </summary>
    /// <returns>Whether the band exists in the database.</returns>
    public bool OpenBand(string edition, string name)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(name);
        if (_store is null)
        {
            return false;
        }

        if (!Editions.Contains(edition) || _store.Band(edition, name) is null)
        {
            ResultText = $"{name} is not a band of the ISCN {edition} KB.";
            return false;
        }

        SelectedEdition = edition; // reloads when it changes
        if (!Items.Any(i => i.Name == name))
        {
            _suspendRefresh = true;
            Query = string.Empty;
            SelectedChromosome = ChromosomeChoice.All;
            SelectedLevel = LevelChoice.All;
            NotInBookOnly = false;
            _suspendRefresh = false;
            Refresh();
        }

        SelectedItem = Items.FirstOrDefault(i => i.Name == name);
        return SelectedItem is not null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _store?.Dispose();
        GC.SuppressFinalize(this);
    }

    partial void OnSelectedEditionChanged(string? value)
    {
        if (_store is null || value is null)
        {
            return;
        }

        var bands = _store.Bands(value);
        var depth = new Dictionary<string, int>(StringComparer.Ordinal);
        var parents = bands.ToDictionary(b => b.Name, b => b.Parent, StringComparer.Ordinal);
        _all = [.. bands.Select(b => new BandRowViewModel(b, DepthOf(b.Name, parents, depth)))];

        var keepChromosome = SelectedChromosome?.Chromosome;
        var keepLevel = SelectedLevel?.Level;
        var suspended = _suspendRefresh;
        _suspendRefresh = true;
        Chromosomes = [ChromosomeChoice.All, .. bands.Select(b => b.Chromosome).Distinct(StringComparer.Ordinal).Select(c => new ChromosomeChoice(c, "Chromosome " + c))];
        Levels = [LevelChoice.All, .. bands.SelectMany(b => b.Levels).Distinct().Order().Select(l => new LevelChoice(l, $"{l}-band level"))];
        SelectedChromosome = Chromosomes.FirstOrDefault(c => c.Chromosome == keepChromosome) ?? ChromosomeChoice.All;
        SelectedLevel = Levels.FirstOrDefault(l => l.Level == keepLevel) ?? LevelChoice.All;
        _suspendRefresh = suspended;
        Refresh();
    }

    partial void OnSelectedChromosomeChanged(ChromosomeChoice? value) => Refresh();

    partial void OnSelectedLevelChanged(LevelChoice? value) => Refresh();

    partial void OnNotInBookOnlyChanged(bool value) => Refresh();

    partial void OnQueryChanged(string value) => Refresh();

    partial void OnSelectedItemChanged(BandRowViewModel? value)
    {
        var detail = value is null || _store is null || SelectedEdition is not { } edition ? null : _store.Band(edition, value.Name);
        Detail = detail is null ? null : new BandDetailViewModel(detail, OpenRelated);
    }

    // Nesting depth: the number of ancestors (a parent outside the band set ends the chain).
    private static int DepthOf(string name, Dictionary<string, string?> parents, Dictionary<string, int> memo)
    {
        if (memo.TryGetValue(name, out var known))
        {
            return known;
        }

        var parent = parents.GetValueOrDefault(name);
        var depth = parent is not null && parents.ContainsKey(parent) ? DepthOf(parent, parents, memo) + 1 : 0;
        memo[name] = depth;
        return depth;
    }

    private void OpenRelated(string name)
    {
        if (SelectedEdition is { } edition)
        {
            OpenBand(edition, name);
        }
    }

    private void Refresh()
    {
        if (_suspendRefresh || _store is null || SelectedEdition is null)
        {
            return;
        }

        var selected = SelectedItem?.Name;
        var chromosome = SelectedChromosome?.Chromosome;
        var level = SelectedLevel?.Level;
        var query = Query.Trim();
        Items =
        [
            .. _all.Where(r =>
                (chromosome is null || r.Band.Chromosome == chromosome)
                && (level is null || r.Band.Levels.Contains(level.Value))
                && (!NotInBookOnly || !r.Band.Book)
                && (query.Length == 0 || r.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))),
        ];
        var filtered = chromosome is not null || level is not null || NotInBookOnly || query.Length > 0;
        ResultText = _all.IsEmpty
            ? "No bands in this KB database: run 'uv run --directory tools kb build', then 'dotnet build'."
            : filtered ? $"{Items.Length} of {_all.Length} band(s)" : $"{_all.Length} band(s)";
        SelectedItem = Items.FirstOrDefault(i => i.Name == selected)
            ?? (query.Length > 0
                ? Items.FirstOrDefault(i => string.Equals(i.Name, query, StringComparison.OrdinalIgnoreCase)) ?? Items.FirstOrDefault()
                : null);
    }
}

/// <summary>One row of the Bands tab's list.</summary>
public sealed class BandRowViewModel(Band band, int depth)
{
    /// <summary>The band.</summary>
    public Band Band { get; } = band;

    /// <summary>The band name.</summary>
    public string Name => Band.Name;

    /// <summary>The nesting depth (0 = a band with no enclosing band).</summary>
    public int Depth { get; } = depth;

    /// <summary>The left margin that shows the nesting.</summary>
    public Thickness Indent { get; } = new(depth * 16, 0, 0, 0);

    /// <summary><c>levels 300, 400</c>, or that the name only groups sub-bands.</summary>
    public string Summary { get; } = band.Levels.IsDefaultOrEmpty
        ? "group of sub-bands"
        : "levels " + string.Join(", ", band.Levels);

    /// <summary>Whether the book's idiograms don't show the band.</summary>
    public bool IsNotInBook => !Band.Book;

    /// <summary>Whether the band has no coordinates (book-only band).</summary>
    public bool HasNoCoordinates => Band.Grch37 is null && Band.Grch38 is null;
}
