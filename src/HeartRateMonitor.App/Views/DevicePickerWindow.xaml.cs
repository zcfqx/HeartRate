using System.Windows;
using System.Windows.Input;
using HeartRateMonitor.App.ViewModels;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.App.Views;

public partial class DevicePickerWindow : Window
{
    private readonly DevicePickerViewModel _viewModel;

    public DevicePickerWindow(DevicePickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _viewModel.DeviceSelected += OnDeviceSelected;
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
        DialogResult = true;
        Close();
    }
}
