using System.IO;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.App.Extensibility;

/// <summary>
/// 传给插件的运行上下文。移除回调只对“组件视图”开放（<see cref="IWidgetContext"/>）。
/// </summary>
internal sealed class PluginContext : IWidgetContext
{
    private readonly Action? _remove;

    public PluginContext(
        IServiceProvider services,
        ILogger log,
        IHeartRateSession session,
        IBleService ble,
        ISettingsService settings,
        string dataDirectory,
        string pluginId,
        Action? remove = null)
    {
        Services = services;
        Log = log;
        Session = session;
        Ble = ble;
        Settings = settings;
        DataDirectory = dataDirectory;
        PluginId = pluginId;
        _remove = remove;
    }

    public IServiceProvider Services { get; }
    public ILogger Log { get; }
    public IHeartRateSession Session { get; }
    public IBleService Ble { get; }
    public ISettingsService Settings { get; }
    public string DataDirectory { get; }
    public string PluginId { get; }

    public void Remove() => _remove?.Invoke();
}

/// <summary>创建插件上下文的工厂（DI 单例）。</summary>
public sealed class PluginContextFactory
{
    private readonly IServiceProvider _services;
    private readonly ILogger _log;
    private readonly IHeartRateSession _session;
    private readonly IBleService _ble;
    private readonly ISettingsService _settings;
    private readonly string _dataRoot;

    public PluginContextFactory(
        IServiceProvider services,
        ILogger log,
        IHeartRateSession session,
        IBleService ble,
        ISettingsService settings)
    {
        _services = services;
        _log = log;
        _session = session;
        _ble = ble;
        _settings = settings;
        _dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeartRateMonitor", "plugin-data");
    }

    public IPluginContext Create(string pluginId, Action? remove = null)
    {
        var directory = Path.Combine(_dataRoot, Sanitize(pluginId));
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch
        {
            // 目录不可用时仍然返回上下文，插件自行降级
        }

        return new PluginContext(_services, _log, _session, _ble, _settings, directory, pluginId, remove);
    }

    private static string Sanitize(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = id.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
