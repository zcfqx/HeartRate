using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HeartRateMonitor.Core.Events;

namespace HeartRateMonitor.App.Widgets;

/// <summary>本次会话的时长、卡路里与采样数，并提供“开始新会话”。</summary>
internal sealed class SessionWidget : WidgetViewBase
{
    private readonly TextBlock _duration = WidgetUi.Value("00:00", 20);
    private readonly TextBlock _calories = WidgetUi.Value("0.0", 20);
    private readonly TextBlock _samples = WidgetUi.Value("0", 20);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public SessionWidget()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        AddCell(grid, _duration, "时长", 0);
        AddCell(grid, _calories, "千卡", 1);
        AddCell(grid, _samples, "采样", 2);

        Children.Add(grid);

        var reset = new Button
        {
            Content = "开始新会话",
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0)
        };
        reset.SetResourceReference(FrameworkElement.StyleProperty, "Button.Outline");
        reset.Click += (_, _) => Session.Reset();

        Grid.SetRow(reset, 1);
        Children.Add(reset);

        _timer.Tick += (_, _) => UpdateDuration();
    }

    protected override void OnAttached()
    {
        Session.SampleReceived += OnSample;
        Session.SessionReset += OnReset;
        _timer.Start();
    }

    protected override void OnDetached()
    {
        Session.SampleReceived -= OnSample;
        Session.SessionReset -= OnReset;
        _timer.Stop();
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

    private void OnSample(object? sender, HeartRateChangedEventArgs e) => Refresh();

    private void OnReset(object? sender, EventArgs e) => Refresh();

    private void UpdateDuration()
    {
        if (Session.StartedAt is not { } startedAt)
        {
            _duration.Text = "00:00";
            return;
        }

        var duration = DateTime.Now - startedAt;
        _duration.Text = duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private void Refresh()
    {
        _calories.Text = Session.Calories.ToString("F1");
        _samples.Text = Session.SampleCount.ToString();
        UpdateDuration();
    }
}
