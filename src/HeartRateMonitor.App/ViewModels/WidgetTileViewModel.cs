using System.Windows.Media;
using System.Windows.Media.Imaging;
using HeartRateMonitor.App.Infrastructure;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.App.ViewModels;

/// <summary>看板上的一个组件实例：描述符 + 已创建的视图。</summary>
public sealed class WidgetTileViewModel : IDisposable
{
    private bool _disposed;

    public WidgetTileViewModel(WidgetDescriptor descriptor, System.Windows.FrameworkElement view, string pluginName)
    {
        Descriptor = descriptor;
        View = view;
        PluginName = pluginName;
    }

    public WidgetDescriptor Descriptor { get; }

    public System.Windows.FrameworkElement View { get; }

    public string PluginName { get; }

    public string Title => Descriptor.Title;

    public string? Description => Descriptor.Description;

    public bool Removable => Descriptor.Removable;

    /// <summary>组件图标（由插件清单里的逻辑键映射到内置矢量图标）。</summary>
    public Geometry IconGeometry => AppIcons.Get(Descriptor.Icon);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (View is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
