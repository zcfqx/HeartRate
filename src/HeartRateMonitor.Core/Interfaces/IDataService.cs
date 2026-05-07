using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Core.Interfaces;

public interface IDataService
{
    Task InitializeAsync();
    Task SaveHeartRateRecordAsync(HeartRateData data, string? deviceId = null);
    Task<List<HeartRateData>> GetHeartRateRecordsAsync(DateTime startTime, DateTime endTime);
    Task<DailyReport> GetDailyReportAsync(DateTime date);
    Task SaveDeviceInfoAsync(BleDevice device);
    Task<BleDevice?> GetLastConnectedDeviceAsync();
    Task<List<BleDevice>> GetPairedDevicesAsync();
    Task CleanupOldDataAsync(TimeSpan retention);
}
