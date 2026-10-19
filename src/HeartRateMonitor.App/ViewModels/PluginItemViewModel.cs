using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.App.ViewModels;

/// <summary>设置页“功能插件”列表中的一项。</summary>
public sealed partial class PluginItemViewModel : ObservableObject
{
    private readonly IPluginHost _host;
    private readonly IDialogService _dialogs;
    private readonly ILogger _log;

    [ObservableProperty]
    private bool _isEnabled;

    public PluginItemViewModel(PluginInfo info, IPluginHost host, IDialogService dialogs, ILogger log)
    {
        Info = info;
        _host = host;
        _dialogs = dialogs;
        _log = log;
        _isEnabled = info.IsEnabled;
    }

    public PluginInfo Info { get; }

    public string Id => Info.Id;

    public string Name => Info.Name;

    public string Description => Info.Description;

    public string Version => $"v{Info.Version}";

    public string Author => Info.Author;

    public string SourceText => Info.Source == PluginSource.BuiltIn ? "内置" : "外部";

    public bool CanUninstall => Info.CanUninstall;

    public string? Error => Info.Error;

    public bool HasError => !string.IsNullOrWhiteSpace(Info.Error);

    public string StatusText => IsEnabled ? "已启用" : "已停用";

    partial void OnIsEnabledChanged(bool value)
    {
        if (value == Info.IsEnabled)
        {
            return;
        }

        _ = ApplyAsync(value);
    }

    private async Task ApplyAsync(bool enable)
    {
        try
        {
            if (enable)
            {
                await _host.EnableAsync(Id);
            }
            else
            {
                await _host.DisableAsync(Id);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"切换插件状态失败: {Id}", ex);
            _dialogs.ShowMessage($"切换插件状态失败：{ex.Message}", "插件");
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        try
        {
            await _host.ReloadAsync(Id);
        }
        catch (Exception ex)
        {
            _log.Error($"重载插件失败: {Id}", ex);
            _dialogs.ShowMessage($"重载插件失败：{ex.Message}", "插件");
        }
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (!_dialogs.Confirm($"确定卸载插件“{Name}”？插件文件将被删除。", "卸载插件"))
        {
            return;
        }

        var removed = await _host.UninstallAsync(Id);
        if (!removed)
        {
            _dialogs.ShowMessage("卸载失败，请关闭占用插件文件的程序后重试。", "卸载插件");
        }
    }
}
