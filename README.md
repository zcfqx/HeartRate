<div align="center">

# HeartRate Monitor

**Windows 桌面悬浮窗应用 — 实时监控华为手环心率数据**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?logo=windows)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)](#许可证)
[![Tests](https://img.shields.io/badge/Tests-79%20passed-brightgreen.svg)]()
[![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp)]()

通过蓝牙低功耗（BLE）协议连接华为手环，在无边框悬浮窗中实时显示心率数值、趋势曲线与统计信息。
**v2 已重构为轻量、可插拔的组件化看板**：界面重新设计，依赖大幅裁剪，功能全部以插件形式增删。
**v2.2 优化**：修复扫描列表不显示设备的根因（设备行永远被隐藏）、新增「仅心率设备」筛选与按心率优先 + 信号强度排序、
设备名自动升级（匿名广播「心率传感器」→ 真实名称）；极简模式重新设计为**红色渐变心跳图标 + 发光 + 心跳节拍光环**，
只保留图标与数字，背景右键可一键切到 **0%（完全透明）** 且仍可拖动/右键，新增 `Ctrl+M` 快捷切换。

[功能特性](#功能特性) | [界面预览](#界面预览) | [快速开始](#快速开始) | [插件机制](#插件机制) | [项目架构](#项目架构) | [文档索引](#文档索引)

</div>

---

## 目录

- [功能特性](#功能特性)
- [界面预览](#界面预览)
- [系统要求](#系统要求)
- [快速开始](#快速开始)
- [使用方法](#使用方法)
- [插件机制](#插件机制)
- [项目架构](#项目架构)
- [配置说明](#配置说明)
- [数据与日志路径](#数据与日志路径)
- [心率区间](#心率区间)
- [BLE 协议说明](#ble-协议说明)
- [运行测试](#运行测试)
- [发布与体积](#发布与体积)
- [故障排查](#故障排查)
- [文档索引](#文档索引)
- [贡献指南](#贡献指南)
- [许可证](#许可证)

---

## 功能特性

### 核心功能

- **BLE 设备扫描与连接** — 扫描周围 BLE 设备，默认「仅心率设备」过滤耳机/触控笔等噪音，按「心率设备优先 + 信号强度」排序；设备列表内嵌在悬浮窗组件中
- **实时心率接收** — 基于标准蓝牙心率服务（GATT `0x180D` / `0x2A37`）接收并解析心率数据
- **轻量趋势曲线** — 自绘 `Sparkline` 控件（平滑贝塞尔曲线 + 半透明面积），不再依赖 SkiaSharp/LiveCharts
- **心率区间识别** — 五区间自动分类（放松 / 燃脂 / 有氧 / 无氧 / 极限），颜色映射
- **实时统计** — 当前/平均/最高/最低心率、RR 间期、卡路里估算、本次会话时长
- **自动重连** — 设备断开后自动重连（最多 3 次，递增退避），启动时可选自动回连上次设备

### 界面特性

- **无边框圆角悬浮窗** — 置顶显示，可拖动、可调整宽度、可调不透明度
- **组件化看板** — 功能以「组件卡片」排列，随时从组件库增删、进入编辑模式移除
- **极简模式** — 只显示红色心跳图标（渐变填充 + 发光 + 按心率节拍跳动）与大号数字，可右键开关趋势曲线；`Ctrl+M`、顶栏按钮、托盘菜单、设置页均可切换
- **背景完全透明** — 不透明度可调到 0%（只保留心跳与数字），0% 时窗口仍可拖动、右键；极简模式右键菜单提供 100% / 90% / 80% / 0% 快捷预设
- **深色 / 浅色主题** — 运行时即时切换，8 种强调色可选，无需重启（v2.1 修正深色主题下部分文字继承黑色的问题）
- **设置中心** — 左侧导航式设置窗：外观 / 连接 / 提醒 / 数据 / 插件贡献页 / 功能插件 / 关于
- **系统托盘** — 自研 Win32 托盘图标（P/Invoke），右键菜单含插件命令，关闭窗口默认最小化到托盘
- **单实例守护** — 重复启动会唤醒已有窗口；若旧进程卡死占用实例锁，会自动结束它并继续启动（v2.1）

### 插件化

- **可增删的功能** — 组件（Widget）、设置页（Settings Section）、托盘命令（Command）三类扩展点
- **内置插件 + 外部插件同 API** — 内置 4 个插件编译进主程序；外部插件放入插件目录即可被发现
- **程序集隔离** — 每个外部插件独立可回收 `AssemblyLoadContext`，共享 `HeartRateMonitor.*` 契约程序集
- **热管理** — 设置页内启用 / 禁用 / 重新加载 / 卸载 / 重新扫描，禁用状态持久化

### 数据管理

- **本地持久化** — SQLite 存储心率记录与设备信息（会话数据按 1s 节流写库）
- **设备记忆** — 保存上次连接设备，支持启动自动回连
- **数据清理** — 按保留天数清理历史数据
- **文件日志** — 内置 `FileLogger` 按天写文件，单文件 4MB 轮转，保留 7 天

---

## 界面预览

| 主悬浮窗（深色） | 组件库浮层 |
|:---:|:---:|
| ![主悬浮窗](docs/screenshots/main-dark.png) | ![组件库](docs/screenshots/main-add-panel.png) |
| 组件卡片看板 + 连接状态 + 会话底栏 | 从组件库添加组件（含外部插件贡献的「呼吸引导」） |

| 设置中心（深色） | 设置中心（浅色） |
|:---:|:---:|
| ![设置-深色](docs/screenshots/settings-dark.png) | ![设置-浅色](docs/screenshots/settings-light.png) |
| 左侧导航 + 外观页 + 插件贡献的「呼吸引导」页 | 浅色主题即时切换，强调色可换 |

| 极简模式（带曲线） | 极简模式（无曲线） | 极简模式（背景 0% 透明） |
|:---:|:---:|:---:|
| ![极简-曲线](docs/screenshots/main-minimal.png) | ![极简-无曲线](docs/screenshots/main-minimal-nochart.png) | ![极简-透明](docs/screenshots/main-minimal-transparent.png) |
| 红色心跳图标 + 心率数字（离线渲染时为 `--`） | 右键菜单「显示趋势曲线」可关 | 背景完全透明，只留心跳与数字 |

| 实机连接（看板） | 极简模式（实机连接） |
|:---:|:---:|
| ![实机连接](docs/screenshots/main-connected.png) | ![极简-实机](docs/screenshots/main-minimal-connected.png) |
| 连接 HUAWEI Band HR-8F4：65 BPM、区间、趋势、会话时长 | 红色心跳 + 大号数字 + 红色趋势曲线 |

> 前 8 张由仓库内 `tools/HeartRateMonitor.UiSmoke` 离线渲染生成（`RenderTargetBitmap`，非系统截图）：`dotnet run --project tools/HeartRateMonitor.UiSmoke`。运行前请先退出正在运行的应用（单实例）。`main-connected.png` / `main-minimal-connected.png` 为连接真实手环时的运行时截图。更多设计说明见 [docs/UI-REDESIGN.md](docs/UI-REDESIGN.md)。

---

## 系统要求

| 项目 | 要求 |
|------|------|
| **操作系统** | Windows 10 (build 19041+) 或 Windows 11 |
| **运行时** | [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **蓝牙** | Bluetooth 4.0+ (BLE) 适配器 |
| **内存** | 100 MB 可用内存 |

---

## 快速开始

### 先决条件

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 支持 BLE 的蓝牙适配器

### 构建与运行

```bash
git clone https://github.com/<your-username>/HeartRate.git
cd HeartRate

# 构建整个解决方案（5 个项目）
dotnet build src/HeartRateMonitor.sln -c Debug

# 开发模式运行
dotnet run --project src/HeartRateMonitor.App/HeartRateMonitor.App.csproj
```

> **受限沙箱提示：** 若 `dotnet build` 在“正在确定要还原的项目…”之后立即失败且不报错，说明当前终端无法建立 MSBuild 并行节点。
> 在命令后追加 `-m:1 -p:BuildInParallel=false` 即可正常构建，例如：
> `dotnet build src/HeartRateMonitor.sln -c Debug -m:1 -p:BuildInParallel=false`

### 发布

```bash
# 框架依赖单文件（体积最小，需目标机器安装 .NET 8 Desktop Runtime）
dotnet publish src/HeartRateMonitor.App/HeartRateMonitor.App.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true -o publish/framework-dependent

# 自包含单文件（免安装运行时，开启压缩后约 75 MB）
dotnet publish src/HeartRateMonitor.App/HeartRateMonitor.App.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish/self-contained
```

### 快捷启动图标

应用图标为 `src/HeartRateMonitor.App/Assets/app.ico`（多尺寸，由 `scripts/generate_app_icon.py` 生成），
构建时内嵌进 exe 并作为窗口/托盘图标（从内嵌资源解码，不依赖 `pack://`，单文件发布同样有效）。

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Install-Shortcut.ps1            # 桌面 + 开始菜单
powershell -ExecutionPolicy Bypass -File scripts\Install-Shortcut.ps1 -AutoStart # 额外开机自启
powershell -ExecutionPolicy Bypass -File scripts\Install-Shortcut.ps1 -Uninstall # 删除快捷方式
```

---

## 使用方法

### 连接设备

1. **开启手环心率广播** — 华为手环进入运动模式，或在设置中开启「心率广播」
2. **启动应用** — 运行 HeartRate Monitor
3. **扫描并连接** — 在「设备连接」组件卡片中点击**扫描**，从列表中选择手环连接
4. **查看数据** — 连接成功后，心率、趋势、区间、统计组件实时更新

> 首次连接成功后会记住设备；下次启动如开启「自动连接」，应用会在后台自动回连。

### 主界面说明

```
┌──────────────────────────────────────────┐
│ ♥ 心率监控 ● 已连接   ＋  ✎  ⚙  ─  ✕   │  顶栏：状态 + 添加组件/编辑/设置/最小化/关闭
├──────────────────────────────────────────┤
│ ┌──────────────────────────────────────┐ │
│ │ ♥ 心率                     核心显示  │ │  组件卡片（可增删）
│ │   72 BPM                ♡            │ │
│ │   [燃脂]                             │ │
│ └──────────────────────────────────────┘ │
│ ┌──────────────────────────────────────┐ │
│ │ 📈 心率趋势                 核心显示  │ │
│ │  ╭─╮   ╭──╮    ╭╮   最近趋势  72     │ │
│ │ ╯   ╰─╯    ╰──╯ ╰─╯                  │ │
│ └──────────────────────────────────────┘ │
│ ┌──────────────────────────────────────┐ │
│ │ 设备连接                   设备连接   │ │
│ │ ● 已连接  华为手环 8          [断开]  │ │
│ └──────────────────────────────────────┘ │
├──────────────────────────────────────────┤
│ 本次会话 12:34 · 华为手环 8      ＋添加组件│  底栏：会话时长 + 设备名 + 添加组件
└──────────────────────────────────────────┘
```

| 操作 | 说明 |
|------|------|
| 拖动顶栏 | 移动悬浮窗；位置自动记忆 |
| 点击 ＋ | 打开组件库浮层，点击任意组件加入看板 |
| 点击 ✎ | 进入编辑模式，组件右上角出现移除按钮 |
| 点击 ⚙ | 打开设置中心 |
| 点击 ⤢（极简按钮） | 切换到极简模式 |
| 点击 ─ / 关闭 | 最小化到托盘（可关闭「关闭时最小化」改为退出） |
| 托盘图标 | 左键显示/隐藏窗口，右键菜单：显示主窗口 / 设置 / 极简模式 / 插件命令 / 退出 |

### 极简模式

只显示「心跳图标 + 大号 BPM + 当前区间」的小窗口，适合长期贴在桌面角落：

| 操作 | 说明 |
|------|------|
| 顶栏的极简按钮（或托盘菜单「极简模式」） | 进入 / 退出极简模式 |
| 极简窗口内双击 | 退出极简模式 |
| 极简窗口内拖动 | 移动窗口（位置与普通模式共用） |
| 极简窗口右键 | 显示趋势曲线（可勾选开关）/ 退出极简 / 设置 / 最小化 / 关闭 |
| 设置 → 外观 → 极简模式 | 默认进入极简、是否显示趋势曲线 |
| 设置 → 外观 → 不透明度 | 调到 0% 时窗口背景完全透明，只保留文字与图形 |

### 内置组件

| 组件 | 说明 | 默认显示 |
|------|------|:--------:|
| `bpm` 心率 | 大号 BPM、单位、区间标签、心跳脉冲动画 | ✅ |
| `chart` 心率趋势 | 自绘 Sparkline 曲线 + 最近段最小/最大值 | ✅ |
| `zone` 心率区间 | 五段区间条，高亮当前区间 | — |
| `device` 设备连接 | 内嵌扫描、设备列表、连接/断开 | ✅ |
| `stats` 会话统计 | 本次会话平均 / 最高 / 最低 | — |
| `session` 本次会话 | 时长、卡路里、采样数、开始新会话 | — |
| `clock` 时钟 | 当前时间与日期 | — |

### 设置中心

| 页面 | 内容 |
|------|------|
| 外观 | 深色/浅色主题、8 种强调色、不透明度、窗口宽度、托盘/开机自启 |
| 连接 | 当前连接状态、重连、断开、打开系统蓝牙设置、自动连接 |
| 提醒 | 通知/声音开关、心率上下限阈值 |
| 数据 | 数据保留天数、清理数据、打开数据/日志目录 |
| 功能插件 | 插件列表：启用/禁用、重新加载、卸载、打开插件目录、重新扫描 |
| 关于 | 版本、插件统计、数据/插件路径 |
| 插件贡献页 | 由插件通过 `SettingsSectionDescriptor` 注入（示例：呼吸引导） |

---

## 插件机制

应用的功能完全由插件驱动：**内置 4 个插件**（`core` / `device` / `session` / `extras`）编译进主程序，
**外部插件**放在插件目录，启动时被发现并加载。

### 插件能提供什么

| 扩展点 | 描述符 | 表现 |
|--------|--------|------|
| 组件 | `WidgetDescriptor` | 出现在「组件库」，可加入主看板 |
| 设置页 | `SettingsSectionDescriptor` | 作为一页加入设置中心左侧导航 |
| 命令 | `CommandDescriptor` | 出现在托盘右键菜单（可执行任意逻辑） |

### 插件放置位置

```text
%LOCALAPPDATA%\HeartRateMonitor\plugins\
└── Breathing\                   # 一个插件一个目录
    ├── plugin.json              # 清单：id / name / entryAssembly / entryType ...
    └── HeartRateMonitor.Plugin.Breathing.dll
```

也支持 `AppContext.BaseDirectory\plugins\`（便携部署）。插件数据目录为
`%LOCALAPPDATA%\HeartRateMonitor\plugin-data\<插件 id>\`。

最小清单：

```json
{
  "id": "breathing",
  "name": "呼吸引导",
  "version": "1.0.0",
  "description": "跟随动画节奏呼吸，降低心率。",
  "author": "you",
  "entryAssembly": "HeartRateMonitor.Plugin.Breathing.dll",
  "entryType": "HeartRateMonitor.Plugin.Breathing.BreathingPlugin"
}
```

入口实现 `IAppPlugin`：

```csharp
public sealed class BreathingPlugin : IAppPlugin
{
    public PluginMetadata Metadata => new()
    {
        Id = "breathing",
        Name = "呼吸引导",
        Description = "跟随动画节奏呼吸，降低心率。",
        Version = "1.0.0",
        Author = "you"
    };

    public void Register(IPluginContext context, IPluginRegistrar registrar)
    {
        registrar.AddWidget(new WidgetDescriptor
        {
            Id = "breathing",
            Title = "呼吸引导",
            Icon = "sparkles",              // 逻辑图标键，宿主映射为矢量图标
            Order = 70,
            CreateView = ctx => new BreathingWidget(ctx)
        });
    }
}
```

完整教程、生命周期、隔离细节与调试方法见 **[docs/PLUGIN-DEVELOPMENT.md](docs/PLUGIN-DEVELOPMENT.md)**，
可直接参考可运行示例 **[samples/Breathing](samples/Breathing)**（呼吸引导：组件 + 设置页 + 托盘命令）。

构建并安装示例插件：

```bash
dotnet build samples/Breathing/Breathing.csproj -c Debug
# 产物：samples/Breathing/bin/Debug/net8.0-windows10.0.22621.0/plugin/
# 将该目录整体复制为 %LOCALAPPDATA%\HeartRateMonitor\plugins\Breathing
# 然后在 设置 → 功能插件 → 重新扫描
```

---

## 项目架构

### 技术栈

| 组件 | 技术 | 说明 |
|------|------|------|
| UI 框架 | WPF / .NET 8 | 无边框悬浮窗、XAML + 纯 C# 组件视图 |
| MVVM | CommunityToolkit.Mvvm 8.3.2 | 源生成器、`ObservableProperty`、`RelayCommand` |
| BLE 通信 | Windows BLE API | `Windows.Devices.Bluetooth` 原生 API |
| 图表 | 自研 `Sparkline` | `FrameworkElement` + `StreamGeometry` 自绘 |
| 数据库 | SQLite + Dapper | 本地关系型存储 |
| 日志 | 自研 `FileLogger` | 按天滚动、4MB 轮转、保留 7 天 |
| 托盘 | 自研 `TrayIcon` | `Shell_NotifyIcon` P/Invoke |
| 依赖注入 | Microsoft.Extensions.DI 8.0.1 | 内置 DI 容器 |
| 测试 | MSTest 3.1.1 | 79 个单元测试 |

### 轻量化：依赖裁剪对比

| 依赖 | v1 | v2 | 替代方案 |
|------|:--:|:--:|----------|
| LiveChartsCore.SkiaSharpView.WPF（含 SkiaSharp 原生库） | ✅ | ❌ 移除 | 自绘 `Sparkline` |
| Serilog + Sinks.File | ✅ | ❌ 移除 | 自研 `FileLogger` |
| Hardcodet.NotifyIcon.Wpf | ✅ | ❌ 移除 | 自研 Win32 `TrayIcon` |
| Microsoft.Extensions.Hosting | ✅ | ❌ 移除 | 直接使用 DI 容器 |
| CommunityToolkit.Mvvm | ✅ | ✅ 保留 | — |
| Microsoft.Extensions.DependencyInjection | ✅ | ✅ 保留 | — |

### 分层架构

```
┌────────────────────────────────────────────────────────────┐
│  HeartRateMonitor.App (WPF)                                │
│  Views / ViewModels / Widgets / Themes / Infrastructure    │
│  Extensibility（插件宿主实现：PluginHost / LoadContext）    │
├────────────────────────────────────────────────────────────┤
│  HeartRateMonitor.Extensibility（插件契约，供第三方引用）    │
│  IAppPlugin / IPluginHost / WidgetDescriptor / ...          │
├────────────────────────────────────────────────────────────┤
│  HeartRateMonitor.Services                                 │
│  BleService / HeartRateSession / DataService / Settings     │
│  HeartRateParser / HeartRateCalculator / FileLogger         │
├────────────────────────────────────────────────────────────┤
│  HeartRateMonitor.Core（零外部依赖）                        │
│  Models / Interfaces / Enums / Events / Exceptions          │
├────────────────────────────────────────────────────────────┤
│  HeartRateMonitor.Data                                     │
│  SQLite + Dapper：Database / Entities / Repositories        │
└────────────────────────────────────────────────────────────┘
```

**模块依赖：**

```
HeartRateMonitor.App
    ├── HeartRateMonitor.Core
    ├── HeartRateMonitor.Data
    ├── HeartRateMonitor.Services
    │   ├── HeartRateMonitor.Core
    │   └── HeartRateMonitor.Data
    └── HeartRateMonitor.Extensibility   （插件契约）
            └── HeartRateMonitor.Core
```

### 项目结构

```
HeartRate/
├── src/
│   ├── HeartRateMonitor.App/               # WPF 主程序 + 插件宿主
│   │   ├── Controls/Sparkline.cs           # 自绘趋势曲线
│   │   ├── Converters/Converters.cs        # XAML 值转换器
│   │   ├── Extensibility/                  # 插件宿主实现
│   │   │   ├── PluginHost.cs               # 发现/加载/启停/卸载插件
│   │   │   ├── PluginLoadContext.cs        # 可回收 AssemblyLoadContext
│   │   │   ├── PluginManifest.cs           # plugin.json 解析
│   │   │   ├── PluginContext.cs            # 插件上下文与工厂
│   │   │   └── BuiltInPlugins.cs           # 内置 4 插件
│   │   ├── Infrastructure/
│   │   │   ├── ThemeManager.cs             # 主题/强调色运行时切换
│   │   │   ├── TrayIcon.cs                 # Win32 托盘图标
│   │   │   └── AppIcons.cs                 # 逻辑图标键 → 矢量 Geometry
│   │   ├── Themes/Theme.Dark.xaml          # 深色色板
│   │   ├── Themes/Theme.Light.xaml         # 浅色色板
│   │   ├── Styles/Base.xaml                # 字体/卡片/按钮/开关等样式
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs            # 看板、布局持久化、插件同步
│   │   │   ├── SettingsViewModel.cs        # 设置中心
│   │   │   ├── WidgetTileViewModel.cs      # 组件卡片
│   │   │   ├── PluginItemViewModel.cs      # 插件管理项
│   │   │   └── SettingsNavItem.cs
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml(.cs)        # 无边框悬浮窗
│   │   │   ├── SettingsWindow.xaml(.cs)    # 导航式设置中心
│   │   │   └── Settings/                   # 外观/连接/提醒/数据/插件/关于 页
│   │   ├── Widgets/                        # 7 个内置组件视图
│   │   ├── Assets/app.ico
│   │   └── App.xaml(.cs)
│   │
│   ├── HeartRateMonitor.Extensibility/     # 插件契约程序集
│   │   ├── PluginMetadata.cs               # IAppPlugin / PluginMetadata
│   │   ├── Contributions.cs                # 三类描述符 + IPluginRegistrar
│   │   ├── IPluginContext.cs               # 插件上下文
│   │   └── IPluginHost.cs                  # 宿主接口
│   │
│   ├── HeartRateMonitor.Core/              # 领域层（无外部依赖）
│   │   ├── Models/                         # HeartRateData / HeartRatePoint /
│   │   │                                   # HeartRateZones / WidgetLayoutCodec ...
│   │   ├── Interfaces/                     # IBleService / IHeartRateSession /
│   │   │                                   # IDataService / ISettingsService ...
│   │   ├── Enums/ Events/ Exceptions/
│   │
│   ├── HeartRateMonitor.Services/          # 服务实现
│   │   ├── BLE/BleService.cs
│   │   ├── HeartRate/                      # Parser / Calculator / Service / Session
│   │   ├── DataService/ Settings/ Logging/
│   │
│   └── HeartRateMonitor.Data/              # SQLite + Dapper
│       ├── Database/ Entities/ Repositories/
│
├── samples/Breathing/                      # 外部插件示例（呼吸引导）
├── tests/HeartRateMonitor.Tests/           # 79 个单元测试（8 个测试类）
├── tools/HeartRateMonitor.UiSmoke/         # 界面冒烟检查 + 截图渲染（docs/screenshots）
├── scripts/                                # 图标生成 / 快捷方式安装
├── publish/                                # 发布产物（未跟踪）
└── docs/                                   # 设计文档 + 界面截图
```

---

## 配置说明

配置持久化在 SQLite 的 `Settings` 表（Key/Value），新增键无需改表结构。

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `Theme` | string | `Dark` | 主题：`Dark` / `Light` |
| `AccentColor` | string | `#6366F1` | 强调色（8 个预设色可换） |
| `OverlayOpacity` | double | `1.0` | 悬浮窗不透明度 0.0–1.0（0 = 背景完全透明） |
| `WindowWidth` | double | `320` | 悬浮窗宽度 280–520 |
| `WindowLeft` / `WindowTop` | double | — | 窗口位置 |
| `MinimalMode` | bool | `false` | 是否进入极简模式 |
| `MinimalShowChart` | bool | `true` | 极简模式下是否显示趋势曲线 |
| `Widgets` | string | `""` | 组件布局（JSON 字符串数组，空 = 默认布局） |
| `DisabledPlugins` | string | `""` | 已禁用的插件 id（`;` 分隔） |
| `MinimizeToTray` | bool | `true` | 关闭窗口时最小化到托盘 |
| `StartWithWindows` | bool | `false` | 开机自启 |
| `AutoConnect` | bool | `true` | 启动自动连接上次设备 |
| `LastDeviceId` / `LastDeviceName` | string | — | 上次连接设备 |
| `HighHeartRateThreshold` | int | `160` | 心率上限提醒 |
| `LowHeartRateThreshold` | int | `50` | 心率下限提醒 |
| `EnableNotifications` | bool | `true` | 异常通知 |
| `EnableSoundAlert` | bool | `false` | 声音提醒 |
| `DataRetentionDays` | int | `30` | 数据保留天数 |
| `Language` | string | `zh-CN` | 界面语言（预留给多语言） |

---

## 数据与日志路径

| 内容 | 路径 |
|------|------|
| 数据库 | `%LOCALAPPDATA%\HeartRateMonitor\heartrate.db` |
| 运行日志 | `%LOCALAPPDATA%\HeartRateMonitor\logs\app-yyyyMMdd.log` |
| 启动打点 | `%LOCALAPPDATA%\HeartRateMonitor\startup.log` |
| 托盘图标缓存 | `%LOCALAPPDATA%\HeartRateMonitor\tray.ico` |
| 外部插件 | `%LOCALAPPDATA%\HeartRateMonitor\plugins\<插件名>\` |
| 插件数据 | `%LOCALAPPDATA%\HeartRateMonitor\plugin-data\<插件 id>\` |

> 日志单文件超过 4MB 自动轮转为 `.bak`，并清理 7 天前日志。

---

## 心率区间

| 区间 | 心率范围 | 颜色 |
|------|----------|------|
| 放松 | < 100 BPM | `#6366F1` |
| 燃脂 | 100 – 139 BPM | `#22C55E` |
| 有氧 | 140 – 169 BPM | `#F59E0B` |
| 无氧 | 170 – 199 BPM | `#F97316` |
| 极限 | ≥ 200 BPM | `#EF4444` |

区间定义集中在 `src/HeartRateMonitor.Core/Models/HeartRateZones.cs`（`Classify` / `IndexOf`），
UI 颜色、组件区间条、区间标签全部来源于此，插件也可直接复用。

---

## BLE 协议说明

应用基于 Bluetooth SIG 标准心率服务（Heart Rate Profile）通信。

### 服务与特征值

| 服务/特征 | UUID | 说明 |
|-----------|------|------|
| Heart Rate Service | `0x180D` | 心率服务 |
| Heart Rate Measurement | `0x2A37` | 心率测量值（Notify） |
| Body Sensor Location | `0x2A38` | 传感器位置（可选读取） |
| Heart Rate Control Point | `0x2A39` | 控制点（可选写入） |

### Heart Rate Measurement 数据格式

```
Byte 0: Flags
  ├── Bit 0:   Heart Rate Value Format (0 = UINT8, 1 = UINT16)
  ├── Bit 1-2: Sensor Contact Status
  ├── Bit 3:   Energy Expended Status
  ├── Bit 4:   RR-Interval Status
  └── Bit 5-7: Reserved

Byte 1:      Heart Rate Value (UINT8)
  或
Byte 1-2:    Heart Rate Value (UINT16, 小端序)

[可选] Energy Expended (2 bytes)
[可选] RR-Interval (每组 2 bytes, 单位 1/1024 秒)
```

### 连接流程

```
1. 启动 BLE 广告扫描 (BluetoothLEAdvertisementWatcher)
2. 发现设备，过滤广播数据中的设备名称
3. 用户在设备组件中选择目标设备
4. 建立 GATT 连接 (BluetoothLEDevice.FromIdAsync)
5. 发现心率服务 (GetGattServicesForUuidAsync: 0x180D)
6. 发现心率测量特征值 (GetCharacteristicsForUuidAsync: 0x2A37)
7. 启用通知 (WriteClientCharacteristicConfigurationDescriptor: Notify)
8. 接收心率数据 → HeartRateParser 解析 → HeartRateSession 聚合 → 组件更新
9. 连接断开 → 自动重连（最多 3 次，间隔 2s/4s/6s）
```

---

## 运行测试

```bash
dotnet test tests/HeartRateMonitor.Tests/HeartRateMonitor.Tests.csproj
```

| 测试类 | 覆盖内容 |
|--------|----------|
| `HeartRateParserTests` | UINT8/UINT16 解析、Sensor Contact、RR-Interval、异常输入 |
| `HeartRateCalculatorTests` | 统计计算、区间分类、报告生成 |
| `HeartRateServiceTests` | 心率更新、历史管理、事件触发 |
| `DatabaseTests` | CRUD、查询、数据库初始化 |
| `WidgetLayoutCodecTests` | 组件布局 JSON 解析/序列化、损坏数据回退、去重 |
| `HeartRateZonesTests` | 五区间边界、`Classify`/`IndexOf` 一致性、颜色合法性 |
| `SettingsServiceTests` | WAL 模式、`MinimalShowChart`/透明度 0 往返、并发保存不出现 `database is locked` |
| `BleAdvertisementRulesTests` | 广播过滤规则、占位名升级为真实设备名、心率优先 + 信号排序 |

> 当前共 **79 个用例**，全部通过（受限沙箱内 `vstest`/testhost 会因进程权限失败，可用反射运行器执行）。

### 界面冒烟与截图

```bash
# 先退出正在运行的应用（单实例），再执行：
dotnet run --project tools/HeartRateMonitor.UiSmoke             # 检查并刷新 docs/screenshots
dotnet run --project tools/HeartRateMonitor.UiSmoke -- D:\shots # 输出到指定目录
```

工具复用真实启动流程（同一套 DI 单例、插件宿主、主窗口），逐项断言主题资源、深浅主题切换、插件启停、
组件增删、极简模式与 0% 透明背景，并用 `RenderTargetBitmap` 渲染 8 张截图；结束前自动恢复用户设置。
全部通过时输出 `ALL UI SMOKE CHECKS PASSED` 并以 0 退出，失败时列出具体检查项。

---

## 发布与体积

| 发布方式 | 体积（v2.1） | 对比 v1 |
|----------|------|---------|
| 框架依赖单文件（`--self-contained false`） | exe 28.07 MB（目录 29.77 MB，含 `e_sqlite3.dll`） | v1 ≈ 36.0 MB（**-22%**） |
| 自包含单文件 + 压缩（`EnableCompressionInSingleFile=true`） | exe 75.33 MB（目录 ≈ 85.4 MB） | v1 ≈ 99 MB |
| 自包含单文件（不压缩） | ≈ 181 MB | — |

打包 zip（`publish\`）：`HeartRate-v2.1.0-win-x64-framework-dependent.zip` ≈ 8.66 MB、`HeartRate-v2.1.0-win-x64-self-contained.zip` ≈ 74.45 MB。

主要收益来自移除 SkiaSharp 原生库（`libSkiaSharp.dll` 约 9.6MB 且包体巨大）、Serilog 与第三方托盘库。

---

## 故障排查

### 扫描失败怎么办

扫描前会检测蓝牙可用性并给出具体原因：

| 情况 | 界面表现 | 处理方式 |
|------|----------|----------|
| 蓝牙开关已关闭 | 「蓝牙未开启」+ 提示说明 | 打开系统蓝牙后重试（点提示条中的「打开蓝牙设置」） |
| 没有 BLE 适配器 | 「未检测到可用的低功耗蓝牙（BLE）适配器…」 | 检查蓝牙硬件与驱动 |
| 系统拒绝访问 | 「系统拒绝了蓝牙访问…」 | 检查 Windows 隐私/蓝牙权限 |
| 扫描中蓝牙被关闭/占用 | 「扫描已中断」+ 原因 | 关闭占用程序或重开蓝牙 |

> 未打包桌面应用无法由程序自动打开蓝牙（`Radio.SetStateAsync` 返回 `DeniedByUser`），应用只负责检测与提示。

> **v2.1 扫描规则**：除「有名称的可连接设备」外，只要广播里包含标准心率服务 `0x180D`（即使设备没有名字）也会被列出，名称显示为「心率传感器」，避免手环匿名广播时列表为空。同一设备随后广播出真实名称时会自动升级显示名。

#### 扫描得到 0 台设备

若日志（`%LOCALAPPDATA%\HeartRateMonitor\logs\app-yyyyMMdd.log`）里出现「收到广播」但列表为空，按下面顺序排查：

1. 场景里确实没有心率设备在广播：手环/手表通常需要进入「运动模式」或开启「心率广播」后才会广播 `0x180D`，普通待机状态只广播通用服务；
2. 只看得到耳机等非心率设备：默认开启「仅心率设备」筛选会隐藏它们，点击设备卡头部的筛选按钮可显示全部周边设备；
3. 设备广播了心率服务但名称未知：列表会显示为「心率传感器」，直接点击即可尝试连接；
4. 以上都排除后查看日志中的 `发现设备:` / `设备名称更新:` 行，确认 `BluetoothLEDevice` 是否收到目标设备的广播。

### 设置保存失败怎么办

v2.1 修复了两处与 SQLite 写入相关的问题：

- 数据库启用 **WAL 模式** + `busy_timeout=15s` + 连接池，心率每秒写库不再与设置保存抢锁；
- 设置保存改为**单事务批量写入**（`SettingsRepository.SetManyAsync`），失败时对 `SQLITE_BUSY/LOCKED` 自动重试 3 次。

若仍提示「保存设置失败」，查看 `%LOCALAPPDATA%\HeartRateMonitor\logs\` 中的错误详情；确认磁盘未满、数据库文件未被其他程序独占。

### 界面卡死/无响应

v1 曾出现启动期死锁：在 UI 线程用 `.GetAwaiter().GetResult()` 等待内含 WinRT `await` 的方法是根因。
修复约定（v2 继续遵守）：

- 蓝牙权限检查与自动重连在后台异步执行，**不在 UI 线程等待**；
- `BleService` 内 WinRT 调用一律 `AsTask().ConfigureAwait(false)`；
- 数据库初始化、设置加载、插件初始化用 `Task.Run(...).GetAwaiter().GetResult()`；
- BLE 事件（设备发现/连接状态/扫描失败）统一通过 `WidgetViewBase.RunOnUi` 切回 UI 线程，避免跨线程操作控件（v2.1 修复扫描列表不刷新的根因）。

### 启动时提示“已经在运行”怎么办

v2.1 起单实例行为已改为：

1. **已有实例正常运行** → 新进程通知它把主窗口显示出来（即使窗口被隐藏到托盘），随后新进程静默退出，不再弹「已经在运行」；
2. **已有实例卡死无响应**（例如上次异常退出留下的僵尸进程仍占用实例锁）→ 自动结束该进程并继续启动；
3. 以上都失败时才提示「心率监控已经在运行」。

若仍遇到卡死，可检查是否有残留进程并手动结束：

```powershell
Get-Process HeartRateMonitor.App -ErrorAction SilentlyContinue | Stop-Process -Force
```

启动各阶段打点见 `%LOCALAPPDATA%\HeartRateMonitor\startup.log`（每行带进程号，便于区分多实例）。

若仍异常，查看启动打点日志定位阶段：

```text
%LOCALAPPDATA%\HeartRateMonitor\startup.log
%LOCALAPPDATA%\HeartRateMonitor\logs\app-yyyyMMdd.log
```

### 插件没有出现

1. 确认目录结构正确：`plugins\<插件名>\plugin.json` + dll；
2. 检查清单字段 `id` / `entryAssembly` / `entryType` 与程序集一致；
3. 设置 → 功能插件 → **重新扫描**；
4. 查看日志中的插件加载错误（插件列表会显示错误文字）。

---

## 文档索引

| 文档 | 说明 |
|------|------|
| [docs/UI-REDESIGN.md](docs/UI-REDESIGN.md) | **v2 界面重构说明**：布局、主题系统、组件模型、交互规范 |
| [docs/PLUGIN-DEVELOPMENT.md](docs/PLUGIN-DEVELOPMENT.md) | **插件开发指南**：契约、清单、生命周期、隔离、调试、发布 |
| [docs/01_架构设计文档.md](docs/01_架构设计文档.md) | 分层架构与服务职责（v1 设计，v2 增补见文首） |
| [docs/02_界面设计文档.md](docs/02_界面设计文档.md) | 界面设计（v1；v2 见 UI-REDESIGN） |
| [docs/03_接口设计文档.md](docs/03_接口设计文档.md) | 接口契约 |
| [docs/04_数据库设计文档.md](docs/04_数据库设计文档.md) | SQLite 表结构与仓储 |
| [docs/05_BLE通信设计文档.md](docs/05_BLE通信设计文档.md) | BLE 连接与数据解析细节 |
| [docs/06_测试设计文档.md](docs/06_测试设计文档.md) | 测试设计 |
| [docs/07_前端样式规范.md](docs/07_前端样式规范.md) | 样式规范（v1） |
| [docs/08_交互逻辑与前端表现规范.md](docs/08_交互逻辑与前端表现规范.md) | 交互规范（v1） |
| [docs/心率监控软件需求文档.md](docs/心率监控软件需求文档.md) | 需求文档 |

---

## 贡献指南

1. Fork 仓库并创建分支：`git checkout -b feature/amazing-feature`
2. 提交遵循 [Conventional Commits](https://www.conventionalcommits.org/)：`feat:` / `fix:` / `docs:` / `refactor:` / `test:` / `chore:`
3. 确保 `dotnet build src/HeartRateMonitor.sln` 与测试通过
4. 新功能附带单元测试；涉及插件契约的改动需同步更新 `docs/PLUGIN-DEVELOPMENT.md`
5. 提交 Pull Request 并说明动机与改动

---

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

```
MIT License

Copyright (c) 2026 HeartRate Monitor Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 致谢

- [Bluetooth Heart Rate Profile Specification](https://www.bluetooth.com/specifications/specs/heart-rate-profile-1-0/) — Bluetooth SIG 心率服务规范
- [Windows BLE API Documentation](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM 框架
- [Dapper](https://github.com/DapperLib/Dapper) — 轻量 ORM
- [Segoe Fluent Icons](https://learn.microsoft.com/windows/apps/design/style/segoe-fluent-icons-font) — 界面图标字体
