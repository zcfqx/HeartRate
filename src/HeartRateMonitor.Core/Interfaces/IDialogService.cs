using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Core.Interfaces;

public interface IDialogService
{
    void ShowMessage(string message, string title);
    BleDevice? ShowDevicePickerDialog();
    void ShowSettingsDialog();
}
