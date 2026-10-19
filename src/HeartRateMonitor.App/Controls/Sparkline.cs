using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.App.Controls;

/// <summary>
/// 轻量心率折线图：直接绘制在 <see cref="FrameworkElement"/> 上，不引入任何图表库。
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    private int _hoveredIndex = -1;

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

    /// <summary>是否允许鼠标悬停并显示最近数据点的数值气泡（默认关闭）。</summary>
    public static readonly DependencyProperty ShowHoverValueProperty = DependencyProperty.Register(
        nameof(ShowHoverValue),
        typeof(bool),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public bool ShowHoverValue
    {
        get => (bool)GetValue(ShowHoverValueProperty);
        set => SetValue(ShowHoverValueProperty, value);
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

        ComputeScale(points, height, out var min, out var range, out var usable, out var bottom);

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

        if (ShowHoverValue)
        {
            DrawHover(context, points, line, min, range, usable, bottom, width, height);
        }
    }

    /// <summary>绘制悬停指示：垂直参考线 + 数据点圆点 + 数值气泡。仅 ShowHoverValue=true 时调用。</summary>
    private void DrawHover(
        DrawingContext context,
        IReadOnlyList<HeartRatePoint> points,
        Brush line,
        double min,
        double range,
        double usable,
        double bottom,
        double width,
        double height)
    {
        var index = _hoveredIndex;
        if (points.Count <= 1 || width <= 1) return;
        if (index < 0 || index >= points.Count) return; // 点集/尺寸变化后索引失效，直接忽略

        var stepX = width / Math.Max(1, points.Count - 1);
        var point = points[index];
        var x = index * stepX;
        var y = bottom - (point.Bpm - min) / range * usable;

        // 1. 淡色垂直参考线：从该数据点到图表区域顶部
        var guidePen = new Pen(WithOpacity(line, 0.4), 1);
        guidePen.Freeze();
        context.DrawLine(guidePen, new Point(x, 4), new Point(x, y));

        // 2. 数据点圆点标记
        var dotOutline = new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)), 1);
        dotOutline.Freeze();
        context.DrawEllipse(line, dotOutline, new Point(x, y), 3.5, 3.5);

        // 3. 数值气泡：`80 BPM · 15:49:32`
        var dpi = VisualTreeHelper.GetDpi(this);
        var text = new FormattedText(
            $"{point.Bpm} BPM · {point.Timestamp.ToLocalTime():HH:mm:ss}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            Brushes.White,
            dpi.PixelsPerDip);

        const double padX = 8;
        const double padY = 4;
        const double gap = 8;
        const double radius = 5;

        var bubbleWidth = text.Width + padX * 2;
        var bubbleHeight = text.Height + padY * 2;

        var left = x - bubbleWidth / 2;
        var top = y - gap - bubbleHeight;
        if (top < 2)
        {
            // 上方空间不足，气泡放到数据点下方
            top = y + gap;
        }

        left = Math.Clamp(left, 2, Math.Max(2, width - bubbleWidth - 2));
        top = Math.Clamp(top, 2, Math.Max(2, height - bubbleHeight - 2));

        var bubble = new Rect(left, top, bubbleWidth, bubbleHeight);
        var background = new SolidColorBrush(Color.FromArgb(0xE6, 0x11, 0x18, 0x27));
        background.Freeze();
        var border = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), 1);
        border.Freeze();

        context.DrawRoundedRectangle(background, border, bubble, radius, radius);
        context.DrawText(text, new Point(left + padX, top + padY));
    }

    /// <summary>统一的纵轴刻度计算：既有绘制路径与悬停命中共用，保证坐标一致。</summary>
    private static void ComputeScale(
        IReadOnlyList<HeartRatePoint> points,
        double height,
        out double min,
        out double range,
        out double usable,
        out double bottom)
    {
        min = double.MaxValue;
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
        bottom = height - padY;
        var top = padY;
        range = Math.Max(1, max - min);
        usable = bottom - top;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!ShowHoverValue) return;

        var points = Points;
        if (points is null || points.Count <= 1 || ActualWidth <= 1)
        {
            SetHoveredIndex(-1);
            return;
        }

        var stepX = ActualWidth / (points.Count - 1);
        if (stepX <= 0)
        {
            SetHoveredIndex(-1);
            return;
        }

        var index = (int)Math.Round(e.GetPosition(this).X / stepX);
        SetHoveredIndex(Math.Clamp(index, 0, points.Count - 1));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetHoveredIndex(-1);
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
    {
        // 仅开启悬停的实例参与命中测试，极简模式实例保持原有行为
        if (ShowHoverValue)
        {
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }

        return base.HitTestCore(hitTestParameters)!;
    }

    private void SetHoveredIndex(int index)
    {
        if (_hoveredIndex == index) return;
        _hoveredIndex = index;
        InvalidateVisual();
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
