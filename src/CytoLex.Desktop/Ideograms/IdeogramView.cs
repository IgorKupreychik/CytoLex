using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CytoLex.Desktop.ViewModels;

namespace CytoLex.Desktop.Ideograms;

/// <summary>The locus a pointer event on an ideogram is about; <c>null</c> when the pointer left every marker.</summary>
public sealed class LocusEventArgs(RoutedEvent routedEvent, LocusItemViewModel? locus) : RoutedEventArgs(routedEvent)
{
    /// <summary>The locus under the pointer.</summary>
    public LocusItemViewModel? Locus { get; } = locus;
}

/// <summary>
/// Draws one chromosome of the Map view (T-101): the 850-level bands by stain (acen as a waist, gvar hatched, stalk narrow)
/// and the markers of its loci in lanes to the right (breaks as ticks across the chromosome). Raises
/// <see cref="LocusHoveredEvent"/> and <see cref="LocusPressedEvent"/> for the marker under the pointer.
/// </summary>
public sealed class IdeogramView : Control
{
    /// <summary>The chromosome drawn.</summary>
    public static readonly StyledProperty<IdeogramChromosomeViewModel?> ChromosomeProperty =
        AvaloniaProperty.Register<IdeogramView, IdeogramChromosomeViewModel?>(nameof(Chromosome));

    /// <summary>The drawn height of the longest chromosome (chromosome 1), in pixels.</summary>
    public static readonly StyledProperty<double> MaxBodyHeightProperty =
        AvaloniaProperty.Register<IdeogramView, double>(nameof(MaxBodyHeight), 300);

    /// <summary>The pointer moved onto a marker or off every marker.</summary>
    public static readonly RoutedEvent<LocusEventArgs> LocusHoveredEvent =
        RoutedEvent.Register<IdeogramView, LocusEventArgs>(nameof(LocusHovered), RoutingStrategies.Bubble);

    /// <summary>A marker was clicked.</summary>
    public static readonly RoutedEvent<LocusEventArgs> LocusPressedEvent =
        RoutedEvent.Register<IdeogramView, LocusEventArgs>(nameof(LocusPressed), RoutingStrategies.Bubble);

    private const double _top = 18;
    private const double _bottom = 6;
    private const double _bodyLeft = 6;
    private const double _bodyWidth = 14;
    private const double _laneGap = 2;
    private const double _laneWidth = 5;
    private const double _narrowestWidth = 44;

    private static readonly IImmutableSolidColorBrush _outline = new ImmutableSolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50));
    private static readonly IImmutableSolidColorBrush _acen = new ImmutableSolidColorBrush(Color.FromRgb(0xB4, 0x50, 0x50));
    private static readonly IImmutableSolidColorBrush _gvar = new ImmutableSolidColorBrush(Color.FromRgb(0xA8, 0xB8, 0xD0));
    private static readonly IImmutableSolidColorBrush _stalk = new ImmutableSolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0));
    private static readonly IImmutableSolidColorBrush _highlight = new ImmutableSolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xC8, 0x00));
    private static readonly IImmutableSolidColorBrush _gneg = Gray(0xFA);
    private static readonly IImmutableSolidColorBrush _gpos25 = Gray(0xC8);
    private static readonly IImmutableSolidColorBrush _gpos50 = Gray(0x96);
    private static readonly IImmutableSolidColorBrush _gpos75 = Gray(0x64);
    private static readonly IImmutableSolidColorBrush _gpos100 = Gray(0x1E);
    private static readonly ImmutablePen _outlinePen = new(_outline, 1);
    private static readonly ImmutablePen _hatchPen = new(new ImmutableSolidColorBrush(Color.FromRgb(0x70, 0x80, 0x98)), 0.7);
    private static readonly ImmutablePen _selectedPen = new(new ImmutableSolidColorBrush(Colors.Black), 1.5);

    private IdeogramChromosomeViewModel? _observed;
    private LocusItemViewModel? _hovered;

    static IdeogramView()
    {
        AffectsMeasure<IdeogramView>(ChromosomeProperty, MaxBodyHeightProperty);
        AffectsRender<IdeogramView>(ChromosomeProperty, MaxBodyHeightProperty);
    }

    /// <summary>The chromosome drawn.</summary>
    public IdeogramChromosomeViewModel? Chromosome
    {
        get => GetValue(ChromosomeProperty);
        set => SetValue(ChromosomeProperty, value);
    }

    /// <summary>The drawn height of chromosome 1, in pixels.</summary>
    public double MaxBodyHeight
    {
        get => GetValue(MaxBodyHeightProperty);
        set => SetValue(MaxBodyHeightProperty, value);
    }

    /// <summary>The pointer moved onto a marker or off every marker.</summary>
    public event EventHandler<LocusEventArgs>? LocusHovered
    {
        add => AddHandler(LocusHoveredEvent, value);
        remove => RemoveHandler(LocusHoveredEvent, value);
    }

    /// <summary>A marker was clicked.</summary>
    public event EventHandler<LocusEventArgs>? LocusPressed
    {
        add => AddHandler(LocusPressedEvent, value);
        remove => RemoveHandler(LocusPressedEvent, value);
    }

    private double BodyHeight => Math.Max(20, (Chromosome?.RelativeLength ?? 0) * MaxBodyHeight);

    private const double _lanesLeft = _bodyLeft + _bodyWidth + _laneGap + 2;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var lanes = Math.Max(2, Chromosome?.LaneCount ?? 0);
        return new Size(Math.Max(_narrowestWidth, _lanesLeft + (lanes * (_laneWidth + _laneGap)) + 4), _top + MaxBodyHeight + _bottom);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChromosomeProperty)
        {
            if (_observed is not null)
            {
                _observed.PropertyChanged -= OnChromosomeChanged;
            }

            _observed = Chromosome;
            if (_observed is not null)
            {
                _observed.PropertyChanged += OnChromosomeChanged;
            }

            ScrollIntoViewIfTarget();
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The Map tab may be opened after the selection ("Map" on a card): scroll once the ideograms exist.
        ScrollIntoViewIfTarget();
    }

    // After layout, so the scroll viewer knows where this chromosome is.
    private void ScrollIntoViewIfTarget()
    {
        if (Chromosome is { IsScrollTarget: true })
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () =>
                {
                    if (Chromosome is { IsScrollTarget: true } && TopLevel.GetTopLevel(this) is not null)
                    {
                        this.BringIntoView();
                    }
                },
                Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Chromosome is not { } chromosome)
        {
            return;
        }

        var label = new FormattedText(chromosome.Name, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.Gray);
        context.DrawText(label, new Point(_bodyLeft + (_bodyWidth / 2) - (label.Width / 2), 0));

        var height = BodyHeight;
        foreach (var band in chromosome.Bands)
        {
            DrawBand(context, band, height);
        }

        if (!chromosome.Bands.IsEmpty)
        {
            DrawOutline(context, chromosome, height);
        }

        foreach (var marker in chromosome.Markers)
        {
            DrawMarker(context, marker, height);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);
        var marker = MarkerAt(e.GetPosition(this));
        if (!ReferenceEquals(marker?.Locus, _hovered))
        {
            _hovered = marker?.Locus;
            ToolTip.SetTip(this, _hovered?.Tip);
            ToolTip.SetIsOpen(this, _hovered is not null);
            RaiseEvent(new LocusEventArgs(LocusHoveredEvent, _hovered));
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hovered is not null)
        {
            _hovered = null;
            ToolTip.SetIsOpen(this, false);
            RaiseEvent(new LocusEventArgs(LocusHoveredEvent, null));
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);
        if (MarkerAt(e.GetPosition(this)) is { } marker)
        {
            RaiseEvent(new LocusEventArgs(LocusPressedEvent, marker.Locus));
            e.Handled = true;
        }
    }

    private IdeogramMarker? MarkerAt(Point p)
    {
        if (Chromosome is not { } chromosome || BodyHeight <= 0)
        {
            return null;
        }

        var fraction = (p.Y - _top) / BodyHeight;
        var tolerance = 3 / BodyHeight;
        if (p.X >= _bodyLeft - 2 && p.X <= _bodyLeft + _bodyWidth + 2)
        {
            // Over the chromosome: breaks are ticks across it.
            var tick = chromosome.Markers.Where(m => m.Locus.IsPoint && fraction >= m.Span.From - tolerance && fraction <= m.Span.To + tolerance).FirstOrDefault();
            if (tick is not null)
            {
                return tick;
            }
        }

        var lane = (int)Math.Floor((p.X - _lanesLeft) / (_laneWidth + _laneGap));
        return lane < 0 ? null : chromosome.MarkerAt(fraction, lane, tolerance);
    }

    private static void DrawBand(DrawingContext context, IdeogramBand band, double height)
    {
        var y0 = _top + (band.From * height);
        var y1 = _top + (band.To * height);
        var rect = new Rect(_bodyLeft, y0, _bodyWidth, Math.Max(0.5, y1 - y0));
        switch (band.Stain)
        {
            case "acen":
                // The centromere: a waist narrowing towards the p/q boundary.
                var narrowAtEnd = band.Arm == "p";
                var mid = _bodyLeft + (_bodyWidth / 2);
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    var wide = narrowAtEnd ? y0 : y1;
                    var narrow = narrowAtEnd ? y1 : y0;
                    g.BeginFigure(new Point(_bodyLeft, wide), true);
                    g.LineTo(new Point(_bodyLeft + _bodyWidth, wide));
                    g.LineTo(new Point(mid + 2, narrow));
                    g.LineTo(new Point(mid - 2, narrow));
                    g.EndFigure(true);
                }

                context.DrawGeometry(_acen, null, geometry);
                break;
            case "stalk":
                context.FillRectangle(_stalk, rect.Deflate(new Thickness(_bodyWidth / 3, 0)));
                break;
            case "gvar":
                context.FillRectangle(_gvar, rect);
                for (var y = y0; y < y1; y += 3)
                {
                    context.DrawLine(_hatchPen, new Point(_bodyLeft, y), new Point(_bodyLeft + _bodyWidth, Math.Min(y1, y + 3)));
                }

                break;
            default:
                context.FillRectangle(StainBrush(band.Stain), rect);
                break;
        }
    }

    private static void DrawOutline(DrawingContext context, IdeogramChromosomeViewModel chromosome, double height)
    {
        var bands = chromosome.Bands;
        var start = _top + (bands[0].From * height);
        var end = _top + (bands[^1].To * height);
        var acen = bands.Where(b => b.Stain == "acen").ToList();
        if (acen.Count == 0 || chromosome.Centromere is null)
        {
            context.DrawRectangle(null, _outlinePen, new Rect(_bodyLeft, start, _bodyWidth, end - start), 4, 4);
            return;
        }

        var pTop = _top + (acen.Min(b => b.From) * height);
        var qBottom = _top + (acen.Max(b => b.To) * height);
        context.DrawRectangle(null, _outlinePen, new Rect(_bodyLeft, start, _bodyWidth, Math.Max(1, pTop - start)), 4, 4);
        context.DrawRectangle(null, _outlinePen, new Rect(_bodyLeft, qBottom, _bodyWidth, Math.Max(1, end - qBottom)), 4, 4);
    }

    private static void DrawMarker(DrawingContext context, IdeogramMarker marker, double height)
    {
        var locus = marker.Locus;
        var y0 = _top + (marker.Span.From * height);
        var y1 = _top + (marker.Span.To * height);
        var brush = locus.Brush;
        var x = _lanesLeft + (marker.Span.Lane * (_laneWidth + _laneGap));
        if (locus.IsHighlighted)
        {
            context.FillRectangle(_highlight, new Rect(_bodyLeft - 3, y0 - 3, x + _laneWidth + 6 - _bodyLeft, (y1 - y0) + 6));
        }

        if (locus.IsPoint)
        {
            var y = (y0 + y1) / 2;
            var pen = new ImmutablePen(brush, locus.IsHighlighted || locus.IsSelected ? 2.5 : 1.5);
            context.DrawLine(pen, new Point(_bodyLeft - 3, y), new Point(_bodyLeft + _bodyWidth + 3, y));
            var arrow = new StreamGeometry();
            using (var g = arrow.Open())
            {
                g.BeginFigure(new Point(x, y), true);
                g.LineTo(new Point(x + _laneWidth, y - 3));
                g.LineTo(new Point(x + _laneWidth, y + 3));
                g.EndFigure(true);
            }

            context.DrawGeometry(brush, locus.IsSelected ? _selectedPen : null, arrow);
            return;
        }

        var bar = new Rect(x, y0, _laneWidth, Math.Max(2, y1 - y0));
        context.DrawRectangle(locus.Locus.IsUncertain ? new ImmutableSolidColorBrush(brush.Color, 0.45) : brush, locus.IsSelected ? _selectedPen : null, bar, 1.5, 1.5);
    }

    private static IImmutableSolidColorBrush StainBrush(string? stain) => stain switch
    {
        "gpos25" => _gpos25,
        "gpos50" => _gpos50,
        "gpos75" => _gpos75,
        "gpos100" => _gpos100,
        _ => _gneg,
    };

    private static ImmutableSolidColorBrush Gray(byte v) => new(Color.FromRgb(v, v, v));

    private void OnChromosomeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IdeogramChromosomeViewModel.IsScrollTarget))
        {
            ScrollIntoViewIfTarget();
            return;
        }

        InvalidateVisual();
        if (e.PropertyName == nameof(IdeogramChromosomeViewModel.LaneCount))
        {
            InvalidateMeasure();
        }
    }
}
