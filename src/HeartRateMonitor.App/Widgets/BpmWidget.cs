using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Core.Events;

namespace HeartRateMonitor.App.Widgets;

/// <summary>心脏跳动 + 大号 BPM，是最核心的心率组件。</summary>
internal sealed class BpmWidget : WidgetViewBase
{
    private readonly TextBlock _bpm = WidgetUi.Value("--", 44);
    private readonly TextBlock _zoneLabel;
    private readonly Border _zoneChip;
    private readonly Path _heart;
    private readonly ScaleTransform _pulse = new(1, 1);

    public BpmWidget()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _heart = WidgetUi.Icon("heart", 26);
        _heart.RenderTransform = _pulse;
        _heart.RenderTransformOrigin = new Point(0.5, 0.5);
        _heart.HorizontalAlignment = HorizontalAlignment.Right;

        var bpmStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        _bpm.Margin = new Thickness(0, 0, 8, 0);
        _bpm.LineHeight = 44;

        var unit = WidgetUi.Text("BPM", "Text.Caption", "Brush.TextMuted");
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        unit.Margin = new Thickness(0, 0, 0, 9);

        bpmStack.Children.Add(_bpm);
        bpmStack.Children.Add(unit);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(bpmStack);
        Grid.SetColumn(_heart, 1);
        row.Children.Add(_heart);

        Children.Add(row);

        _zoneLabel = WidgetUi.Text("等待连接设备", "Text.Caption", "Brush.TextMuted");
        _zoneChip = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(7, 2, 7, 2),
            Child = _zoneLabel,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _zoneChip.SetResourceReference(Border.BackgroundProperty, "Brush.AccentSoft");

        Grid.SetRow(_zoneChip, 1);
        Children.Add(_zoneChip);
    }

    protected override void OnAttached()
    {
        Session.SampleReceived += OnSample;
        Session.SessionReset += OnReset;
        StartPulse(64);
    }

    protected override void OnDetached()
    {
        Session.SampleReceived -= OnSample;
        Session.SessionReset -= OnReset;
        _pulse.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _pulse.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private void OnSample(object? sender, HeartRateChangedEventArgs e)
    {
        var bpm = e.Data.HeartRate;
        _bpm.Text = bpm.ToString();
        ApplyZone(bpm);
        StartPulse(bpm);
    }

    private void OnReset(object? sender, EventArgs e)
    {
        _bpm.Text = "--";
        _zoneLabel.Text = "等待心率数据";
        _zoneLabel.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextMuted");
        _zoneChip.SetResourceReference(Border.BackgroundProperty, "Brush.AccentSoft");
        _heart.SetResourceReference(Shape.StrokeProperty, "Brush.Accent");
        StartPulse(64);
    }

    private void ApplyZone(int bpm)
    {
        var zone = HeartRateZones.Classify(bpm);
        var color = ParseColor(zone.Color);

        _zoneLabel.Text = $"{zone.Name} · {zone.ShortName}";
        _zoneLabel.Foreground = new SolidColorBrush(color);
        _zoneChip.Background = new SolidColorBrush(Color.FromArgb(0x2E, color.R, color.G, color.B));
        _heart.Stroke = new SolidColorBrush(color);
    }

    private void StartPulse(int bpm)
    {
        var period = Math.Clamp(60.0 / Math.Max(bpm, 40), 0.4, 1.6);
        var animation = new DoubleAnimation(1.0, 1.18, TimeSpan.FromSeconds(period / 2))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        _pulse.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        _pulse.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    internal static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(0x63, 0x66, 0xF1);
        }
    }
}
