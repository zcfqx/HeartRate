using System.Windows;
using System.Windows.Input;
using HeartRateMonitor.App.ViewModels;

namespace HeartRateMonitor.App.Views;

public partial class SettingsWindow : Window
{
    private bool _discardOnClose;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();

        Icon = AppIcon.Image;
        DataContext = viewModel;
        ViewModel = viewModel;

        Closing += OnClosing;
        Closed += OnClosed;
    }

    public SettingsViewModel ViewModel { get; }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 鼠标已释放，忽略
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveAsync();
        Close();
    }

    private async void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _discardOnClose = true;
        await ViewModel.DiscardChangesAsync();
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_discardOnClose)
        {
            // 关闭按钮/保存后的关闭同样持久化（保存命令本身是幂等的）
            _ = ViewModel.SaveAsync();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ViewModel.Dispose();
    }
}
