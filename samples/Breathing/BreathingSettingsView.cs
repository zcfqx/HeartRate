using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HeartRateMonitor.Plugin.Breathing;

/// <summary>呼吸引导插件在设置窗口中的说明页。</summary>
public sealed class BreathingSettingsView : StackPanel
{
    public BreathingSettingsView()
    {
        Margin = new Thickness(0, 4, 0, 12);

        Children.Add(Text("呼吸引导", 15, FontWeights.SemiBold, "Brush.Text"));
        Children.Add(Text("这是一个示例外部插件，演示如何向宿主投放组件、设置页和托盘命令。", 12, FontWeights.Normal, "Brush.TextSecondary", top: 6));
        Children.Add(Text("用法：在悬浮窗点击「添加组件」，选择「呼吸引导」即可。组件会按 4 秒吸气、2 秒屏息、6 秒呼气的节奏显示动画，并展示当前心率区间。", 12, FontWeights.Normal, "Brush.TextSecondary", top: 10));
        Children.Add(Text("卸载：在「功能插件」页点击卸载，或直接删除插件目录。", 12, FontWeights.Normal, "Brush.TextMuted", top: 10));
    }

    private TextBlock Text(string content, double size, FontWeight weight, string brushKey, double top = 0)
    {
        return new TextBlock
        {
            Text = content,
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, top, 0, 0),
            Foreground = TryFindResource(brushKey) as Brush ?? new SolidColorBrush(Colors.Gray),
            FontFamily = TryFindResource("Font.Body") as FontFamily ?? new FontFamily("Microsoft YaHei UI")
        };
    }
}
