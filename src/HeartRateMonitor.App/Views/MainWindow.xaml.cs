using System.Windows;
using System.Windows.Input;
using HeartRateMonitor.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HeartRateMonitor.App.Views;

public partial class MainWindow : Window
{
    private bool _forceClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowPosition();
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
            SaveWindowPosition();
        }
    }

    private void OnScanClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ToggleScanCommand.Execute(null);
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsWindow = App.Services?.GetService<SettingsWindow>();
        if (settingsWindow != null)
        {
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _forceClose = true;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            return;
        }

        SaveWindowPosition();
        Application.Current.Shutdown();
    }

    private void SaveWindowPosition()
    {
        var settings = App.Services?.GetService<Core.Interfaces.ISettingsService>();
        if (settings != null)
        {
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
            _ = settings.SaveAsync();
        }
    }

    private void RestoreWindowPosition()
    {
        var settings = App.Services?.GetService<Core.Interfaces.ISettingsService>();
        if (settings is { WindowLeft: > 0, WindowTop: > 0 })
        {
            Left = settings.WindowLeft;
            Top = settings.WindowTop;
        }
    }
}
