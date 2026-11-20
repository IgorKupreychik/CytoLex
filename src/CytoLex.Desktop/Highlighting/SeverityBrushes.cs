using Avalonia.Media;
using Avalonia.Media.Immutable;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.Highlighting;

/// <summary>The severity colours shared by the editor highlights and the diagnostics list.</summary>
public static class SeverityBrushes
{
    /// <summary>Errors.</summary>
    public static IImmutableSolidColorBrush Error { get; } = new ImmutableSolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38));

    /// <summary>Warnings.</summary>
    public static IImmutableSolidColorBrush Warning { get; } = new ImmutableSolidColorBrush(Color.FromRgb(0xCA, 0x50, 0x10));

    /// <summary>Infos.</summary>
    public static IImmutableSolidColorBrush Info { get; } = new ImmutableSolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));

    /// <summary>The grey of expected (inserted) text.</summary>
    public static IImmutableSolidColorBrush Expected { get; } = new ImmutableSolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));

    /// <summary>The brush of <paramref name="severity"/>.</summary>
    public static IImmutableSolidColorBrush For(Severity severity) => severity switch
    {
        Severity.Error => Error,
        Severity.Warning => Warning,
        _ => Info,
    };
}
