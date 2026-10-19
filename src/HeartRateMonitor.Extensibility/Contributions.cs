using System.Windows;

namespace HeartRateMonitor.Extensibility;

/// <summary>
/// 主界面上的一个「组件」：插件通过它向悬浮窗投放一块可增删的界面。
/// <para>
/// 创建视图的委托在 UI 线程执行；返回的 <see cref="FrameworkElement"/> 在组件被移除时
/// 若实现了 <see cref="IDisposable"/> 会被宿主释放，插件应借此解除事件订阅。
/// </para>
/// </summary>
public sealed class WidgetDescriptor
{
    /// <summary>全局唯一 Id，用于持久化用户布局。</summary>
    public required string Id { get; init; }

    /// <summary>组件标题，显示在组件卡片头部与组件库中。</summary>
    public required string Title { get; init; }

    public string Description { get; init; } = string.Empty;

    /// <summary>图标键（见 Icons.xaml 中的几何资源），未知键回退为 puzzle。</summary>
    public string Icon { get; init; } = "puzzle";

    /// <summary>组件库中的排序权重，越小越靠前。</summary>
    public int Order { get; init; } = 100;

    /// <summary>是否允许用户从布局中移除。核心组件可设为 false。</summary>
    public bool Removable { get; init; } = true;

    /// <summary>首次启动时是否默认出现在悬浮窗中。</summary>
    public bool DefaultEnabled { get; init; }

    /// <summary>创建组件视图（UI 线程调用）。</summary>
    public required Func<IWidgetContext, FrameworkElement> CreateView { get; init; }

    /// <summary>由宿主填充：贡献该组件的插件 Id。</summary>
    public string PluginId { get; set; } = string.Empty;
}

/// <summary>
/// 一个可执行的命令：宿主会把它放进托盘右键菜单（将来也可用于命令面板）。
/// </summary>
public sealed class CommandDescriptor
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string Icon { get; init; } = "sparkles";

    public required Func<Task> Execute { get; init; }

    /// <summary>由宿主填充：贡献该命令的插件 Id。</summary>
    public string PluginId { get; set; } = string.Empty;
}

/// <summary>
/// 设置窗口中的一个额外页面。插件返回的视图会显示在设置窗口左侧导航中。
/// </summary>
public sealed class SettingsSectionDescriptor
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string Icon { get; init; } = "sliders";

    public required Func<IPluginContext, FrameworkElement> CreateView { get; init; }

    /// <summary>由宿主填充：贡献该设置页的插件 Id。</summary>
    public string PluginId { get; set; } = string.Empty;
}

/// <summary>插件向宿主注册扩展点的入口。</summary>
public interface IPluginRegistrar
{
    void AddWidget(WidgetDescriptor widget);
    void AddCommand(CommandDescriptor command);
    void AddSettingsSection(SettingsSectionDescriptor section);
}

/// <summary>组件视图的上下文。</summary>
public interface IWidgetContext : IPluginContext
{
    /// <summary>请求宿主按 Id 移除本组件（例如组件内的关闭按钮）。</summary>
    void Remove();
}

/// <summary>插件列表项（供设置界面展示），由宿主构造。</summary>
public sealed record PluginInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = string.Empty;
    public PluginSource Source { get; init; }
    public string Location { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public bool CanUninstall => Source == PluginSource.External;
    public string? Error { get; init; }
}
