namespace HeartRateMonitor.Extensibility;

/// <summary>
/// 插件宿主。负责发现、加载、启用/禁用、卸载插件，并把插件贡献的扩展点暴露给界面。
/// </summary>
public interface IPluginHost
{
    /// <summary>所有已知插件（含被禁用与加载失败的）。</summary>
    IReadOnlyList<PluginInfo> Plugins { get; }

    /// <summary>当前可用组件（仅启用插件贡献的）。</summary>
    IReadOnlyList<WidgetDescriptor> Widgets { get; }

    /// <summary>当前可用命令。</summary>
    IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>当前可用设置页面。</summary>
    IReadOnlyList<SettingsSectionDescriptor> SettingsSections { get; }

    /// <summary>用户插件目录（%LocalAppData%\HeartRateMonitor\plugins）。</summary>
    string PluginsDirectory { get; }

    /// <summary>插件集合或扩展点发生变化。</summary>
    event EventHandler? PluginsChanged;

    /// <summary>扫描内置与外部插件，并加载所有已启用插件。</summary>
    Task InitializeAsync();

    /// <summary>启用插件并注册其扩展点。</summary>
    Task EnableAsync(string pluginId);

    /// <summary>禁用插件：注销扩展点并释放插件实例。</summary>
    Task DisableAsync(string pluginId);

    /// <summary>重新扫描外部插件目录（保留启用状态）。</summary>
    Task RescanAsync();

    /// <summary>重新加载单个外部插件。</summary>
    Task ReloadAsync(string pluginId);

    /// <summary>卸载外部插件：删除插件文件并注销扩展点。内置插件返回 false。</summary>
    Task<bool> UninstallAsync(string pluginId);
}
