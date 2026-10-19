namespace HeartRateMonitor.Extensibility;

/// <summary>插件来源。</summary>
public enum PluginSource
{
    /// <summary>随应用一起编译，位于 App 程序集内。</summary>
    BuiltIn,

    /// <summary>从插件目录中的独立程序集加载。</summary>
    External
}

/// <summary>
/// 插件元数据。插件必须提供稳定、唯一的 <see cref="Id"/>，
/// 用户的启用/禁用状态与组件布局都按此 Id 持久化。
/// </summary>
public sealed record PluginMetadata
{
    /// <summary>唯一标识，建议使用反斜杠命名空间风格，例如 <c>heartrate.core</c>。</summary>
    public required string Id { get; init; }

    /// <summary>界面上显示的名称。</summary>
    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Version { get; init; } = "1.0.0";

    public string Author { get; init; } = string.Empty;
}

/// <summary>
/// 插件契约。实现类必须有无参构造函数（内置插件由宿主直接创建，外部插件由宿主反射创建）。
/// </summary>
public interface IAppPlugin
{
    PluginMetadata Metadata { get; }

    /// <summary>
    /// 向宿主注册本插件提供的扩展点（组件、命令、设置页面）。
    /// 只允许在此阶段注册；<paramref name="context"/> 中的服务可用于构造视图。
    /// </summary>
    void Register(IPluginContext context, IPluginRegistrar registrar);

    /// <summary>插件被禁用或卸载时调用，用于释放插件持有的事件订阅与原生资源。</summary>
    void Shutdown()
    {
    }
}
