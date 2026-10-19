using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HeartRateMonitor.App.Extensibility;
using HeartRateMonitor.App.Infrastructure;
using HeartRateMonitor.App.ViewModels;
using HeartRateMonitor.App.Views;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Data.Database;
using HeartRateMonitor.Extensibility;
using HeartRateMonitor.Data.Repositories;
using HeartRateMonitor.Services.BLE;
using HeartRateMonitor.Services.DataService;
using HeartRateMonitor.Services.HeartRate;
using HeartRateMonitor.Services.Logging;
using HeartRateMonitor.Services.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace HeartRateMonitor.App;

public partial class App : Application
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HeartRateMonitor", "startup.log");

    private static ServiceProvider? _serviceProvider;
    private static MainWindow? _mainWindow;
    private static TrayIcon? _trayIcon;
    private const string MutexName = @"Local\HeartRateMonitor.SingleInstance";
    private const string ActivateEventName = @"Local\HeartRateMonitor.Activate";
    private const string ActivateAckEventName = @"Local\HeartRateMonitor.ActivateAck";
    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _activateEvent;
    private EventWaitHandle? _activateAckEvent;
    private Thread? _activationListener;
    private volatile bool _stopActivationListener;
    private volatile bool _pendingActivation;

    public static IServiceProvider? Services => _serviceProvider;

    /// <summary>应用是否正在真正退出（用于区分“关闭窗口→隐藏到托盘”）。</summary>
    public static bool IsExiting { get; private set; }

    private static void DebugLog(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 带进程号：多实例交叠时可以区分是哪一次启动写的日志。
            File.AppendAllText(
                LogFile,
                $"[{DateTime.Now:HH:mm:ss.fff}][pid {Environment.ProcessId}] {msg}\n");
        }
        catch
        {
            // 启动日志不可写时忽略：日志失败不能让应用启动失败。
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DebugLog("OnStartup entered");

        // 单实例：已有实例在运行就通知它显示窗口，本进程直接退出；
        // 如果已有实例卡死无响应，则结束它并继续启动，避免“一直提示已在运行但不显示界面”。
        _mutex = new Mutex(true, MutexName, out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            DebugLog("Another instance detected, trying to activate it");
            if (TryActivateExistingInstance())
            {
                DebugLog("Existing instance activated, shutting down");
                Shutdown();
                return;
            }

            DebugLog("Existing instance did not respond, trying to take over");
            if (TryTakeOverStaleInstance())
            {
                DebugLog("Stale instance terminated, continuing startup");
            }
            else
            {
                DebugLog("Another instance is running, shutting down");
                MessageBox.Show("心率监控已经在运行。", "心率监控", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
        }

        StartActivationListener();
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            DebugLog("Configuring services...");
            var services = new ServiceCollection();
            ConfigureServices(services);
            DebugLog("Building provider...");
            _serviceProvider = services.BuildServiceProvider();
            DebugLog("Provider built OK");

            var logger = _serviceProvider.GetRequiredService<ILogger>();
            logger.Info("=== 应用启动中 ===");

            DebugLog("Initializing DB...");
            var dataService = _serviceProvider.GetRequiredService<IDataService>();
            // 放在线程池再同步等待：绝不在 UI 线程上同步等待内部含 WinRT await 的方法，
            // 否则续体会排到已被阻塞的 Dispatcher 上，界面永久卡死。
            Task.Run(() => dataService.InitializeAsync()).GetAwaiter().GetResult();
            logger.Info("数据库初始化完成");

            DebugLog("Loading settings...");
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            Task.Run(() => settingsService.LoadAsync()).GetAwaiter().GetResult();
            logger.Info("设置加载完成");

            DebugLog("Applying theme...");
            ThemeManager.Apply(settingsService.Theme, settingsService.AccentColor);

            // 提前解析会话服务：保证在蓝牙连接前已完成订阅
            _serviceProvider.GetRequiredService<IHeartRateSession>();

            DebugLog("Initializing plugins...");
            var pluginHost = _serviceProvider.GetRequiredService<IPluginHost>();
            Task.Run(() => pluginHost.InitializeAsync()).GetAwaiter().GetResult();
            logger.Info($"插件初始化完成：{pluginHost.Plugins.Count} 个插件");

            DebugLog("Creating tray icon...");
            CreateTrayIcon(logger, settingsService);

            DebugLog("Creating MainWindow...");
            _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            DebugLog("Showing MainWindow...");
            _mainWindow.Show();
            DebugLog("MainWindow shown");

            if (_pendingActivation)
            {
                _pendingActivation = false;
                ShowMainWindow();
            }

            try
            {
                var bleService = _serviceProvider.GetRequiredService<IBleService>();
                DebugLog("Requesting BLE permission (async, non-blocking)...");
                _ = InitializeBluetoothAsync(bleService, settingsService, logger);
            }
            catch (Exception bleEx)
            {
                logger.Warning($"蓝牙初始化已跳过: {bleEx.Message}");
                DebugLog($"BLE init skipped: {bleEx.Message}");
            }

            DebugLog("=== Startup Complete ===");
            logger.Info("=== 应用已启动 ===");
        }
        catch (Exception ex)
        {
            DebugLog($"STARTUP FAILED: {ex}");
            MessageBox.Show(
                $"应用启动失败:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "心率监控",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>监听“再次启动”信号：第二个进程会 Set 激活事件，这里负责把主窗口叫出来。</summary>
    private void StartActivationListener()
    {
        try
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            _activateAckEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateAckEventName);
        }
        catch (Exception ex)
        {
            DebugLog($"创建单实例激活事件失败: {ex.Message}");
            return;
        }

        _activationListener = new Thread(() =>
        {
            while (!_stopActivationListener)
            {
                try
                {
                    if (!_activateEvent.WaitOne(500))
                    {
                        continue;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                // 先回执，让第二个进程知道本进程还活着（随后它自己退出）。
                try
                {
                    _activateAckEvent.Set();
                }
                catch
                {
                    // 回执失败不影响显示窗口
                }

                Dispatcher.BeginInvoke(() =>
                {
                    if (_mainWindow is null)
                    {
                        _pendingActivation = true;
                        return;
                    }

                    ShowMainWindow();
                });
            }
        })
        {
            IsBackground = true,
            Name = "HeartRateMonitor.ActivationListener"
        };
        _activationListener.Start();
    }

    /// <summary>通知已有实例显示窗口；收到回执返回 true，超时说明对方可能卡死。</summary>
    private static bool TryActivateExistingInstance()
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(ActivateEventName, out var activate))
            {
                return false;
            }

            using (activate)
            {
                if (!EventWaitHandle.TryOpenExisting(ActivateAckEventName, out var ack))
                {
                    // 旧版本没有回执事件，只能认为通知成功。
                    activate.Set();
                    return true;
                }

                using (ack)
                {
                    activate.Set();
                    return ack.WaitOne(TimeSpan.FromSeconds(3));
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog($"激活已有实例失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>已有实例持有互斥量却不响应时结束它，然后重新取得互斥量继续启动。</summary>
    private bool TryTakeOverStaleInstance()
    {
        try
        {
            var current = Process.GetCurrentProcess();

            // 先只结束同一个可执行文件启动的旧实例；若仍拿不到互斥量，
            // 再退一步结束同名进程（旧版本/旧发布目录留下的僵尸进程）。
            KillInstances(current, requireSamePath: true);
            if (TryAcquireMutexForTakeover())
            {
                return true;
            }

            KillInstances(current, requireSamePath: false);
            if (TryAcquireMutexForTakeover())
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            DebugLog($"接管旧实例失败: {ex.Message}");
        }

        return false;
    }

    /// <summary>结束无响应的心率监控进程；requireSamePath=true 时只结束同一路径的进程。</summary>
    private static void KillInstances(Process current, bool requireSamePath)
    {
        string? currentPath = null;
        try
        {
            currentPath = current.MainModule?.FileName;
        }
        catch
        {
            // 读不到自身路径时退回按进程名匹配
        }

        foreach (var process in Process.GetProcessesByName(current.ProcessName))
        {
            using (process)
            {
                if (process.Id == current.Id)
                {
                    continue;
                }

                if (requireSamePath)
                {
                    string? path = null;
                    try
                    {
                        path = process.MainModule?.FileName;
                    }
                    catch
                    {
                        // 无权读取路径时不结束该进程
                    }

                    if (path is null || currentPath is null ||
                        !string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                DebugLog($"结束无响应的旧实例: pid={process.Id} name={process.ProcessName}");
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch (Exception ex)
                {
                    DebugLog($"结束旧实例失败: {ex.Message}");
                }
            }
        }
    }

    /// <summary>旧进程退出后重建互斥量并尝试取得所有权。</summary>
    private bool TryAcquireMutexForTakeover()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                _mutex?.Dispose();
            }
            catch
            {
                // 忽略
            }

            _mutex = new Mutex(true, MutexName, out var createdNew);
            _ownsMutex = createdNew;
            if (createdNew)
            {
                return true;
            }

            Thread.Sleep(200);
        }

        return false;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DebugLog($"UNHANDLED: {e.Exception}");
        try
        {
            _serviceProvider?.GetService<ILogger>()?.Error("未处理的界面异常", e.Exception);
        }
        catch
        {
            // 日志不可用
        }

        MessageBox.Show($"出现未处理的错误：\n{e.Exception.Message}", "心率监控",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void CreateTrayIcon(ILogger logger, ISettingsService settings)
    {
        try
        {
            _trayIcon = new TrayIcon("心率监控");
            _trayIcon.Activated += (_, _) => ShowMainWindow();
            _trayIcon.DoubleClicked += (_, _) => ShowMainWindow();
            _trayIcon.MenuFactory = BuildTrayMenu;

            if (!settings.MinimizeToTray)
            {
                // 用户关闭了“最小化到托盘”，托盘图标仍保留但提示退出方式
                logger.Debug("托盘图标已创建（最小化到托盘已关闭）");
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"创建托盘图标失败: {ex.Message}");
        }
    }

    private static ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();

        var showItem = new MenuItem { Header = "显示主窗口" };
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);

        var settingsItem = new MenuItem { Header = "设置" };
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        var minimalItem = new MenuItem { Header = "极简模式", IsCheckable = true };
        var mainViewModel = _serviceProvider?.GetService<MainViewModel>();
        if (mainViewModel is not null)
        {
            minimalItem.IsChecked = mainViewModel.IsMinimalMode;
        }

        minimalItem.Click += (_, _) =>
        {
            var viewModel = _serviceProvider?.GetService<MainViewModel>();
            if (viewModel is null)
            {
                return;
            }

            viewModel.ToggleMinimalModeCommand.Execute(null);
            minimalItem.IsChecked = viewModel.IsMinimalMode;
        };
        menu.Items.Add(minimalItem);

        // 极简模式的曲线开关与背景不透明度，不必打开设置窗口就能随手调。
        var chartItem = new MenuItem { Header = "显示趋势曲线", IsCheckable = true };
        if (mainViewModel is not null)
        {
            chartItem.IsChecked = mainViewModel.MinimalShowChart;
        }

        chartItem.Click += (_, _) =>
        {
            var viewModel = _serviceProvider?.GetService<MainViewModel>();
            if (viewModel is null)
            {
                return;
            }

            viewModel.ToggleMinimalChartCommand.Execute(null);
            chartItem.IsChecked = viewModel.MinimalShowChart;
        };
        menu.Items.Add(chartItem);

        // 极简模式的整体尺寸预设：心形、数字与曲线一起缩放。
        var minimalSizeItem = new MenuItem { Header = "极简模式尺寸" };
        foreach (var preset in new (string Label, double Value)[]
                 {
                     ("紧凑 (0.8)", 0.8), ("标准 (1.0)", 1.0), ("大 (1.25)", 1.25), ("特大 (1.5)", 1.5)
                 })
        {
            var presetItem = new MenuItem { Header = preset.Label };
            var scale = preset.Value;
            presetItem.Click += (_, _) =>
            {
                var viewModel = _serviceProvider?.GetService<MainViewModel>();
                viewModel?.SetMinimalScaleCommand.Execute(scale);
            };
            minimalSizeItem.Items.Add(presetItem);
        }

        menu.Items.Add(minimalSizeItem);

        // 窗口置顶：让悬浮窗始终显示在其他窗口之上，随时能看到心率。
        var topmostItem = new MenuItem { Header = "窗口置顶", IsCheckable = true };
        if (mainViewModel is not null)
        {
            topmostItem.IsChecked = mainViewModel.WindowTopmost;
        }

        topmostItem.Click += (_, _) =>
        {
            var viewModel = _serviceProvider?.GetService<MainViewModel>();
            if (viewModel is null)
            {
                return;
            }

            viewModel.ToggleTopmostCommand.Execute(null);
            topmostItem.IsChecked = viewModel.WindowTopmost;
        };
        menu.Items.Add(topmostItem);

        var backgroundItem = new MenuItem { Header = "背景不透明度" };
        foreach (var preset in new (string Label, double Value)[]
                 {
                     ("完全透明", 0.0), ("90% 不透明", 0.9), ("80% 不透明", 0.8), ("100% 不透明", 1.0)
                 })
        {
            var presetItem = new MenuItem { Header = preset.Label };
            var opacity = preset.Value;
            presetItem.Click += (_, _) =>
            {
                var viewModel = _serviceProvider?.GetService<MainViewModel>();
                viewModel?.SetOverlayOpacityCommand.Execute(opacity);
            };
            backgroundItem.Items.Add(presetItem);
        }

        menu.Items.Add(backgroundItem);

        if (_serviceProvider?.GetService<IPluginHost>() is { Commands.Count: > 0 } pluginHost)
        {
            menu.Items.Add(new Separator());
            var logger = _serviceProvider.GetService<ILogger>();
            foreach (var command in pluginHost.Commands)
            {
                var item = new MenuItem { Header = command.Title };
                item.Click += async (_, _) =>
                {
                    try
                    {
                        await command.Execute();
                    }
                    catch (Exception ex)
                    {
                        logger?.Error($"执行插件命令失败: {command.Id}", ex);
                    }
                };
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new Separator());
        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);

        return menu;
    }

    /// <summary>显示并激活主窗口（托盘与插件都可能调用）。</summary>
    public static void ShowMainWindow()
    {
        var window = _mainWindow;
        if (window is null)
        {
            return;
        }

        try
        {
            if (!window.IsVisible)
            {
                // 窗口若已被关闭，Show() 会抛 InvalidOperationException：
                // 这里只记录日志，不再弹出错误提示（否则激活路径会变成二次弹窗）。
                window.Show();
            }
        }
        catch (InvalidOperationException ex)
        {
            DebugLog($"主窗口已关闭，无法再次显示: {ex.Message}");
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        // 先用“临时置顶”把窗口带到最前，再恢复用户设置的置顶状态。
        // （原实现无条件置 false，会让设置了置顶的窗口在托盘/二次启动激活后失去置顶。）
        var wantTopmost = window.Topmost;
        window.Activate();
        window.Topmost = true;
        window.Topmost = wantTopmost;
        window.Focus();
        DebugLog($"ShowMainWindow: visible={window.IsVisible} state={window.WindowState}");
    }

    private static void OpenSettings()
    {
        try
        {
            _serviceProvider?.GetRequiredService<IDialogService>().ShowSettingsDialog();
        }
        catch (Exception ex)
        {
            _serviceProvider?.GetService<ILogger>()?.Error("打开设置失败", ex);
        }
    }

    /// <summary>真正退出应用：先保存设置，再关闭所有窗口并释放资源。</summary>
    public static void ExitApp()
    {
        IsExiting = true;

        try
        {
            var settings = _serviceProvider?.GetService<ISettingsService>();
            if (settings is not null)
            {
                Task.Run(() => settings.SaveAsync()).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // 保存失败不阻止退出
        }

        Current?.Shutdown();
    }

    /// <summary>
    /// 后台完成蓝牙权限检查与自动重连：不在 UI 线程上同步等待任何异步调用，
    /// 否则 WinRT 的 await 续体会排队到已被阻塞的 Dispatcher 上，导致整个界面卡死。
    /// </summary>
    private static async Task InitializeBluetoothAsync(
        IBleService bleService,
        ISettingsService settingsService,
        ILogger logger)
    {
        try
        {
            var granted = await bleService.RequestPermissionAsync().ConfigureAwait(false);
            logger.Info(granted ? "蓝牙权限已授予" : "蓝牙当前不可用，等待用户开启");

            var savedDeviceId = settingsService.LastDeviceId;
            if (!settingsService.AutoConnect)
            {
                logger.Info("启动时自动连接已关闭，可在 设置 → 连接 中开启");
                return;
            }

            if (!granted)
            {
                return;
            }

            if (string.IsNullOrEmpty(savedDeviceId))
            {
                logger.Info("还没有连接过设备，等待在设备组件中扫描并连接");
                return;
            }

            // AutoReconnectAsync 只在已有连接时才工作，启动阶段必须走“扫描 → 发现即连接”。
            // 手环的心率广播有间隔，单次扫描常常刚好错过，因此给两次机会。
            const int attempts = 2;
            var timeout = TimeSpan.FromSeconds(20);
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    var connected = await bleService
                        .ConnectSavedDeviceAsync(savedDeviceId, timeout)
                        .ConfigureAwait(false);

                    if (connected)
                    {
                        logger.Info("启动时已自动连接上次使用的设备");

                        // 设备名称可能刚从占位名升级，顺手把最新名称写回设置，
                        // 这样托盘/底栏显示的是手环真实名称。
                        var name = bleService.ConnectedDevice?.DeviceName;
                        if (!string.IsNullOrWhiteSpace(name) &&
                            !string.Equals(name, settingsService.LastDeviceName, StringComparison.Ordinal))
                        {
                            settingsService.LastDeviceName = name;
                            await settingsService.SaveAsync().ConfigureAwait(false);
                        }

                        return;
                    }

                    if (attempt < attempts)
                    {
                        logger.Info("启动自动连接：本次扫描未发现手环，立即重试一次");
                    }
                    else
                    {
                        logger.Warning("启动自动连接未成功，可在设备组件中点击“扫描”，或在设置 → 连接中重新连接");
                    }
                }
                catch (Exception rex)
                {
                    logger.Warning($"启动自动连接第 {attempt}/{attempts} 次尝试失败: {rex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"蓝牙初始化已跳过: {ex.Message}");
        }
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<HeartRateRepository>();
        services.AddSingleton<DeviceRepository>();
        services.AddSingleton<SettingsRepository>();

        services.AddSingleton<ILogger, FileLogger>();
        services.AddSingleton<IHeartRateParser, HeartRateParser>();
        services.AddSingleton<IHeartRateCalculator, HeartRateCalculator>();
        services.AddSingleton<IBleService, BleService>();
        services.AddSingleton<IHeartRateService, HeartRateService>();
        services.AddSingleton<IHeartRateSession, HeartRateSession>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDataService, DataService>();
        services.AddSingleton<IDialogService, DialogService>();

        // 插件基础设施
        services.AddSingleton<PluginContextFactory>();
        services.AddSingleton<IAppPlugin, CoreWidgetsPlugin>();
        services.AddSingleton<IAppPlugin, DevicePlugin>();
        services.AddSingleton<IAppPlugin, SessionPlugin>();
        services.AddSingleton<IAppPlugin, ExtrasPlugin>();
        services.AddSingleton<IPluginHost, PluginHost>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DebugLog("OnExit called");
        IsExiting = true;

        _stopActivationListener = true;
        try
        {
            _activateEvent?.Set();
        }
        catch
        {
            // 忽略
        }

        try
        {
            _activationListener?.Join(1000);
        }
        catch
        {
            // 忽略
        }

        _activateEvent?.Dispose();
        _activateAckEvent?.Dispose();

        try
        {
            _trayIcon?.Dispose();
        }
        catch
        {
            // 托盘资源释放失败可忽略
        }

        try
        {
            _serviceProvider?.GetService<MainViewModel>()?.Dispose();
        }
        catch
        {
            // 忽略
        }

        try
        {
            (_serviceProvider?.GetService<IBleService>() as IDisposable)?.Dispose();
        }
        catch
        {
            // 忽略
        }

        _serviceProvider?.Dispose();

        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
                // 未持有互斥量
            }
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }
}
