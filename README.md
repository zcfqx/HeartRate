# HeartRate Monitor - 华为手环心率监控悬浮窗

一款 Windows 桌面悬浮窗应用，通过蓝牙低功耗（BLE）协议实时接收并展示华为手环广播的心率数据。界面采用透明背景设计，仅显示心率数值、趋势图表等视觉元素，适用于运动监控、日常健康管理等场景。

## 功能特性

### 核心功能
- **BLE 设备扫描与连接** — 自动扫描周围蓝牙设备，支持手动选择华为手环连接
- **实时心率接收** — 基于标准蓝牙心率服务（GATT UUID: 0x180D）解析心率测量值
- **心率趋势图表** — 使用 LiveCharts2 实时绘制心率折线图，自动缩放 Y 轴
- **心率区间识别** — 自动分类静息/燃脂/有氧/极限/最大心率区间，并以不同颜色标识
- **统计信息** — 实时计算并显示平均心率、最高心率、最低心率
- **自动重连** — 设备断开后自动尝试重新连接（最多 3 次，递增间隔）

### 界面特性
- **透明悬浮窗** — 背景完全透明，置顶显示，不遮挡其他应用
- **心率颜色映射** — 心率数值根据区间动态变换颜色
- **连接状态指示** — 实时显示扫描/连接/断开/重连等状态
- **RR 间期显示** — 展示心率变异性（HRV）相关数据

### 数据管理
- **本地数据存储** — 使用 SQLite 持久化心率记录和设备信息
- **设备管理** — 自动保存已连接设备信息，支持快速回连

## 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| UI 框架 | WPF (.NET 8) | 透明窗口、硬件加速渲染 |
| MVVM 框架 | CommunityToolkit.Mvvm 8.3.2 | 源生成器、ObservableProperty、RelayCommand |
| BLE 通信 | Windows BLE API | `Windows.Devices.Bluetooth` 原生支持 |
| 图表库 | LiveCharts2 (SkiaSharp) | 高性能实时折线图 |
| 数据库 | SQLite + Dapper | 轻量级本地存储 |
| 日志 | Serilog | 结构化日志，按天滚动写入文件 |
| 系统托盘 | Hardcodet.NotifyIcon.Wpf | 最小化到系统托盘 |
| 测试 | MSTest | 单元测试框架 |

## 项目结构

```
HeartRate/
├── src/
│   ├── HeartRateMonitor.App/            # WPF 主应用程序
│   │   ├── ViewModels/                  # 视图模型（MainViewModel, SettingsViewModel, DevicePickerViewModel）
│   │   ├── Views/                       # XAML 视图（MainWindow, SettingsWindow, DevicePickerWindow）
│   │   ├── Converters/                  # 值转换器
│   │   ├── Styles/                      # 主题样式
│   │   └── App.xaml / App.xaml.cs       # 应用入口与依赖注入配置
│   │
│   ├── HeartRateMonitor.Core/           # 核心业务逻辑（零外部依赖）
│   │   ├── Models/                      # 数据模型（HeartRateData, BleDevice, HeartRateZone, DailyReport）
│   │   ├── Interfaces/                  # 服务接口（IBleService, IHeartRateService, IDataService 等）
│   │   ├── Enums/                       # 枚举（ConnectionState, HeartRateZoneType, ThemeType 等）
│   │   └── Events/                      # 事件参数定义
│   │
│   ├── HeartRateMonitor.Services/       # 服务层实现
│   │   ├── BLE/                         # 蓝牙服务（BleService - 设备扫描、GATT 连接、数据接收）
│   │   ├── HeartRate/                   # 心率服务（HeartRateService, HeartRateParser, HeartRateCalculator）
│   │   ├── DataService/                 # 数据持久化服务
│   │   └── Settings/                    # 配置管理服务
│   │
│   └── HeartRateMonitor.Data/           # 数据访问层
│       ├── Database/                    # 数据库初始化
│       ├── Entities/                    # 数据库实体
│       └── Repositories/               # 仓储模式实现（DeviceRepository, HeartRateRepository, SettingsRepository）
│
├── tests/
│   └── HeartRateMonitor.Tests/          # 单元测试
│       ├── HeartRateParserTests.cs      # 心率数据解析测试
│       ├── HeartRateCalculatorTests.cs  # 心率计算逻辑测试
│       ├── HeartRateServiceTests.cs     # 心率服务测试
│       └── DatabaseTests.cs            # 数据库操作测试
│
└── docs/                                # 项目文档
    ├── 心率监控软件需求文档.md
    ├── 01_架构设计文档.md
    ├── 02_界面设计文档.md
    ├── 03_接口设计文档.md
    ├── 04_数据库设计文档.md
    ├── 05_BLE通信设计文档.md
    └── 06_测试设计文档.md
```

## 环境要求

- **操作系统**: Windows 10 (build 19041+) / Windows 11
- **运行时**: .NET 8.0 Desktop Runtime
- **蓝牙**: Bluetooth 4.0+ (BLE) 适配器
- **屏幕分辨率**: 1920x1080 及以上

## 快速开始

### 安装 .NET 8 SDK

前往 [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) 下载并安装 .NET 8 SDK。

### 克隆项目

```bash
git clone <repository-url>
cd HeartRate
```

### 构建项目

```bash
dotnet build src/HeartRateMonitor.App/HeartRateMonitor.App.csproj
```

### 运行应用

```bash
dotnet run --project src/HeartRateMonitor.App/HeartRateMonitor.App.csproj
```

### 运行测试

```bash
dotnet test tests/HeartRateMonitor.Tests/HeartRateMonitor.Tests.csproj
```

## 使用说明

1. **开启手环心率广播** — 在华为手环上进入运动模式或开启"心率广播"功能
2. **启动应用** — 运行 HeartRate Monitor
3. **扫描设备** — 点击扫描按钮，应用将自动搜索附近的 BLE 心率设备
4. **选择设备** — 从设备列表中选择你的华为手环并连接
5. **查看心率** — 连接成功后，悬浮窗将实时显示心率数值和趋势图表
6. **拖动窗口** — 可自由拖动悬浮窗到屏幕任意位置

## 架构设计

项目采用 **MVVM + 分层架构**，遵循依赖倒置原则：

```
┌──────────────────────────────────────────┐
│         Presentation Layer (WPF)         │
│   Views ← ViewModels ← Converters       │
├──────────────────────────────────────────┤
│         Service Layer                    │
│   BleService, HeartRateService,          │
│   DataService, SettingsService           │
├──────────────────────────────────────────┤
│         Domain Layer (Core)              │
│   Models, Interfaces, Enums, Events      │
├──────────────────────────────────────────┤
│         Infrastructure Layer             │
│   BLE (Windows API), SQLite, Serilog     │
└──────────────────────────────────────────┘
```

- **HeartRateMonitor.Core** — 纯业务逻辑层，定义接口和模型，无外部依赖
- **HeartRateMonitor.Services** — 接口的具体实现，依赖 Core 和 Data 层
- **HeartRateMonitor.Data** — 数据访问层，使用 Dapper + SQLite
- **HeartRateMonitor.App** — WPF 应用层，负责 UI 渲染和依赖注入

## 支持设备

| 设备型号 | 心率广播 | 备注 |
|----------|----------|------|
| 华为手环 8 | 完全支持 | 推荐 |
| 华为手环 9 | 完全支持 | 推荐 |
| 华为 Watch GT2 | 需开启心率广播 | |
| 华为 Watch GT3 | 需开启心率广播 | |
| 华为 Watch GT4 | 需开启心率广播 | |
| 华为 Watch Fit | 需开启心率广播 | |
| 华为 Watch Fit 2 | 需开启心率广播 | |

## 蓝牙协议

应用基于标准 BLE 心率服务协议与设备通信：

| 服务/特征 | UUID | 说明 |
|-----------|------|------|
| Heart Rate Service | 0x180D | 心率服务 |
| Heart Rate Measurement | 0x2A37 | 心率测量值（支持 UINT8/UINT16 格式） |
| Body Sensor Location | 0x2A38 | 传感器位置 |
| Heart Rate Control Point | 0x2A39 | 控制点（可选） |

心率数据解析支持：
- **UINT8 格式** — Flags 字节 Bit 0 = 0，心率值为单字节（0-255 BPM）
- **UINT16 格式** — Flags 字节 Bit 0 = 1，心率值为双字节小端序
- **Sensor Contact** — 检测传感器是否接触皮肤
- **RR-Interval** — 解析心跳间期，用于计算心率变异性（HRV）

## 性能指标

| 指标 | 目标值 |
|------|--------|
| CPU 占用 | < 5% |
| 内存占用 | < 100 MB |
| 数据延迟 | < 2 秒 |
| 启动时间 | < 3 秒 |

## 许可证

本项目仅供学习和个人使用。
