# 插件开发指南（v2）

> 适用版本：HeartRate Monitor v2（插件契约程序集 `HeartRateMonitor.Extensibility`）。
> 相关文档：[UI-REDESIGN.md](UI-REDESIGN.md) · [README](../README.md)

HeartRate Monitor v2 的界面功能全部由插件提供。主程序内置 4 个插件（`core` / `device` / `session` / `extras`），
第三方插件与内置插件使用**完全相同的契约**，可以：

- 往悬浮窗投放可增删的**组件**（Widget）；
- 往设置中心添加一页**设置页**（Settings Section）；
- 往托盘右键菜单添加**命令**（Command）。

一个插件就是一个实现 `IAppPlugin` 的类 + 一份 `plugin.json` 清单 + 程序集。

---

## 目录

- [架构概览](#架构概览)
- [快速开始](#快速开始)
- [插件清单 plugin.json](#插件清单-pluginjson)
- [入口：IAppPlugin](#入口iappplugin)
- [三类扩展点](#三类扩展点)
- [插件上下文 IPluginContext](#插件上下文-iplugincontext)
- [编写组件视图](#编写组件视图)
- [图标键](#图标键)
- [生命周期](#生命周期)
- [加载与程序集隔离](#加载与程序集隔离)
- [调试与排错](#调试与排错)
- [打包、安装与卸载](#打包安装与卸载)
- [版本兼容与最佳实践](#版本兼容与最佳实践)
- [完整示例：呼吸引导](#完整示例呼吸引导)

---

## 架构概览

```
┌──────────────────────────────────────────────────────────┐
│ Host（HeartRateMonitor.App）                              │
│  PluginHost ── 发现 / 加载 / 启停 / 重载 / 卸载            │
│      │        每个外部插件一个可回收 AssemblyLoadContext    │
│      ├─ Widgets           → 主窗看板 + 组件库              │
│      ├─ SettingsSections  → 设置窗左侧导航                 │
│      └─ Commands          → 托盘右键菜单                   │
└──────────────▲───────────────────────────────────────────┘
               │ 共享契约程序集 HeartRateMonitor.*
┌──────────────┴───────────────────────────────────────────┐
│ Plugin（如 Breathing）                                    │
│  IAppPlugin.Register(IPluginContext, IPluginRegistrar)    │
└──────────────────────────────────────────────────────────┘
```

契约程序集 `HeartRateMonitor.Extensibility` 只依赖 `HeartRateMonitor.Core`（零第三方依赖），
因此插件可以安全引用它，而不会把 WPF/DI 版本绑定进 API。

---

## 快速开始

### 1. 创建项目

```
MyPlugin/
├── MyPlugin.csproj
├── plugin.json
└── MyPlugin.cs
```

`MyPlugin.csproj`（要点：`net8.0-windows`、`UseWPF`、引用 Extensibility、把清单复制到输出）：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.22621.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>MyPlugin</AssemblyName>
    <RootNamespace>MyPlugin</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HeartRateMonitor.Extensibility\HeartRateMonitor.Extensibility.csproj">
      <!-- 宿主已提供契约程序集：不要复制到插件目录 -->
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>

  <ItemGroup>
    <None Update="plugin.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

> 也可以引用 NuGet 包（若你分发契约包）；关键是**运行时不要自带** `HeartRateMonitor.*.dll`。

### 2. 编写入口

```csharp
using System.Windows;
using System.Windows.Controls;
using HeartRateMonitor.Extensibility;

public sealed class MyPlugin : IAppPlugin
{
    public PluginMetadata Metadata => new()
    {
        Id = "my.plugin",              // 全局唯一，稳定不变（布局与禁用状态按此持久化）
        Name = "我的插件",
        Description = "示例插件",
        Version = "1.0.0",
        Author = "you"
    };

    private IPluginContext? _context;

    public void Register(IPluginContext context, IPluginRegistrar registrar)
    {
        _context = context;

        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "my.hello",             // 组件 Id 全局唯一
            Title = "你好",
            Description = "一个最小示例组件",
            Icon = "heart",
            Order = 90,
            DefaultEnabled = false,      // 默认不放到看板，用户从组件库添加
            CreateView = ctx =>
            {
                var text = new TextBlock { Text = "Hello plugin" };
                return text;
            }
        });
    }

    public void Shutdown() => _context = null;
}
```

### 3. 构建并安装

```bash
dotnet build MyPlugin.csproj -c Release
# 输出 bin/Release/net8.0-windows10.0.22621.0/ 下应有 MyPlugin.dll 与 plugin.json
mkdir "%LOCALAPPDATA%\HeartRateMonitor\plugins\MyPlugin"
copy 输出目录\MyPlugin.dll   "%LOCALAPPDATA%\HeartRateMonitor\plugins\MyPlugin\"
copy 输出目录\plugin.json    "%LOCALAPPDATA%\HeartRateMonitor\plugins\MyPlugin\"
```

然后在应用 **设置 → 功能插件 → 重新扫描**，即可看到插件并启用。

---

## 插件清单 plugin.json

```json
{
  "id": "my.plugin",
  "name": "我的插件",
  "version": "1.0.0",
  "description": "示例插件",
  "author": "you",
  "entryAssembly": "MyPlugin.dll",
  "entryType": "MyPlugin.MyPlugin"
}
```

| 字段 | 必填 | 说明 |
|------|:---:|------|
| `id` | ✅ | 插件唯一标识，建议 `命名空间.插件名`，启用状态与布局按它持久化 |
| `name` | ✅ | 显示名称 |
| `entryAssembly` | ✅ | 入口程序集文件名（相对插件目录） |
| `entryType` | ✅ | 入口 `IAppPlugin` 实现的完整类型名 |
| `version` | — | 默认 `1.0.0`，显示在插件列表 |
| `description` | — | 插件列表中的说明 |
| `author` | — | 作者 |

解析为大小写不敏感的 JSON（允许注释与尾逗号）。若 `entryType` 找不到，宿主会回退为扫描该程序集中
第一个 `IAppPlugin` 实现，方便入口类型改名而不改清单。

---

## 入口：IAppPlugin

```csharp
public interface IAppPlugin
{
    PluginMetadata Metadata { get; }
    void Register(IPluginContext context, IPluginRegistrar registrar);
    void Shutdown() { }   // 默认空实现
}
```

- **实现类必须有无参构造函数**（外部插件由宿主反射创建）。
- `Register` 只在插件被激活时调用一次；不要在构造函数里做 UI 或耗时操作。
- `Shutdown` 在禁用、重载、卸载、应用退出时调用；请在此解除你自己的**非视图**订阅与资源。
- 组件视图返回的 `FrameworkElement` 若实现 `IDisposable`，被移除时宿主会自动 `Dispose`。

### 注册器

```csharp
public interface IPluginRegistrar
{
    void AddWidget(WidgetDescriptor widget);
    void AddCommand(CommandDescriptor command);
    void AddSettingsSection(SettingsSectionDescriptor section);
}
```

同一插件内各描述符的 `Id` 也必须唯一且稳定。

---

## 三类扩展点

### 1. 组件 WidgetDescriptor

```csharp
new WidgetDescriptor
{
    Id = "my.breath",
    Title = "呼吸引导",
    Description = "跟随动画呼吸",
    Icon = "sparkles",        // 逻辑图标键
    Order = 70,               // 组件库排序，越小越靠前
    Removable = true,         // false = 用户不能从看板移除
    DefaultEnabled = false,   // true = 首次启动默认显示
    CreateView = ctx => new MyWidget(ctx)
};
```

- 组件出现在「组件库」浮层，用户点击后加入看板；布局以 Id 数组持久化到设置项 `Widgets`。
- 宿主统一提供卡片外壳（图标、标题、插件名、编辑态移除按钮），插件视图只需负责卡片**内容**。
- `CreateView` 在 UI 线程调用；返回视图会被放入卡片 `ContentPresenter`。

### 2. 设置页 SettingsSectionDescriptor

```csharp
new SettingsSectionDescriptor
{
    Id = "my.about",
    Title = "我的插件",
    Icon = "sliders",
    CreateView = ctx => new MySettingsView(ctx)
};
```

设置页会作为左侧导航的一项出现（位于「数据」与「功能插件」之间）。视图通常包含
`TextBlock` / `TextBox` / `Slider` / `ToggleButton` 等表单控件；如需保存配置，直接使用
`ctx.Settings` 写属性后调用 `await ctx.Settings.SaveAsync()`（或保留给设置窗的“保存并关闭”统一保存）。

### 3. 命令 CommandDescriptor

```csharp
new CommandDescriptor
{
    Id = "my.guide",
    Title = "打开使用说明",
    Icon = "info",
    Execute = async () =>
    {
        var dialogs = (IDialogService?)ctx.Services.GetService(typeof(IDialogService));
        dialogs?.ShowMessage("这是插件提供的命令。", "说明");
        await Task.CompletedTask;
    }
};
```

命令出现在**托盘右键菜单**（显示主窗口 / 设置之后）。`Execute` 在 UI 线程触发，可 `async`。

---

## 插件上下文 IPluginContext

```csharp
public interface IPluginContext
{
    IServiceProvider Services { get; }   // 宿主 DI 容器
    ILogger Log { get; }
    IHeartRateSession Session { get; }
    IBleService Ble { get; }
    ISettingsService Settings { get; }
    string DataDirectory { get; }        // %LocalAppData%\HeartRateMonitor\plugin-data\<id>\
    string PluginId { get; }
}
```

常用成员：

| 成员 | 用途 |
|------|------|
| `Session.CurrentBpm` / `Latest` / `MinBpm` / `MaxBpm` / `AverageBpm` / `SampleCount` / `Calories` / `Duration` | 会话实时数据 |
| `Session.Points` | 最近 240 个 `HeartRatePoint(Bpm, Timestamp)`，用于自绘趋势 |
| `Session.StartedAt` | 会话开始时间（未开始为 null） |
| `Session.SampleReceived` / `SessionReset` / `ConnectionStateChanged` | 事件订阅（回调已切回 UI 线程） |
| `Session.Reset()` | 开始新会话 |
| `Ble.State` / `Ble.ConnectedDevice` / `Ble.IsScanning` | 蓝牙状态 |
| `Ble.StartScanningAsync()` / `StopScanningAsync()` / `ConnectAsync(device)` / `DisconnectAsync()` / `AutoReconnectAsync()` | 设备控制 |
| `Ble.CheckAvailabilityAsync()` | 扫描前检查，返回 `BleAvailability`（可用 `ToUserMessage()` 转中文提示） |
| `Settings.*` | 全部设置属性（见 README 配置表） |
| `Log.Info/Warning/Error/Debug` | 写入 `%LocalAppData%\HeartRateMonitor\logs\app-yyyyMMdd.log` |
| `DataDirectory` | 插件自己的持久化目录，首次访问时自动创建 |
| `Services` | 可解析 `IDialogService`、`IDataService`、`IHeartRateService` 等宿主服务 |

> `WidgetDescriptor.CreateView` 收到的是 `IWidgetContext`（继承自 `IPluginContext`），额外提供 `Remove()`
> 供组件内部按钮请求移除自己。

---

## 编写组件视图

组件视图是普通 WPF `FrameworkElement`（`UserControl`、`Grid`、`Border`…均可）。
宿主已提供主题画刷与样式，建议：

- 用 `DynamicResource` 引用主题键，例如 `{DynamicResource Brush.Text}`、`{DynamicResource Brush.SurfaceAlt}`、
  `{DynamicResource Brush.Accent}`，这样深/浅主题切换自动生效；
- 文字样式键：`Text.H1` / `Text.H2` / `Text.Label` / `Text.Caption` / `Text.Value`；
- 卡片内部可用 `Card.Inner`、`Chip`、`Button.Ghost`、`Button.Icon`、`Switch` 等 `Base.xaml` 样式。

订阅事件并在移除时释放（推荐模式）：

```csharp
public sealed class MyWidget : Grid, IDisposable
{
    private readonly IWidgetContext _ctx;

    public MyWidget(IWidgetContext ctx)
    {
        _ctx = ctx;
        _ctx.Session.SampleReceived += OnSample;
        _ctx.Session.SessionReset += OnReset;
        Build();
    }

    private void OnSample(object? sender, HeartRateChangedEventArgs e)
        => Dispatcher.BeginInvoke(() => Update(e.Data.HeartRate));

    private void OnReset(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(Update);

    private void Update(int bpm)
    {
        // 更新自己的控件
    }

    public void Dispose()
    {
        _ctx.Session.SampleReceived -= OnSample;
        _ctx.Session.SessionReset -= OnReset;
    }
}
```

> 事件回调已经由 `HeartRateSession` 切回 UI 线程（构造时捕获 `SynchronizationContext`），
> 但插件若在线程池线程创建视图或收到回调，仍建议用 `Dispatcher.BeginInvoke` 保证安全。

心跳/周期性刷新可用 `DispatcherTimer`，并在 `Dispose` 中 `Stop()`。

---

## 图标键

`Icon` 使用逻辑键，由宿主 `AppIcons` 映射为矢量 `Geometry`（未知键回退 `puzzle`）：

| 键 | 图标 | 键 | 图标 |
|----|------|----|------|
| `heart` | ♥ 心 | `chart` | 趋势 |
| `zone` | 区间 | `device` | 蓝牙 |
| `stats` | 统计 | `clock` | 时钟 |
| `sparkles` | 闪光 | `puzzle` | 拼图（默认） |
| `sliders` | 设置项 | `palette` | 调色板 |
| `bell` | 提醒 | `info` | 信息 |
| `add` / `close` / `minimize` | 增/关/最小化 | `edit` / `refresh` / `check` / `remove` / `settings` | 编辑/刷新/勾/移除/齿轮 |

---

## 生命周期

| 阶段 | 宿主动作 | 插件侧 |
|------|----------|--------|
| 启动 | 扫描 `plugins\*\plugin.json` → 创建 ALC → 反射实例化 → 若启用则 `Register` | 构造函数应轻量 |
| 启用 | 创建上下文 → `Register` → 收集扩展点 → 重建 UI | `Register` 只在此调用 |
| 禁用 | 移除并释放该插件的视图/设置页 → `Shutdown()` → 卸载 ALC（外部插件） | `Shutdown` 解除订阅 |
| 重新加载 | 禁用后重新加载程序集并激活（外部插件） | 同启用 |
| 卸载 | 禁用 → 删除插件目录 → 从列表移除（内置插件不可卸载） | — |
| 重新扫描 | 重新枚举目录：新插件加入、缺失的插件移除（保留启用状态） | — |
| 应用退出 | 依次禁用所有插件 | `Shutdown` |

> 启用状态保存在设置项 `DisabledPlugins`（`;` 分隔的插件 Id），不会因重扫描丢失。

---

## 加载与程序集隔离

- 每个**外部插件**使用独立的可回收 `AssemblyLoadContext`（`isCollectible: true`）。
- 所有以 `HeartRateMonitor.` 开头的程序集**交回宿主默认 ALC** 解析，保证插件与宿主共享同一套契约类型
  —— 因此插件**不要**自带 `HeartRateMonitor.Core.dll` / `HeartRateMonitor.Extensibility.dll`。
- 插件私有依赖（第三方库、自己的辅助程序集）放在插件目录，由 `AssemblyDependencyResolver` 解析。
- 禁用/卸载时会 `Unload` 插件 ALC；若插件仍持有静态事件引用，可能延迟回收，但类型标识已隔离。
- 插件数据目录：`%LOCALAPPDATA%\HeartRateMonitor\plugin-data\<sanitized plugin id>\`。

---

## 调试与排错

1. **插件未出现在列表**：确认目录为 `plugins\<任意名>\plugin.json`，JSON 合法，`id/entryAssembly/entryType` 正确。
2. **列表出现但带红色错误**：`PluginInfo.Error` 会显示加载/注册异常信息；同时查看日志 `logs\app-yyyyMMdd.log`。
3. **组件不显示**：确认插件已启用、组件 `Id` 未与其他插件冲突、`CreateView` 未抛异常。
4. **设置页不显示**：`SettingsSectionDescriptor.Title` 为空或 `CreateView` 抛异常时会被跳过。
5. **调试插件**：将启动项目设为 `HeartRateMonitor.App`，在插件代码里打断点并附加到 `HeartRateMonitor.App.exe`；
   或临时把插件作为内置插件调试（不推荐，见下）。
6. **日志**：`ctx.Log.Info(...)` 写入按天滚动的日志文件（4MB 轮转，保留 7 天）。

---

## 打包、安装与卸载

推荐发布布局：

```
MyPlugin-v1.0.0/
├── plugin.json
├── MyPlugin.dll
└── <私有依赖>.dll
```

安装即把整个目录复制到 `%LOCALAPPDATA%\HeartRateMonitor\plugins\MyPlugin\`（目录名可与插件 id 不同），
然后在设置页点「重新扫描」。卸载可直接在设置页点「卸载」（仅外部插件），或关闭应用后删除目录。

> 便携部署：也支持 `<应用目录>\plugins\`，适合随应用一起分发的绿色包。

---

## 版本兼容与最佳实践

- **Id 稳定**：`PluginMetadata.Id`、`WidgetDescriptor.Id`、`SettingsSectionDescriptor.Id`、`CommandDescriptor.Id`
  一律使用稳定字符串；用户布局/禁用状态/设置都按 Id 关联，改名会丢用户数据。
- **轻量创建**：`CreateView` 会被多次调用（组件被移除再添加），不要在构造函数里做 IO；缓存到字段可减少开销。
- **必须释放订阅**：组件视图实现 `IDisposable` 并解除事件；否则视图被移除后仍会收到回调。
- **不要跨线程碰 UI**：优先在 UI 线程创建视图；后台回调统一 `Dispatcher.BeginInvoke`。
- **不要打包契约程序集**：`HeartRateMonitor.Core` / `HeartRateMonitor.Extensibility` 由宿主提供。
- **配置写入**：修改 `ctx.Settings` 属性后调用 `await ctx.Settings.SaveAsync()` 才会落库；
  批量修改可等设置窗“保存并关闭”统一保存（插件设置页的修改也遵循此机制）。
- **面向 `net8.0-windows10.0.22621.0`（或更低 `net8.0-windows`）**：与宿主 TFM 匹配最稳妥。
- **失败要优雅**：`Register` 中单个描述符构造失败应由插件自行 try/catch，避免整个插件加载失败。

---

## 完整示例：呼吸引导

仓库内可运行示例位于 **[samples/Breathing](../samples/Breathing)**：

| 文件 | 作用 |
|------|------|
| `plugin.json` | 清单：id=`breathing`，入口 `HeartRateMonitor.Plugin.Breathing.BreathingPlugin` |
| `BreathingPlugin.cs` | 注册 1 个组件（呼吸引导）、1 个设置页（呼吸引导）、1 个托盘命令（说明） |
| `BreathingWidget.cs` | 4 秒吸气 / 2 秒屏息 / 6 秒呼气动画，带当前心率显示 |
| `BreathingSettingsView.cs` | 纯 C# 构建的设置页说明 |

构建：

```bash
dotnet build samples/Breathing/Breathing.csproj -c Debug
```

项目自带 `StagePluginDirectory` 目标，会把 dll 与 `plugin.json` 复制到输出的 `plugin\` 子目录；
把该目录内容复制到 `%LOCALAPPDATA%\HeartRateMonitor\plugins\Breathing\`，然后在 设置 → 功能插件 → 重新扫描，
即可在组件库看到「呼吸引导」，在设置左侧看到「呼吸引导」页，在托盘右键菜单看到对应命令。

---

## 附：契约 API 速查

| 类型 | 说明 |
|------|------|
| `IAppPlugin` | 插件入口：`Metadata` + `Register(context, registrar)` + `Shutdown()` |
| `PluginMetadata` | `Id` / `Name` / `Description` / `Version` / `Author` |
| `IPluginRegistrar` | `AddWidget` / `AddCommand` / `AddSettingsSection` |
| `WidgetDescriptor` | `Id` / `Title` / `Description` / `Icon` / `Order` / `Removable` / `DefaultEnabled` / `CreateView` |
| `SettingsSectionDescriptor` | `Id` / `Title` / `Icon` / `CreateView` |
| `CommandDescriptor` | `Id` / `Title` / `Icon` / `Execute` |
| `IPluginContext` | `Services` / `Log` / `Session` / `Ble` / `Settings` / `DataDirectory` / `PluginId` |
| `IWidgetContext` | 继承 `IPluginContext`，增加 `Remove()` |
| `IPluginHost` | `Plugins` / `Widgets` / `Commands` / `SettingsSections` / `PluginsDirectory` / `PluginsChanged` / `InitializeAsync` / `EnableAsync` / `DisableAsync` / `RescanAsync` / `ReloadAsync` / `UninstallAsync` |
| `PluginInfo` | 插件列表项：`Id` / `Name` / `Source` / `Version` / `IsEnabled` / `Error` / `CanUninstall` / `Location` |
| `IHeartRateSession` | `CurrentBpm` / `Latest` / `StartedAt` / `Duration` / `MinBpm` / `MaxBpm` / `AverageBpm` / `SampleCount` / `Calories` / `Points` / 事件 / `Reset()` |
