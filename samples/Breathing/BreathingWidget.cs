using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.Plugin.Breathing;

/// <summary>
/// 呼吸引导组件：一个随呼吸节奏缩放的圆点 + 阶段倒计时 + 当前心率。
/// 组件视图只依赖 WPF 与插件契约，被移除时宿主会调用 <see cref="Dispose"/>。
/// </summary>
public sealed class BreathingWidget : Grid, IDisposable
{
    private const double InhaleSeconds = 4;
    private const double HoldSeconds = 2;
    private const double ExhaleSeconds = 6;
    private const double CycleSeconds = InhaleSeconds + HoldSeconds + ExhaleSeconds;

    private readonly IWidgetContext _context;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _phase = new();
    private readonly TextBlock _remaining = new();
    private readonly TextBlock _bpm = new();
    private readonly Border _orb;
    private readonly ScaleTransform _orbScale = new(1, 1);
    private double _elapsed;

    public BreathingWidget(IWidgetContext context)
    {
        _context = context;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _orb = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = Brush("Brush.Accent", Color.FromRgb(0x63, 0x66, 0xF1)),
            RenderTransform = _orbScale,
            RenderTransformOrigin = new Point(0.5, 0.5),
            VerticalAlignment = VerticalAlignment.Center
        };

        _phase.FontFamily = FontFamily();
        _phase.FontSize = 15;
        _phase.FontWeight = FontWeights.SemiBold;
        _phase.Foreground = Brush("Brush.Text", Colors.White);
        _phase.Text = "吸气";

        _remaining.FontFamily = FontFamily();
        _remaining.FontSize = 11;
        _remaining.Foreground = Brush("Brush.TextMuted", Color.FromRgb(0x88, 0x88, 0x88));
        _remaining.Margin = new Thickness(0, 2, 0, 0);
        _remaining.Text = "准备开始";

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        text.Children.Add(_phase);
        text.Children.Add(_remaining);

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(_orb);
        header.Children.Add(text);
        Children.Add(header);

        _bpm.FontFamily = FontFamily();
        _bpm.FontSize = 11;
        _bpm.Foreground = Brush("Brush.TextSecondary", Color.FromRgb(0xAA, 0xAA, 0xAA));
        _bpm.Margin = new Thickness(0, 8, 0, 0);
        _bpm.Text = "未连接设备";
        SetRow(_bpm, 1);
        Children.Add(_bpm);

        _context.Session.SampleReceived += OnSampleReceived;
        UpdateBpm(_context.Session.CurrentBpm);

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _context.Session.SampleReceived -= OnSampleReceived;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _elapsed += 0.05;

        var t = _elapsed % CycleSeconds;
        string phase;
        double scale;
        double remaining;

        if (t < InhaleSeconds)
        {
            phase = "吸气";
            scale = 0.55 + 0.45 * (t / InhaleSeconds);
            remaining = InhaleSeconds - t;
        }
        else if (t < InhaleSeconds + HoldSeconds)
        {
            phase = "屏息";
            scale = 1;
            remaining = InhaleSeconds + HoldSeconds - t;
        }
        else
        {
            phase = "呼气";
            scale = 1 - 0.45 * ((t - InhaleSeconds - HoldSeconds) / ExhaleSeconds);
            remaining = CycleSeconds - t;
        }

        _phase.Text = phase;
        _remaining.Text = $"{Math.Max(1, Math.Ceiling(remaining))} 秒";
        _orbScale.ScaleX = scale;
        _orbScale.ScaleY = scale;
    }

    private void OnSampleReceived(object? sender, HeartRateChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateBpm(e.Data.HeartRate));
            return;
        }

        UpdateBpm(e.Data.HeartRate);
    }

    private void UpdateBpm(int bpm)
    {
        if (bpm <= 0)
        {
            _bpm.Text = "未连接设备";
            return;
        }

        var zone = HeartRateZones.Classify(bpm);
        _bpm.Text = $"当前心率 {bpm} BPM · {zone.Name}";
    }

    private FontFamily FontFamily()
        => TryFindResource("Font.Body") as FontFamily ?? new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI, Segoe UI");

    private Brush Brush(string key, Color fallback)
        => TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
}
