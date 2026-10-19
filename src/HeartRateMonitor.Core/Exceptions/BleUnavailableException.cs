using HeartRateMonitor.Core.Enums;

namespace HeartRateMonitor.Core.Exceptions;

/// <summary>
/// 蓝牙不可用（未开启、无适配器、被拒绝等）时抛出，携带具体原因，
/// 便于界面给出明确提示而不是空白错误信息。
/// </summary>
public sealed class BleUnavailableException : Exception
{
    public BleUnavailableException(BleAvailability availability, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Availability = availability;
    }

    /// <summary>不可用的具体原因。</summary>
    public BleAvailability Availability { get; }
}
