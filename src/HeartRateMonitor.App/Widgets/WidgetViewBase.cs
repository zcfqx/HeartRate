using System.Windows;
using System.Windows.Controls;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.App.Widgets;

/// <summary>
/// 所有内置组件视图的基类：宿主创建视图后调用 <see cref="Attach"/> 注入插件上下文，
/// 组件在 <see cref="OnAttached"/> 里订阅会话事件，在 <see cref="OnDetached"/> 里退订。
/// </summary>
public abstract class WidgetViewBase : Grid, IDisposable
{
    private bool _disposed;
    private bool _attached;

    public IWidgetContext Context { get; private set; } = null!;

    protected IHeartRateSession Session => Context.Session;

    protected IBleService Ble => Context.Ble;

    protected ISettingsService Settings => Context.Settings;

    protected ILogger Log => Context.Log;

    /// <summary>
    /// BLE 事件来自后台线程，操作界面元素前必须切回 UI 线程，
    /// 否则会抛“调用线程无法访问此对象”，导致扫描列表永远不更新。
    /// </summary>
    protected static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    public static T Create<T>(T view, IWidgetContext context) where T : WidgetViewBase
    {
        view.Attach(context);
        return view;
    }

    internal void Attach(IWidgetContext context)
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        Context = context;
        OnAttached();
    }

    protected virtual void OnAttached()
    {
    }

    protected virtual void OnDetached()
    {
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            OnDetached();
        }
        catch
        {
            // 组件卸载失败不影响宿主
        }

        GC.SuppressFinalize(this);
    }
}
