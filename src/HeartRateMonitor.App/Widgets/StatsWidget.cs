using System.Windows;
using System.Windows.Controls;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Core.Events;

namespace HeartRateMonitor.App.Widgets;

/// <summary>本次会话的平均 / 最高 / 最低心率与所在区间。</summary>
internal sealed class StatsWidget : WidgetViewBase
{
    private readonly TextBlock _average = WidgetUi.Value("--", 22);
    private readonly TextBlock _maximum = WidgetUi.Value("--", 22);
    private readonly TextBlock _minimum = WidgetUi.Value("--", 22);
    private readonly TextBlock _zone = WidgetUi.Text("等待数据", "Text.Caption", "Brush.TextMuted");

    public StatsWidget()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        AddCell(grid, _average, "平均", 0);
        AddCell(grid, _maximum, "最高", 1);
        AddCell(grid, _minimum, "最低", 2);

        Children.Add(grid);

        Grid.SetRow(_zone, 1);
        _zone.Margin = new Thickness(0, 8, 0, 0);
        Children.Add(_zone);
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

    private static void AddCell(Grid grid, TextBlock value, string caption, int column)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        value.HorizontalAlignment = HorizontalAlignment.Center;

        var label = WidgetUi.Text(caption, "Text.Caption", "Brush.TextMuted");
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Margin = new Thickness(0, 2, 0, 0);

        panel.Children.Add(value);
        panel.Children.Add(label);

        Grid.SetColumn(panel, column);
        grid.Children.Add(panel);
    }

    private void OnSample(object? sender, HeartRateChangedEventArgs e)
    {
        var bpm = e.Data.HeartRate;

        _average.Text = Math.Round(Session.AverageBpm).ToString();
        _maximum.Text = Session.MaxBpm.ToString();
        _minimum.Text = Session.MinBpm.ToString();

        var zone = HeartRateZones.Classify(bpm);
        _zone.Text = $"当前区间 · {zone.Name} ({zone.Min}-{zone.Max} BPM)";
        _zone.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextSecondary");
    }

    private void OnReset(object? sender, EventArgs e)
    {
        _average.Text = "--";
        _maximum.Text = "--";
        _minimum.Text = "--";
        _zone.Text = "等待数据";
        _zone.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextMuted");
    }
}
