using System.Windows;
using HeartRateMonitor.App.Views;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace HeartRateMonitor.App;

public class DialogService : IDialogService
{
    public void ShowMessage(string message, string title)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
        }
    }

    public BleDevice? ShowDevicePickerDialog()
    {
        BleDevice? result = null;

        if (Application.Current.Dispatcher.CheckAccess())
        {
            result = ShowDevicePickerInternal();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                result = ShowDevicePickerInternal();
            });
        }

        return result;
    }

    public void ShowSettingsDialog()
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            ShowSettingsInternal();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(ShowSettingsInternal);
        }
    }

    private static BleDevice? ShowDevicePickerInternal()
    {
        var picker = App.Services?.GetService<DevicePickerWindow>();
        if (picker == null) return null;

        picker.Owner = Application.Current.MainWindow;
        var dialogResult = picker.ShowDialog();
        return dialogResult == true ? picker.SelectedDevice : null;
    }

    private static void ShowSettingsInternal()
    {
        var settingsWindow = App.Services?.GetService<SettingsWindow>();
        if (settingsWindow == null) return;

        settingsWindow.Owner = Application.Current.MainWindow;
        settingsWindow.ShowDialog();
    }
}
