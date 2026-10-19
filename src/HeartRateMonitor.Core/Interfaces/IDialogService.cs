namespace HeartRateMonitor.Core.Interfaces;

public interface IDialogService
{
    void ShowMessage(string message, string title);

    /// <summary>询问确认，用户点“是”返回 <c>true</c>。</summary>
    bool Confirm(string message, string title);

    void ShowSettingsDialog();
}
