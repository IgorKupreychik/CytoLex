using CytoLex.Core.Kb;

namespace CytoLex.Desktop.ViewModels;

/// <summary>The band data the Map view draws (T-101): the runtime bundle embedded in the core, read once.</summary>
internal static class MapBands
{
    private static readonly Lazy<IKnowledgeBase> _kb = new(KnowledgeBase.LoadEmbedded);

    /// <summary>The bands of <paramref name="edition"/>; <c>null</c> when the edition has none.</summary>
    public static BandSet? For(string edition) => _kb.Value.ForEdition(edition)?.Bands;
}
