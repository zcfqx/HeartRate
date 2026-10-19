using System.Windows;
using HeartRateMonitor.App.Views;
using HeartRateMonitor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HeartRateMonitor.App;

/// <summary>
/// 极简对话框服务：消息框、确认框、设置窗口。
/// 所有 UI 操作都会切回 UI 线程执行（插件与后台任务都可能调用）。
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;
    private readonly ILogger _log;

    public DialogService(IServiceProvider services, ILogger log)
    {
        _services = services;
        _log = log;
    }

    public void ShowMessage(string message, string title)
        => OnUi(() =>
        {
            var owner = Application.Current?.MainWindow;
            if (owner is not null)
            {
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });

    public bool Confirm(string message, string title)
        => OnUi(() =>
        {
            var owner = Application.Current?.MainWindow;
            var result = owner is not null
                ? MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
                : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        });

    public void ShowSettingsDialog()
        => OnUi(() =>
        {
            var window = _services.GetService<SettingsWindow>();
            if (window is null)
            {
                _log.Warning("无法创建设置窗口");
                return;
            }

            window.Owner = Application.Current?.MainWindow;
            window.ShowDialog();
        });

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    private static T OnUi<T>(Func<T> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        return dispatcher is null || dispatcher.CheckAccess() ? action() : dispatcher.Invoke(action);
    }
}
