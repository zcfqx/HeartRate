using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HeartRateMonitor.App.Widgets;

/// <summary>用代码构建组件视图的小工具，避免为每个组件写一套 XAML。</summary>
internal static class WidgetUi
{
    public static TextBlock Text(string text, string styleKey, string? colorKey = null)
    {
        var block = new TextBlock { Text = text };
        block.SetResourceReference(FrameworkElement.StyleProperty, styleKey);

        if (colorKey is not null)
        {
            block.SetResourceReference(TextBlock.ForegroundProperty, colorKey);
        }

        return block;
    }

    public static TextBlock Value(string text, double fontSize = 30, string colorKey = "Brush.Text")
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold
        };

        if (Application.Current is { } app && app.TryFindResource("Font.Display") is FontFamily display)
        {
            block.FontFamily = display;
        }

        block.SetResourceReference(TextBlock.ForegroundProperty, colorKey);
        return block;
    }

    public static Path Icon(string iconKey, double size, string colorKey = "Brush.Accent", double thickness = 1.5)
    {
        var path = new Path
        {
            Data = Infrastructure.AppIcons.Get(iconKey),
            StrokeThickness = thickness,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            VerticalAlignment = VerticalAlignment.Center
        };

        path.SetResourceReference(Shape.StrokeProperty, colorKey);
        return path;
    }

    public static Border Chip(string text, string backgroundKey = "Brush.AccentSoft", string foregroundKey = "Brush.Accent")
    {
        var label = Text(text, "Text.Caption", foregroundKey);
        label.Margin = new Thickness(0);

        var chip = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 2, 6, 2),
            Child = label,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        chip.SetResourceReference(Border.BackgroundProperty, backgroundKey);
        return chip;
    }

    public static StackPanel Stack(Orientation orientation = Orientation.Vertical, double gap = 6)
    {
        var panel = new StackPanel { Orientation = orientation, Margin = new Thickness(0) };

        if (gap <= 0)
        {
            return panel;
        }

        // 手动插入间隔：不依赖 StackPanel.Spacing（旧版 WPF 不支持）
        panel.Children.Add(new Border { Height = gap, Width = gap, Background = Brushes.Transparent });
        return panel;
    }

    public static void AddGap(StackPanel panel, double gap)
    {
        if (gap > 0)
        {
            panel.Children.Add(new Border
            {
                Height = panel.Orientation == Orientation.Vertical ? gap : 0,
                Width = panel.Orientation == Orientation.Horizontal ? gap : 0,
                Background = Brushes.Transparent
            });
        }
    }

    public static Border Divider()
    {
        var border = new Border { Height = 1, Margin = new Thickness(0, 4, 0, 4) };
        border.SetResourceReference(Border.BackgroundProperty, "Brush.Border");
        return border;
    }

    public static bool TryFindColor(Application? app, string key, out Color color)
    {
        color = Colors.Transparent;

        if (app is null)
        {
            return false;
        }

        if (app.TryFindResource(key) is Color resourceColor)
        {
            color = resourceColor;
            return true;
        }

        if (app.TryFindResource(key.Replace("Color.", "Brush.")) is SolidColorBrush brush)
        {
            color = brush.Color;
            return true;
        }

        return false;
    }
}
