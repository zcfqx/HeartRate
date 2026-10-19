namespace HeartRateMonitor.Core.Events;

/// <summary>
/// 扫描过程中断（例如扫描途中蓝牙被关闭）时的事件参数。
/// </summary>
public sealed class BleScanFailedEventArgs : EventArgs
{
    public BleScanFailedEventArgs(string message)
    {
        Message = message;
    }

    /// <summary>可直接展示给用户的说明。</summary>
    public string Message { get; }
}
