using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Core.Events;

public class HeartRateChangedEventArgs : EventArgs
{
    public HeartRateData Data { get; }

    public HeartRateChangedEventArgs(HeartRateData data)
    {
        Data = data;
    }
}

public class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionState State { get; }
    public string? DeviceName { get; }

    public ConnectionStateChangedEventArgs(ConnectionState state, string? deviceName = null)
    {
        State = state;
        DeviceName = deviceName;
    }
}

public class HeartRateAlertEventArgs : EventArgs
{
    public HeartRateAlertType AlertType { get; }
    public int HeartRate { get; }
    public string Message { get; }

    public HeartRateAlertEventArgs(HeartRateAlertType alertType, int heartRate, string message)
    {
        AlertType = alertType;
        HeartRate = heartRate;
        Message = message;
    }
}
