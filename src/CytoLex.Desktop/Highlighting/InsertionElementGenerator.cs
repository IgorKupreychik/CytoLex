using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace CytoLex.Desktop.Highlighting;

/// <summary>
/// Draws <see cref="HighlightKind.Insert"/> highlights: a zero-length inline element at the insertion point with a
/// severity-coloured marker and the expected text in grey. It is not part of the document, so offsets are unchanged.
/// An autocorrected insertion (T-097) gets a thin grey marker with the inserted text.
/// </summary>
public sealed class InsertionElementGenerator : VisualLineElementGenerator
{
    /// <summary>The current highlights; the view redraws the text view after setting them.</summary>
    public ImmutableArray<Highlight> Highlights { get; set; } = [];

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var next = -1;
        foreach (var h in Highlights)
        {
            if (h.IsInsertion && h.Span.Start >= startOffset && (next < 0 || h.Span.Start < next))
            {
                next = h.Span.Start;
            }
        }

        return next;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var inserts = Highlights.Where(h => h.IsInsertion && h.Span.Start == offset).ToList();
        if (inserts.Count == 0)
        {
            return null;
        }

        var findings = inserts.Where(h => h.Kind == HighlightKind.Insert).ToList();
        var marker = findings.Count == 0 ? SeverityBrushes.AutoCorrected : SeverityBrushes.For(findings.Max(h => h.Severity));
        var text = string.Concat(inserts.Select(h => h.InsertText));
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new Border
        {
            Width = findings.Count == 0 ? 1 : 2,
            Margin = new Thickness(1, 0),
            Background = marker,
            VerticalAlignment = VerticalAlignment.Stretch,
        });
        if (text.Length > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = SeverityBrushes.Expected,
                FontStyle = FontStyle.Italic,
                FontFamily = CurrentContext.TextView.GetValue(TextBlock.FontFamilyProperty),
                FontSize = CurrentContext.TextView.GetValue(TextBlock.FontSizeProperty),
                Margin = new Thickness(0, 0, 1, 0),
            });
        }

        return new InlineObjectElement(0, panel);
    }
}
