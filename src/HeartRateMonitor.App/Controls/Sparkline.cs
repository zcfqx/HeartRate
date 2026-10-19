using System.Windows;
using System.Windows.Media;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.App.Controls;

/// <summary>
/// 轻量心率折线图：直接绘制在 <see cref="FrameworkElement"/> 上，不引入任何图表库。
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<HeartRatePoint>),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush),
        typeof(Brush),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush),
        typeof(Brush),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumBpmProperty = DependencyProperty.Register(
        nameof(MinimumBpm),
        typeof(double),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(40d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumBpmProperty = DependencyProperty.Register(
        nameof(MaximumBpm),
        typeof(double),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(200d, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<HeartRatePoint>? Points
    {
        get => (IReadOnlyList<HeartRatePoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public Brush? LineBrush
    {
        get => (Brush?)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public double MinimumBpm
    {
        get => (double)GetValue(MinimumBpmProperty);
        set => SetValue(MinimumBpmProperty, value);
    }

    public double MaximumBpm
    {
        get => (double)GetValue(MaximumBpmProperty);
        set => SetValue(MaximumBpmProperty, value);
    }

    protected override void OnRender(DrawingContext context)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 1 || height <= 1) return;

        var line = LineBrush ?? new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
        var area = FillBrush ?? WithOpacity(line, 0.18);

        var points = Points;
        if (points is null || points.Count < 2)
        {
            // 无数据时绘制一条居中提示，保持布局稳定
            return;
        }

        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var point in points)
        {
            if (point.Bpm < min) min = point.Bpm;
            if (point.Bpm > max) max = point.Bpm;
        }

        if (max - min < 10)
        {
            var mid = (max + min) / 2;
            min = mid - 5;
            max = mid + 5;
        }

        const double padY = 4;
        var bottom = height - padY;
        var top = padY;
        var range = Math.Max(1, max - min);
        var usable = bottom - top;

        var stepX = width / Math.Max(1, points.Count - 1);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var first = points[0];
            var firstY = bottom - (first.Bpm - min) / range * usable;
            ctx.BeginFigure(new Point(0, firstY), isFilled: false, isClosed: false);

            Point? previous = new(0, firstY);
            for (var i = 1; i < points.Count; i++)
            {
                var x = i * stepX;
                var y = bottom - (points[i].Bpm - min) / range * usable;
                var current = new Point(x, y);

                if (previous is { } prev)
                {
                    // 用中点做二次贝塞尔，得到平滑曲线
                    var mid = new Point((prev.X + current.X) / 2, (prev.Y + current.Y) / 2);
                    ctx.QuadraticBezierTo(prev, mid, isStroked: true, isSmoothJoin: false);
                    previous = current;
                }
            }

            if (previous is { } last)
            {
                ctx.LineTo(last, isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();

        var areaGeometry = new StreamGeometry();
        using (var ctx = areaGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, bottom), isFilled: true, isClosed: true);
            for (var i = 0; i < points.Count; i++)
            {
                var x = i * stepX;
                var y = bottom - (points[i].Bpm - min) / range * usable;
                ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
            }

            ctx.LineTo(new Point(width, bottom), isStroked: true, isSmoothJoin: false);
        }

        areaGeometry.Freeze();
        context.DrawGeometry(area, null, areaGeometry);

        var pen = new Pen(line, 2)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        context.DrawGeometry(null, pen, geometry);

        // 末点高亮
        var lastPoint = points[^1];
        var lastX = width;
        var lastY = bottom - (lastPoint.Bpm - min) / range * usable;
        context.DrawEllipse(line, null, new Point(lastX, lastY), 3, 3);
    }

    private static SolidColorBrush WithOpacity(Brush source, double opacity)
    {
        if (source is SolidColorBrush solid)
        {
            var brush = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(solid.Color.A * opacity, 0, 255),
                solid.Color.R,
                solid.Color.G,
                solid.Color.B));
            brush.Freeze();
            return brush;
        }

        var fallback = new SolidColorBrush(Color.FromArgb(40, 0x63, 0x66, 0xF1));
        fallback.Freeze();
        return fallback;
    }
}
