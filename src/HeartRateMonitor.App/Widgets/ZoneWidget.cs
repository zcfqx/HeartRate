using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Core.Events;

namespace HeartRateMonitor.App.Widgets;

/// <summary>五段心率区间指示：随当前心率高亮所在区间。</summary>
internal sealed class ZoneWidget : WidgetViewBase
{
    private readonly Border[] _segments = new Border[HeartRateZones.All.Count];
    private readonly StackPanel[] _columns = new StackPanel[HeartRateZones.All.Count];
    private readonly TextBlock _caption = WidgetUi.Text("等待数据", "Text.Caption", "Brush.TextMuted");

    public ZoneWidget()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var bar = new Grid { Margin = new Thickness(0, 2, 0, 8) };

        for (var i = 0; i < HeartRateZones.All.Count; i++)
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition());

            var zone = HeartRateZones.All[i];
            var color = BpmWidget.ParseColor(zone.Color);

            var column = new StackPanel { Margin = new Thickness(i == 0 ? 0 : 2, 0, i == HeartRateZones.All.Count - 1 ? 0 : 2, 0) };

            var segment = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                Opacity = 0.35
            };

            var label = WidgetUi.Text(zone.ShortName, "Text.Caption", "Brush.TextMuted");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 4, 0, 0);

            column.Children.Add(segment);
            column.Children.Add(label);

            _segments[i] = segment;
            _columns[i] = column;

            Grid.SetColumn(column, i);
            bar.Children.Add(column);
        }

        Children.Add(bar);

        _caption.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetRow(_caption, 1);
        Children.Add(_caption);
    }

    protected override void OnAttached()
    {
        Session.SampleReceived += OnSample;
        Session.SessionReset += OnReset;
    }

    protected override void OnDetached()
    {
        Session.SampleReceived -= OnSample;
        Session.SessionReset -= OnReset;
    }

    private void OnSample(object? sender, HeartRateChangedEventArgs e)
    {
        var bpm = e.Data.HeartRate;
        var index = HeartRateZones.IndexOf(bpm);

        for (var i = 0; i < _segments.Length; i++)
        {
            _segments[i].Opacity = i == index ? 1.0 : 0.32;
        }

        var zone = HeartRateZones.All[index];
        _caption.Text = $"{zone.Name} · {zone.Min}-{zone.Max} BPM";
        _caption.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextSecondary");
    }

    private void OnReset(object? sender, EventArgs e)
    {
        for (var i = 0; i < _segments.Length; i++)
        {
            _segments[i].Opacity = 0.32;
        }

        _caption.Text = "等待数据";
        _caption.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextMuted");
    }
}
