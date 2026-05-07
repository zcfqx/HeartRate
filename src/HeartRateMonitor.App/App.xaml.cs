using System.IO;
using System.Windows;
using HeartRateMonitor.App.ViewModels;
using HeartRateMonitor.App.Views;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Data.Database;
using HeartRateMonitor.Data.Repositories;
using HeartRateMonitor.Services;
using HeartRateMonitor.Services.BLE;
using HeartRateMonitor.Services.DataService;
using HeartRateMonitor.Services.HeartRate;
using HeartRateMonitor.Services.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace HeartRateMonitor.App;

public partial class App : Application
{
    private static ServiceProvider? _serviceProvider;
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HeartRateMonitor", "startup.log");

    public static IServiceProvider? Services => _serviceProvider;

    private static void DebugLog(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DebugLog("OnStartup entered");

        try
        {
            DebugLog("Configuring services...");
            var services = new ServiceCollection();
            ConfigureServices(services);
            DebugLog("Building provider...");
            _serviceProvider = services.BuildServiceProvider();
            DebugLog("Provider built OK");

            DebugLog("Creating logger...");
            var logger = _serviceProvider.GetRequiredService<ILogger>();
            logger.Info("=== 应用启动中 ===");

            DebugLog("Initializing DB...");
            var dataService = _serviceProvider.GetRequiredService<IDataService>();
            dataService.InitializeAsync().GetAwaiter().GetResult();
            logger.Info("数据库初始化完成");

            DebugLog("Loading settings...");
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            settingsService.LoadAsync().GetAwaiter().GetResult();
            logger.Info("设置加载完成");

            DebugLog("Creating MainWindow...");
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            DebugLog("Showing MainWindow...");
            mainWindow.Show();
            DebugLog("MainWindow shown");

            try
            {
                var bleService = _serviceProvider.GetRequiredService<IBleService>();
                DebugLog("Requesting BLE permission...");
                bleService.RequestPermissionAsync().GetAwaiter().GetResult();
                logger.Info("蓝牙权限已授予");

                if (settingsService.AutoConnect && !string.IsNullOrEmpty(settingsService.LastDeviceId))
                {
                    _ = Task.Run(async () =>
                    {
                        try { await bleService.AutoReconnectAsync(); }
                        catch (Exception rex) { logger.Warning($"自动重连失败: {rex.Message}"); }
                    });
                }
            }
            catch (Exception bleEx)
            {
                logger?.Warning($"蓝牙初始化已跳过: {bleEx.Message}");
                DebugLog($"BLE init skipped: {bleEx.Message}");
            }

            DebugLog("=== Startup Complete ===");
            logger?.Info("=== 应用已启动 ===");
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

    private static void ConfigureServices(ServiceCollection services)
    {
        var dbInitializer = new DatabaseInitializer();
        services.AddSingleton(dbInitializer);
        services.AddSingleton<HeartRateRepository>();
        services.AddSingleton<DeviceRepository>();
        services.AddSingleton<SettingsRepository>();

        services.AddSingleton<ILogger, SerilogLogger>();
        services.AddSingleton<IHeartRateParser, HeartRateParser>();
        services.AddSingleton<IHeartRateCalculator, HeartRateCalculator>();
        services.AddSingleton<IBleService, BleService>();
        services.AddSingleton<IHeartRateService, HeartRateService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDataService, DataService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DevicePickerViewModel>();

        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<DevicePickerWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DebugLog("OnExit called");
        (_serviceProvider?.GetService<IBleService>() as IDisposable)?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
