<div align="center">

# HeartRate Monitor

**Windows 桌面悬浮窗应用 — 实时监控华为手环心率数据**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?logo=windows)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)](#许可证)
[![Build](https://img.shields.io/badge/Build-Passing-brightgreen.svg)]()
[![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp)]()

通过蓝牙低功耗（BLE）协议连接华为手环，在透明悬浮窗中实时显示心率数值、趋势图表和统计数据。

[功能特性](#功能特性) | [快速开始](#快速开始) | [使用方法](#使用方法) | [配置说明](#配置说明) | [贡献指南](#贡献指南)

</div>

---

## 目录

- [功能特性](#功能特性)
- [截图预览](#截图预览)
- [系统要求](#系统要求)
- [快速开始](#快速开始)
  - [先决条件](#先决条件)
  - [安装步骤](#安装步骤)
  - [构建与运行](#构建与运行)
- [使用方法](#使用方法)
  - [连接设备](#连接设备)
  - [界面说明](#界面说明)
  - [系统托盘](#系统托盘)
- [配置说明](#配置说明)
  - [应用设置](#应用设置)
  - [心率阈值设置](#心率阈值设置)
  - [数据管理设置](#数据管理设置)
- [项目架构](#项目架构)
  - [技术栈](#技术栈)
  - [分层架构](#分层架构)
  - [项目结构](#项目结构)
- [支持设备](#支持设备)
- [BLE 协议说明](#ble-协议说明)
- [运行测试](#运行测试)
- [API 参考](#api-参考)
- [贡献指南](#贡献指南)
- [许可证](#许可证)
- [致谢](#致谢)

---

## 功能特性

### 核心功能

- **BLE 设备扫描与连接** — 自动扫描周围蓝牙低功耗设备，支持手动选择华为手环连接
- **实时心率接收** — 基于标准蓝牙心率服务（GATT UUID: `0x180D`）接收并解析心率数据
- **心率趋势图表** — 使用 LiveCharts2 实时绘制心率折线图，自动缩放 Y 轴范围
- **心率区间识别** — 自动分类 5 种心率区间（静息 / 燃脂 / 有氧 / 极限 / 最大），以不同颜色标识
- **实时统计** — 动态计算并显示平均心率、最高心率、最低心率
- **RR 间期解析** — 提取心跳间期（RR-Interval）数据，用于心率变异性（HRV）分析
- **自动重连** — 设备断开连接后自动尝试重新连接（最多 3 次，递增退避间隔）

### 界面特性

- **透明悬浮窗** — 背景完全透明，窗口置顶显示，不遮挡其他应用程序
- **心率颜色映射** — 心率数值根据当前区间动态变换颜色
- **连接状态指示** — 实时显示扫描中 / 连接中 / 已连接 / 重连中 / 未连接等状态
- **设备选择窗口** — 独立的设备选择对话框，支持扫描结果列表与信号强度显示
- **设置窗口** — 完整的配置管理界面

### 数据管理

- **本地数据持久化** — 使用 SQLite 数据库存储心率记录和设备信息
- **设备记忆** — 自动保存已配对设备信息，支持启动时自动回连
- **历史数据查询** — 支持按时间范围查询历史心率记录
- **每日报告** — 自动生成每日心率统计报告

---

## 截图预览

> 将截图文件放置于 `docs/images/` 目录下，以下为推荐截图：

| 主悬浮窗 | 设备选择 | 设置界面 |
|:---:|:---:|:---:|
| ![主界面](docs/images/main-window.png) | ![设备选择](docs/images/device-picker.png) | ![设置](docs/images/settings.png) |
| 透明背景 + 心率图表 | BLE 设备扫描列表 | 完整配置管理 |

---

## 系统要求

| 项目 | 要求 |
|------|------|
| **操作系统** | Windows 10 (build 19041+) 或 Windows 11 |
| **运行时** | [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **蓝牙** | Bluetooth 4.0+ (BLE) 适配器 |
| **屏幕** | 1920×1080 及以上分辨率（推荐） |
| **内存** | 100 MB 可用内存 |

### 性能指标

| 指标 | 目标值 |
|------|--------|
| CPU 占用 | < 5% |
| 内存占用 | < 100 MB |
| 数据延迟 | < 2 秒 |
| 启动时间 | < 3 秒 |

---

## 快速开始

### 先决条件

确保已安装以下工具：

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（包含运行时和开发工具）
- [Git](https://git-scm.com/downloads)（用于克隆仓库）
- 支持 BLE 的蓝牙适配器（内置或 USB 外置）

### 安装步骤

**1. 克隆仓库**

```bash
git clone https://github.com/<your-username>/HeartRate.git
cd HeartRate
```

**2. 还原依赖包**

```bash
dotnet restore src/HeartRateMonitor.App/HeartRateMonitor.App.csproj
```

**3. 构建项目**

```bash
dotnet build src/HeartRateMonitor.App/HeartRateMonitor.App.csproj -c Release
```

### 构建与运行

**开发模式运行：**

```bash
dotnet run --project src/HeartRateMonitor.App/HeartRateMonitor.App.csproj
```

**发布独立程序：**

```bash
dotnet publish src/HeartRateMonitor.App/HeartRateMonitor.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

发布产物位于 `src/HeartRateMonitor.App/bin/Release/net8.0-windows10.0.22621.0/publish/` 目录下。

---

## 使用方法

### 连接设备

1. **开启手环心率广播** — 在华为手环上进入运动模式，或在设置中开启"心率广播"功能
2. **启动应用** — 运行 HeartRate Monitor 应用程序
3. **扫描设备** — 点击主界面的扫描按钮，应用将搜索附近的 BLE 心率设备
4. **选择设备** — 从设备列表中选择你的华为手环，点击连接
5. **查看心率** — 连接成功后，悬浮窗将实时显示心率数值和趋势图表

> **提示：** 首次连接成功后，应用会记住设备信息。下次启动时如开启"自动连接"，将自动尝试回连。

### 界面说明

```
┌──────────────────────────────────────┐
│                                      │
│     ♥  72 BPM                        │  心率图标 + 实时数值
│     ─────────────────────            │
│     │    /\    /\                    │  心率趋势折线图
│     │   /  \  /  \    /\            │
│     │  /    \/    \  /  \           │
│     │ /            \/    \          │
│     └─────────────────────          │
│     Avg: 68  Max: 85  Min: 62       │  统计信息
│     静息 · 信号良好                   │  心率区间 · 连接状态
│                                      │
└──────────────────────────────────────┘
         ↑ 背景完全透明
```

| 元素 | 说明 |
|------|------|
| 心率数值 | 大号字体显示当前 BPM，颜色随心率区间变化 |
| 心率图标 | 线条心形图标 |
| 趋势图表 | 最近 2 分钟心率折线图，自动缩放 |
| 统计信息 | 平均 / 最高 / 最低心率 |
| 心率区间 | 静息（紫）/ 燃脂（绿）/ 有氧（黄）/ 极限（橙）/ 最大（红） |
| 连接状态 | 指示当前 BLE 连接状态 |

**心率区间颜色对照：**

| 区间 | 心率范围 | 颜色 |
|------|----------|------|
| 静息 | < 100 BPM | ![#6366F1](https://via.placeholder.com/12/6366F1/6366F1.png) `#6366F1` |
| 燃脂 | 100 - 139 BPM | ![#22C55E](https://via.placeholder.com/12/22C55E/22C55E.png) `#22C55E` |
| 有氧 | 140 - 169 BPM | ![#F59E0B](https://via.placeholder.com/12/F59E0B/F59E0B.png) `#F59E0B` |
| 极限 | 170 - 199 BPM | ![#F97316](https://via.placeholder.com/12/F97316/F97316.png) `#F97316` |
| 最大 | ≥ 200 BPM | ![#EF4444](https://via.placeholder.com/12/EF4444/EF4444.png) `#EF4444` |

### 系统托盘

应用支持最小化到系统托盘，双击托盘图标可恢复窗口显示。关闭主窗口时，应用将自动最小化到托盘而非退出。

---

## 配置说明

应用的所有配置项均通过设置界面管理，并持久化存储于 SQLite 数据库中。

### 应用设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `Theme` | string | `Dark` | 界面主题（`Dark` / `Light`） |
| `Language` | string | `zh-CN` | 界面语言 |
| `OverlayOpacity` | double | `1.0` | 悬浮窗透明度（0.5 - 1.0） |
| `StartWithWindows` | bool | `false` | 是否开机自启动 |
| `MinimizeToTray` | bool | `true` | 关闭窗口时最小化到系统托盘 |
| `MinimalMode` | bool | `false` | 极简模式（隐藏图表） |

### 心率阈值设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `HighHeartRateThreshold` | int | `160` | 心率上限报警阈值（BPM） |
| `LowHeartRateThreshold` | int | `50` | 心率下限报警阈值（BPM） |
| `EnableNotifications` | bool | `true` | 启用心率异常通知 |
| `EnableSoundAlert` | bool | `false` | 启用声音报警 |

### 数据管理设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `AutoConnect` | bool | `true` | 启动时自动连接上次设备 |
| `LastDeviceId` | string | — | 上次连接的设备 ID |
| `LastDeviceName` | string | — | 上次连接的设备名称 |
| `DataRetentionDays` | int | `30` | 心率数据保留天数 |

### 数据存储路径

| 数据类型 | 路径 |
|----------|------|
| 数据库文件 | `%LocalAppData%/HeartRateMonitor/heartrate.db` |
| 日志文件 | `%LocalAppData%/HeartRateMonitor/logs/app-{date}.log` |

---

## 项目架构

### 技术栈

| 层级 | 技术 | 版本 | 说明 |
|------|------|------|------|
| UI 框架 | WPF | .NET 8 | 透明窗口、硬件加速渲染 |
| MVVM 框架 | CommunityToolkit.Mvvm | 8.3.2 | 源生成器、ObservableProperty、RelayCommand |
| BLE 通信 | Windows BLE API | — | `Windows.Devices.Bluetooth` 原生 API |
| 图表库 | LiveCharts2 (SkiaSharp) | 2.0.0-rc4.5 | 高性能实时折线图 |
| 数据库 | SQLite + Dapper | — | 轻量级本地关系型数据库 |
| 日志 | Serilog | 4.1.0 | 结构化日志，按天滚动写入文件 |
| 系统托盘 | Hardcodet.NotifyIcon.Wpf | 2.0.1 | Windows 系统托盘图标支持 |
| 依赖注入 | Microsoft.Extensions.DI | 8.0.1 | 内置依赖注入容器 |
| 测试框架 | MSTest | 3.1.1 | 单元测试 |

### 分层架构

项目采用 **MVVM + 分层架构** 设计，遵循依赖倒置原则（DIP）：

```
┌───────────────────────────────────────────────────────────────┐
│                  Presentation Layer (WPF)                     │
│   Views (XAML)  ←→  ViewModels (C#)  ←→  Converters         │
├───────────────────────────────────────────────────────────────┤
│                  Service Layer                                │
│   BleService │ HeartRateService │ DataService │ SettingsService│
├───────────────────────────────────────────────────────────────┤
│                  Domain Layer (Core)                          │
│   Models │ Interfaces │ Enums │ Events                        │
├───────────────────────────────────────────────────────────────┤
│                  Infrastructure Layer                         │
│   Windows BLE API │ SQLite + Dapper │ Serilog                 │
└───────────────────────────────────────────────────────────────┘
```

**模块依赖关系：**

```
HeartRateMonitor.App
    ├── HeartRateMonitor.Core          (无外部依赖)
    ├── HeartRateMonitor.Services
    │   ├── HeartRateMonitor.Core
    │   └── HeartRateMonitor.Data
    └── HeartRateMonitor.Data
        └── HeartRateMonitor.Core
```

### 项目结构

```
HeartRate/
├── src/
│   ├── HeartRateMonitor.App/                 # WPF 主应用程序
│   │   ├── ViewModels/                       # 视图模型
│   │   │   ├── MainViewModel.cs              # 主窗口逻辑、图表数据、事件处理
│   │   │   ├── SettingsViewModel.cs          # 设置管理
│   │   │   └── DevicePickerViewModel.cs      # 设备扫描与选择
│   │   ├── Views/                            # XAML 视图
│   │   │   ├── MainWindow.xaml(.cs)          # 主悬浮窗
│   │   │   ├── SettingsWindow.xaml(.cs)      # 设置窗口
│   │   │   └── DevicePickerWindow.xaml(.cs)  # 设备选择窗口
│   │   ├── Converters/                       # XAML 值转换器
│   │   ├── Styles/                           # 主题与样式资源
│   │   ├── App.xaml(.cs)                     # 应用入口、依赖注入配置
│   │   └── HeartRateMonitor.App.csproj
│   │
│   ├── HeartRateMonitor.Core/                # 核心业务逻辑层（零外部依赖）
│   │   ├── Models/                           # 数据模型
│   │   │   ├── HeartRateData.cs              # 心率数据模型
│   │   │   ├── BleDevice.cs                  # BLE 设备模型
│   │   │   ├── HeartRateStatistics.cs        # 心率统计模型
│   │   │   ├── HeartRateZone.cs              # 心率区间模型
│   │   │   └── DailyReport.cs                # 每日报告模型
│   │   ├── Interfaces/                       # 服务接口契约
│   │   │   ├── IBleService.cs                # 蓝牙服务接口
│   │   │   ├── IHeartRateService.cs          # 心率服务接口
│   │   │   ├── IDataService.cs               # 数据服务接口
│   │   │   ├── ISettingsService.cs           # 设置服务接口
│   │   │   ├── IHeartRateParser.cs           # 数据解析接口
│   │   │   ├── IHeartRateCalculator.cs       # 统计计算接口
│   │   │   └── ILogger.cs                    # 日志接口
│   │   ├── Enums/                            # 枚举定义
│   │   └── Events/                           # 事件参数
│   │
│   ├── HeartRateMonitor.Services/            # 服务层实现
│   │   ├── BLE/
│   │   │   └── BleService.cs                 # BLE 扫描、GATT 连接、数据接收
│   │   ├── HeartRate/
│   │   │   ├── HeartRateService.cs           # 心率数据管理与事件分发
│   │   │   ├── HeartRateParser.cs            # BLE 原始数据解析（UINT8/UINT16/RR）
│   │   │   └── HeartRateCalculator.cs        # 统计计算、区间分类、报告生成
│   │   ├── DataService/
│   │   │   └── DataService.cs                # 数据持久化封装
│   │   ├── Settings/
│   │   │   └── SettingsService.cs            # 配置管理
│   │   └── SerilogLogger.cs                  # Serilog 日志适配器
│   │
│   └── HeartRateMonitor.Data/                # 数据访问层
│       ├── Database/
│       │   └── DatabaseInitializer.cs        # SQLite 数据库初始化与表创建
│       ├── Entities/                         # 数据库实体
│       │   ├── HeartRateRecordEntity.cs
│       │   ├── DeviceInfoEntity.cs
│       │   └── SettingsEntity.cs
│       └── Repositories/                     # 仓储模式实现
│           ├── HeartRateRepository.cs
│           ├── DeviceRepository.cs
│           └── SettingsRepository.cs
│
├── tests/
│   └── HeartRateMonitor.Tests/               # 单元测试
│       ├── HeartRateParserTests.cs           # 心率数据解析测试（12 个用例）
│       ├── HeartRateCalculatorTests.cs       # 统计计算逻辑测试
│       ├── HeartRateServiceTests.cs          # 心率服务测试
│       └── DatabaseTests.cs                  # 数据库操作测试
│
└── docs/                                     # 项目设计文档
    ├── 心率监控软件需求文档.md
    ├── 01_架构设计文档.md
    ├── 02_界面设计文档.md
    ├── 03_接口设计文档.md
    ├── 04_数据库设计文档.md
    ├── 05_BLE通信设计文档.md
    └── 06_测试设计文档.md
```

---

## 支持设备

以下设备已验证支持 BLE 心率广播功能：

| 设备型号 | 心率广播 | 备注 |
|----------|:--------:|------|
| 华为手环 8 | ✅ | 推荐，开箱即用 |
| 华为手环 9 | ✅ | 推荐，开箱即用 |
| 华为 Watch GT2 | ✅ | 需在设置中手动开启心率广播 |
| 华为 Watch GT3 | ✅ | 需在设置中手动开启心率广播 |
| 华为 Watch GT4 | ✅ | 需在设置中手动开启心率广播 |
| 华为 Watch Fit | ✅ | 需在设置中手动开启心率广播 |
| 华为 Watch Fit 2 | ✅ | 需在设置中手动开启心率广播 |

> **注意：** 其他支持标准 BLE 心率服务（UUID: `0x180D`）的设备理论上也可兼容使用。

---

## BLE 协议说明

应用基于 Bluetooth SIG 定义的标准心率服务（Heart Rate Profile）与设备通信。

### 服务与特征值

| 服务/特征 | UUID | 说明 |
|-----------|------|------|
| Heart Rate Service | `0x180D` | 心率服务 |
| Heart Rate Measurement | `0x2A37` | 心率测量值（通知） |
| Body Sensor Location | `0x2A38` | 传感器位置（可选读取） |
| Heart Rate Control Point | `0x2A39` | 控制点（可选写入） |

### Heart Rate Measurement 数据格式

`0x2A37` 特征值的数据格式如下：

```
Byte 0: Flags
  ├── Bit 0:   Heart Rate Value Format (0 = UINT8, 1 = UINT16)
  ├── Bit 1-2: Sensor Contact Status
  ├── Bit 3:   Energy Expended Status
  ├── Bit 4:   RR-Interval Status
  └── Bit 5-7: Reserved

Byte 1:      Heart Rate Value (UINT8, 0-255 BPM)
  — 或 —
Byte 1-2:    Heart Rate Value (UINT16, 小端序)

[可选] Energy Expended (2 bytes)
[可选] RR-Interval (每组 2 bytes, 单位 1/1024 秒)
```

### 连接流程

```
1. 启动 BLE 广告扫描 (BluetoothLEAdvertisementWatcher)
         │
2. 发现设备，过滤广播数据中的设备名称
         │
3. 用户选择目标设备
         │
4. 建立 GATT 连接 (BluetoothLEDevice.FromIdAsync)
         │
5. 发现心率服务 (GetGattServicesForUuidAsync: 0x180D)
         │
6. 发现心率测量特征值 (GetCharacteristicsForUuidAsync: 0x2A37)
         │
7. 启用通知 (WriteClientCharacteristicConfigurationDescriptor: Notify)
         │
8. 接收心率数据 → 解析 → 更新界面
         │
9. 连接断开时 → 自动重连（最多 3 次，间隔 2s/4s/6s）
```

---

## 运行测试

### 执行全部测试

```bash
dotnet test tests/HeartRateMonitor.Tests/HeartRateMonitor.Tests.csproj
```

### 执行特定测试类

```bash
dotnet test tests/HeartRateMonitor.Tests/HeartRateMonitor.Tests.csproj --filter "FullyQualifiedName~HeartRateParserTests"
```

### 查看详细输出

```bash
dotnet test tests/HeartRateMonitor.Tests/HeartRateMonitor.Tests.csproj --verbosity normal
```

### 测试覆盖范围

| 测试类 | 测试内容 | 用例数 |
|--------|----------|--------|
| `HeartRateParserTests` | UINT8/UINT16 解析、Sensor Contact、RR-Interval、异常输入 | 12 |
| `HeartRateCalculatorTests` | 统计计算、区间分类、报告生成 | — |
| `HeartRateServiceTests` | 心率更新、历史管理、事件触发 | — |
| `DatabaseTests` | CRUD 操作、数据查询、数据库初始化 | — |

---

## API 参考

### 核心接口

#### IBleService

蓝牙低功耗设备管理服务。

```csharp
public interface IBleService
{
    ConnectionState State { get; }
    BleDevice? ConnectedDevice { get; }
    bool IsScanning { get; }

    event EventHandler<HeartRateChangedEventArgs>? HeartRateReceived;
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    event EventHandler<HeartRateAlertEventArgs>? HeartRateAlert;

    Task<bool> RequestPermissionAsync();
    Task StartScanningAsync();
    Task StopScanningAsync();
    Task ConnectAsync(BleDevice device);
    Task DisconnectAsync();
    Task<bool> AutoReconnectAsync();
    IAsyncEnumerable<BleDevice> DiscoverDevicesAsync(CancellationToken cancellationToken = default);
}
```

#### IHeartRateService

心率数据管理服务。

```csharp
public interface IHeartRateService
{
    int CurrentHeartRate { get; }
    HeartRateData? LatestData { get; }
    IReadOnlyList<HeartRateData> RecentHistory { get; }

    event EventHandler<HeartRateChangedEventArgs>? HeartRateUpdated;

    void UpdateHeartRate(HeartRateData data);
    Task<HeartRateStatistics> GetStatisticsAsync(DateTime startTime, DateTime endTime);
    Task<DailyReport> GetDailyReportAsync(DateTime date);
    void ClearHistory();
}
```

#### IDataService

数据持久化服务。

```csharp
public interface IDataService
{
    Task InitializeAsync();
    Task SaveHeartRateRecordAsync(HeartRateData data, string? deviceId = null);
    Task<List<HeartRateData>> GetHeartRateRecordsAsync(DateTime startTime, DateTime endTime);
    Task<DailyReport> GetDailyReportAsync(DateTime date);
    Task SaveDeviceInfoAsync(BleDevice device);
    Task<BleDevice?> GetLastConnectedDeviceAsync();
    Task<List<BleDevice>> GetPairedDevicesAsync();
    Task CleanupOldDataAsync(TimeSpan retention);
}
```

#### ISettingsService

应用配置管理服务。

```csharp
public interface ISettingsService
{
    event EventHandler? SettingsChanged;

    string? LastDeviceId { get; set; }
    bool AutoConnect { get; set; }
    int HighHeartRateThreshold { get; set; }
    int LowHeartRateThreshold { get; set; }
    bool EnableNotifications { get; set; }
    double OverlayOpacity { get; set; }
    string Theme { get; set; }
    string Language { get; set; }
    bool StartWithWindows { get; set; }
    bool MinimizeToTray { get; set; }
    int DataRetentionDays { get; set; }
    bool MinimalMode { get; set; }

    Task LoadAsync();
    Task SaveAsync();
    void ResetToDefaults();
    void NotifySettingsChanged();
}
```

---

## 贡献指南

欢迎对本项目做出贡献！请遵循以下流程：

### 如何贡献

1. **Fork** 本仓库
2. 创建你的功能分支
   ```bash
   git checkout -b feature/amazing-feature
   ```
3. 提交你的更改
   ```bash
   git commit -m "feat: add amazing feature"
   ```
4. 推送到你的分支
   ```bash
   git push origin feature/amazing-feature
   ```
5. 打开一个 **Pull Request**

### 提交规范

请使用 [Conventional Commits](https://www.conventionalcommits.org/) 规范编写提交信息：

| 前缀 | 说明 |
|------|------|
| `feat:` | 新功能 |
| `fix:` | Bug 修复 |
| `docs:` | 文档更新 |
| `style:` | 代码格式调整（不影响逻辑） |
| `refactor:` | 代码重构 |
| `test:` | 测试相关 |
| `chore:` | 构建/工具链变更 |

**示例：**
```
feat: 添加心率报警声音提示功能
fix: 修复 BLE 断连后 UI 未更新的问题
docs: 更新 README 安装说明
```

### 开发环境

1. 克隆仓库并还原依赖
   ```bash
   git clone https://github.com/<your-username>/HeartRate.git
   cd HeartRate
   dotnet restore src/HeartRateMonitor.App/HeartRateMonitor.App.csproj
   ```
2. 使用 Visual Studio 2022、JetBrains Rider 或 VS Code 打开项目
3. 运行测试确保环境正常
   ```bash
   dotnet test tests/HeartRateMonitor.Tests/HeartRateMonitor.Tests.csproj
   ```

### Pull Request 要求

- 确保所有测试通过
- 新功能需附带对应的单元测试
- 遵循现有代码风格和命名规范
- 更新相关文档（如有必要）
- PR 描述中说明改动内容和原因

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
- [Windows BLE API Documentation](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth) — Windows 蓝牙 API 文档
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM 框架
- [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2) — 高性能 .NET 图表库
- [Dapper](https://github.com/DapperLib/Dapper) — 轻量 ORM 框架
- [Serilog](https://serilog.net/) — 结构化日志框架
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) — WPF 系统托盘库
