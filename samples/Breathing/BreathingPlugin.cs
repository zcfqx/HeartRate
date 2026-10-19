using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.Plugin.Breathing;

/// <summary>
/// 示例外部插件：注册一个「呼吸引导」组件、一个设置页和一条托盘命令。
/// 这个项目只依赖 <c>HeartRateMonitor.Extensibility</c> 契约包，不引用宿主程序集。
/// </summary>
public sealed class BreathingPlugin : IAppPlugin
{
    private IPluginContext? _context;

    public PluginMetadata Metadata { get; } = new()
    {
        Id = "breathing",
        Name = "呼吸引导",
        Description = "按 4 秒吸气 / 2 秒屏息 / 6 秒呼气的节奏引导深呼吸，帮助在高心率时快速放松。",
        Version = "1.0.0",
        Author = "HeartRate Monitor 示例插件"
    };

    public void Register(IPluginContext context, IPluginRegistrar registrar)
    {
        _context = context;

        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "breathing",
            Title = "呼吸引导",
            Description = "跟随动画节奏呼吸，降低心率。",
            Icon = "sparkles",
            Order = 70,
            DefaultEnabled = false,
            CreateView = widgetContext => new BreathingWidget(widgetContext)
        });

        registrar.AddSettingsSection(new SettingsSectionDescriptor
        {
            Id = "breathing.about",
            Title = "呼吸引导",
            Icon = "bell",
            CreateView = _ => new BreathingSettingsView()
        });

        registrar.AddCommand(new CommandDescriptor
        {
            Id = "breathing.guide",
            Title = "呼吸引导说明",
            Icon = "sparkles",
            Execute = ShowGuideAsync
        });
    }

    public void Shutdown() => _context = null;

    private Task ShowGuideAsync()
    {
        if (_context?.Services.GetService(typeof(IDialogService)) is IDialogService dialogs)
        {
            dialogs.ShowMessage(
                "在组件库中添加「呼吸引导」组件后即可跟随节奏呼吸：吸气 4 秒、屏息 2 秒、呼气 6 秒。",
                "呼吸引导");
        }

        return Task.CompletedTask;
    }
}
