using System.Windows;
using System.Windows.Input;
using HeartRateMonitor.App.ViewModels;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.App.Views;

public partial class DevicePickerWindow : Window
{
    private readonly DevicePickerViewModel _viewModel;

    public BleDevice? SelectedDevice { get; private set; }

    public DevicePickerWindow(DevicePickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _viewModel.DeviceSelected += OnDeviceSelected;
        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.AttachEvents();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.DetachEvents();
    }

    private void OnDeviceClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BleDevice device })
        {
            _viewModel.SelectDeviceCommand.Execute(device);
        }
    }

    private void OnDeviceSelected(object? sender, BleDevice device)
    {
        SelectedDevice = device;
        DialogResult = true;
        Close();
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
