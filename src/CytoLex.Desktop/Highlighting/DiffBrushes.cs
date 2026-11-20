using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace CytoLex.Desktop.Highlighting;

/// <summary>
/// The colours of a fix diff (T-040): removed text on red, added text on green. Solid backgrounds with white text read
/// the same on the light and the dark theme.
/// </summary>
public static class DiffBrushes
{
    /// <summary>Background of removed text.</summary>
    public static IImmutableSolidColorBrush Removed { get; } = new ImmutableSolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));

    /// <summary>Background of added text.</summary>
    public static IImmutableSolidColorBrush Added { get; } = new ImmutableSolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10));

    /// <summary>Text on a removed or added background.</summary>
    public static IImmutableSolidColorBrush OnChange { get; } = new ImmutableSolidColorBrush(Colors.White);
}
