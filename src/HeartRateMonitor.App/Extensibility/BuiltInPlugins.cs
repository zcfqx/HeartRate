using System.Windows;
using HeartRateMonitor.App.Widgets;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.App.Extensibility;

/// <summary>
/// 内置插件：随主程序编译发行，与外部插件使用完全相同的契约，
/// 因此可以被用户在插件页里自由启用 / 禁用 / 卸载布局组件。
/// </summary>
internal sealed class CoreWidgetsPlugin : IAppPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "core",
        Name = "核心显示",
        Description = "心率数字、趋势曲线与心率区间指示",
        Version = "1.0.0",
        Author = "内置"
    };

    public void Register(IPluginContext context, IPluginRegistrar registrar)
    {
        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "bpm",
            Title = "心率",
            Description = "大号 BPM 与心跳动画",
            Icon = "heart",
            Order = 0,
            DefaultEnabled = true,
            CreateView = ctx => WidgetViewBase.Create(new BpmWidget(), ctx)
        });

        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "chart",
            Title = "心率趋势",
            Description = "最近采样的折线图",
            Icon = "chart",
            Order = 10,
            DefaultEnabled = true,
            CreateView = ctx => WidgetViewBase.Create(new ChartWidget(), ctx)
        });

        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "zone",
            Title = "心率区间",
            Description = "五段区间与当前所在区间",
            Icon = "zone",
            Order = 20,
            CreateView = ctx => WidgetViewBase.Create(new ZoneWidget(), ctx)
        });
    }
}

internal sealed class DevicePlugin : IAppPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "device",
        Name = "设备连接",
        Description = "扫描并连接蓝牙心率设备",
        Version = "1.0.0",
        Author = "内置"
    };

    public void Register(IPluginContext context, IPluginRegistrar registrar)
    {
        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "device",
            Title = "设备连接",
            Description = "扫描、连接与断开心率设备",
            Icon = "device",
            Order = 30,
            DefaultEnabled = true,
            CreateView = ctx => WidgetViewBase.Create(new DeviceWidget(), ctx)
        });
    }
}

internal sealed class SessionPlugin : IAppPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "session",
        Name = "会话统计",
        Description = "本次会话的统计、时长与卡路里",
        Version = "1.0.0",
        Author = "内置"
    };

    public void Register(IPluginContext context, IPluginRegistrar registrar)
    {
        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "stats",
            Title = "会话统计",
            Description = "平均 / 最高 / 最低心率",
            Icon = "stats",
            Order = 40,
            CreateView = ctx => WidgetViewBase.Create(new StatsWidget(), ctx)
        });

        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "session",
            Title = "本次会话",
            Description = "时长、卡路里与采样数",
            Icon = "clock",
            Order = 50,
            CreateView = ctx => WidgetViewBase.Create(new SessionWidget(), ctx)
        });

        registrar.AddCommand(new CommandDescriptor
        {
            Id = "session.reset",
            Title = "开始新会话",
            Icon = "refresh",
            Execute = () =>
            {
                context.Session.Reset();
                return Task.CompletedTask;
            }
        });
    }
}

internal sealed class ExtrasPlugin : IAppPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "extras",
        Name = "附加组件",
        Description = "时钟等辅助信息",
        Version = "1.0.0",
        Author = "内置"
    };

    public void Register(IPluginContext context, IPluginRegistrar registrar)
    {
        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "clock",
            Title = "时钟",
            Description = "当前时间与日期",
            Icon = "clock",
            Order = 60,
            CreateView = ctx => WidgetViewBase.Create(new ClockWidget(), ctx)
        });
    }
}
