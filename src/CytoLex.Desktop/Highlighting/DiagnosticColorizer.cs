using System.Collections.Immutable;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.Highlighting;

/// <summary>
/// Draws <see cref="HighlightKind.Underline"/>, <see cref="HighlightKind.Strike"/> and (T-097) non-empty
/// <see cref="HighlightKind.AutoCorrected"/> highlights as text decorations, and the hovered abnormality span as a background.
/// </summary>
public sealed class DiagnosticColorizer : DocumentColorizingTransformer
{
    /// <summary>The current highlights; the view redraws the text view after setting them.</summary>
    public ImmutableArray<Highlight> Highlights { get; set; } = [];

    /// <summary>A span drawn with a light background (the abnormality card under the mouse, T-069); <c>null</c> = none.</summary>
    public TextSpan? HoverSpan { get; set; }

    /// <summary>The background of <see cref="HoverSpan"/>.</summary>
    public static IImmutableSolidColorBrush HoverBrush { get; } = new ImmutableSolidColorBrush(Color.FromArgb(0x40, 0x00, 0x78, 0xD4));

    protected override void ColorizeLine(DocumentLine line)
    {
        if (HoverSpan is { } hover)
        {
            var hs = Math.Max(hover.Start, line.Offset);
            var he = Math.Min(hover.End, line.EndOffset);
            if (hs < he)
            {
                ChangeLinePart(hs, he, element => element.BackgroundBrush = HoverBrush);
            }
        }

        // Autocorrected spots first (soft), then strike, then underline, so an underline wins where they fall on the same characters.
        foreach (var h in Highlights.Where(h => !h.IsInsertion).OrderBy(h => h.Kind switch
        {
            HighlightKind.AutoCorrected => 0,
            HighlightKind.Strike => 1,
            _ => 2,
        }))
        {
            var start = Math.Max(h.Span.Start, line.Offset);
            var end = Math.Min(h.Span.End, line.EndOffset);
            if (start >= end)
            {
                continue;
            }

            var decorations = Decorations(h);
            ChangeLinePart(start, end, element =>
            {
                var merged = new TextDecorationCollection();
                if (element.TextRunProperties.TextDecorations is { } existing)
                {
                    merged.AddRange(existing);
                }

                merged.AddRange(decorations);
                element.TextRunProperties.SetTextDecorations(merged);
            });
        }
    }

    private static TextDecoration[] Decorations(Highlight h)
    {
        if (h.Kind == HighlightKind.AutoCorrected)
        {
            return
            [
                new TextDecoration
                {
                    Location = TextDecorationLocation.Underline,
                    Stroke = SeverityBrushes.AutoCorrected,
                    StrokeThickness = 1,
                    StrokeThicknessUnit = TextDecorationUnit.Pixel,
                    StrokeOffset = 4,
                    StrokeOffsetUnit = TextDecorationUnit.Pixel,
                    StrokeDashArray = new AvaloniaList<double> { 1, 2 },
                },
            ];
        }

        var brush = SeverityBrushes.For(h.Severity);
        var decoration = new TextDecoration
        {
            Location = h.Kind == HighlightKind.Strike ? TextDecorationLocation.Strikethrough : TextDecorationLocation.Underline,
            Stroke = brush,
            StrokeThickness = 2,
            StrokeThicknessUnit = TextDecorationUnit.Pixel,
            StrokeOffset = h.Kind == HighlightKind.Underline ? 2 : 0,
            StrokeOffsetUnit = TextDecorationUnit.Pixel,
        };
        if (h.Severity == Severity.Info)
        {
            decoration.StrokeDashArray = new AvaloniaList<double> { 2, 2 };
        }

        return [decoration];
    }
}
