using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using HeartRateMonitor.App.ViewModels;
using HeartRateMonitor.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace HeartRateMonitor.App.Views;

public partial class MainWindow : Window
{
    private bool _forceClose;
    private Storyboard? _pulseStoryboard;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowPosition();
        _pulseStoryboard = (Storyboard)FindResource("HeartPulseStoryboard");
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsConnected):
                if (vm.IsConnected) _pulseStoryboard?.Begin(this, true);
                else _pulseStoryboard?.Stop(this);
                break;
            case nameof(MainViewModel.CurrentHeartRate):
                UpdatePulseSpeed(vm.CurrentHeartRate);
                break;
        }
    }

    private void UpdatePulseSpeed(int heartRate)
    {
        if (_pulseStoryboard == null || heartRate <= 0) return;
        var half = TimeSpan.FromTicks(TimeSpan.FromSeconds(Math.Max(60.0 / heartRate, 0.25)).Ticks / 2);
        foreach (var t in _pulseStoryboard.Children)
            if (t is DoubleAnimation a) a.Duration = half;
    }

    // === 标题栏 ===
    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
        SaveWindowPosition();
    }

    private void OnMinimalModeToggle(object sender, RoutedEventArgs e)
        => (DataContext as MainViewModel)?.ToggleMinimalModeCommand.Execute(null);

    private void OnBpmAreaClick(object sender, MouseButtonEventArgs e)
        => (DataContext as MainViewModel)?.ToggleMinimalModeCommand.Execute(null);

    private void OnMinimalAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _forceClose = true;
        Close();
    }

    // === 标签栏 ===
    private void OnHeartRateTabClick(object sender, RoutedEventArgs e)
        => (DataContext as MainViewModel)?.ShowHeartRatePanelCommand.Execute(null);

    private void OnDeviceTabClick(object sender, RoutedEventArgs e)
        => (DataContext as MainViewModel)?.ToggleDevicePanelCommand.Execute(null);

    private void OnSettingsTabClick(object sender, RoutedEventArgs e)
        => (DataContext as MainViewModel)?.ToggleSettingsPanelCommand.Execute(null);

    // === 设备 ===
    private void OnScanClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            _ = vm.ToggleScanCommand.ExecuteAsync(null);
    }

    private void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            _ = vm.DisconnectCommand.ExecuteAsync(null);
    }

    private void OnDeviceItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is BleDevice device && DataContext is MainViewModel vm)
            _ = vm.ConnectAsync(device);
    }

    // === 设置 ===
    private void OnSaveSettings(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            _ = vm.SaveSettingsCommand.ExecuteAsync(null);
    }

    private void OnResetSettings(object sender, RoutedEventArgs e)
        => (DataContext as MainViewModel)?.ResetSettingsCommand.Execute(null);

    // === 窗口生命周期 ===
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_forceClose) { e.Cancel = true; WindowState = WindowState.Minimized; return; }
        SaveWindowPosition();
        (DataContext as MainViewModel)?.Cleanup();
        Application.Current.Shutdown();
    }

    private void SaveWindowPosition()
    {
        var s = App.Services?.GetService<Core.Interfaces.ISettingsService>();
        if (s != null) { s.WindowLeft = Left; s.WindowTop = Top; _ = s.SaveAsync(); }
    }

    private void RestoreWindowPosition()
    {
        var s = App.Services?.GetService<Core.Interfaces.ISettingsService>();
        if (s is { WindowLeft: > 0, WindowTop: > 0 }) { Left = s.WindowLeft; Top = s.WindowTop; }
    }
}
