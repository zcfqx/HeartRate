using System.Windows;
using System.Windows.Media;
using HeartRateMonitor.App.Infrastructure;

namespace HeartRateMonitor.App.ViewModels;

/// <summary>设置窗口左侧导航的一项。</summary>
public sealed class SettingsNavItem
{
    public required string Title { get; init; }

    public required string IconKey { get; init; }

    public required FrameworkElement View { get; init; }

    public Geometry IconGeometry => AppIcons.Get(IconKey);
}
