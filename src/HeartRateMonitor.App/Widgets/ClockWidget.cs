using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HeartRateMonitor.App.Widgets;

/// <summary>附加功能示例：当前时间与日期。</summary>
internal sealed class ClockWidget : WidgetViewBase
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("zh-CN");

    private readonly TextBlock _time = WidgetUi.Value("--:--", 30);
    private readonly TextBlock _date = WidgetUi.Text(string.Empty, "Text.Caption", "Brush.TextMuted");
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public ClockWidget()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Children.Add(_time);

        _date.Margin = new Thickness(0, 2, 0, 0);
        Grid.SetRow(_date, 1);
        Children.Add(_date);

        _timer.Tick += (_, _) => Refresh();
    }

    protected override void OnAttached()
    {
        Refresh();
        _timer.Start();
    }

    protected override void OnDetached() => _timer.Stop();

    private void Refresh()
    {
        var now = DateTime.Now;
        _time.Text = now.ToString("HH:mm");
        _date.Text = now.ToString("M月d日 dddd", Culture);
    }
}
