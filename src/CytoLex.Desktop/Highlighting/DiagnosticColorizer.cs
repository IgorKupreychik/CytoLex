using System.Collections.Immutable;
using Avalonia.Collections;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.Highlighting;

/// <summary>Draws <see cref="HighlightKind.Underline"/> and <see cref="HighlightKind.Strike"/> highlights as text decorations.</summary>
public sealed class DiagnosticColorizer : DocumentColorizingTransformer
{
    /// <summary>The current highlights; the view redraws the text view after setting them.</summary>
    public ImmutableArray<Highlight> Highlights { get; set; } = [];

    protected override void ColorizeLine(DocumentLine line)
    {
        // Strike first, then underline, so an underline wins where both fall on the same characters.
        foreach (var h in Highlights.Where(h => h.Kind != HighlightKind.Insert).OrderByDescending(h => h.Kind == HighlightKind.Strike))
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
