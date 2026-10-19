using HeartRateMonitor.Core.Interfaces;

namespace HeartRateMonitor.Extensibility;

/// <summary>
/// 宿主提供给插件的运行期上下文。所有属性在插件生命周期内保持有效。
/// </summary>
public interface IPluginContext
{
    /// <summary>宿主 DI 容器（可解析 Core/Services 中注册的任意服务）。</summary>
    IServiceProvider Services { get; }

    /// <summary>日志。</summary>
    ILogger Log { get; }

    /// <summary>实时心率会话（当前值、统计、最近采样点、卡路里等）。</summary>
    IHeartRateSession Session { get; }

    /// <summary>蓝牙设备服务（扫描、连接、断开）。</summary>
    IBleService Ble { get; }

    /// <summary>应用设置。</summary>
    ISettingsService Settings { get; }

    /// <summary>插件专属可写目录（首次访问时自动创建）。</summary>
    string DataDirectory { get; }

    /// <summary>插件 Id，便于插件在自绘界面中标识自己。</summary>
    string PluginId { get; }
}
