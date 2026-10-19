using System.Windows;
using System.Windows.Controls;
using HeartRateMonitor.App.Controls;
using HeartRateMonitor.Core.Events;

namespace HeartRateMonitor.App.Widgets;

/// <summary>最近心率趋势：自绘 Sparkline，替代原来的 LiveCharts + SkiaSharp。</summary>
internal sealed class ChartWidget : WidgetViewBase
{
    private readonly Sparkline _spark = new() { Height = 56, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _current = WidgetUi.Text("--", "Text.Caption", "Brush.TextSecondary");

    public ChartWidget()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _spark.SetResourceReference(Sparkline.LineBrushProperty, "Brush.Accent");
        _spark.SetResourceReference(Sparkline.FillBrushProperty, "Brush.AccentSoft");
        _spark.MinimumBpm = 50;
        _spark.MaximumBpm = 140;
        _spark.ShowHoverValue = true;

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(WidgetUi.Text("最近趋势", "Text.Caption", "Brush.TextSecondary"));
        Grid.SetColumn(_current, 1);
        header.Children.Add(_current);

        Children.Add(header);
        Grid.SetRow(_spark, 1);
        Children.Add(_spark);
    }

    protected override void OnAttached()
    {
        Session.SampleReceived += OnSample;
        Session.SessionReset += OnReset;
        Refresh();
    }

    protected override void OnDetached()
    {
        Session.SampleReceived -= OnSample;
        Session.SessionReset -= OnReset;
    }

    private void OnSample(object? sender, HeartRateChangedEventArgs e)
    {
        _current.Text = $"{e.Data.HeartRate} BPM";
        Refresh();
    }

    private void OnReset(object? sender, EventArgs e)
    {
        _current.Text = "--";
        Refresh();
    }

    private void Refresh()
    {
        var points = Session.Points;
        _spark.Points = points;

        if (points.Count < 2)
        {
            _spark.MinimumBpm = 50;
            _spark.MaximumBpm = 140;
            return;
        }

        var minimum = int.MaxValue;
        var maximum = int.MinValue;

        foreach (var point in points)
        {
            if (point.Bpm < minimum) minimum = point.Bpm;
            if (point.Bpm > maximum) maximum = point.Bpm;
        }

        var padding = Math.Max(6, (maximum - minimum) / 4);
        _spark.MinimumBpm = Math.Max(30, minimum - padding);
        _spark.MaximumBpm = Math.Min(230, maximum + padding);
    }
}
