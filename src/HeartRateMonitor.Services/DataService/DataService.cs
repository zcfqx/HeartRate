using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Data.Database;
using HeartRateMonitor.Data.Entities;
using HeartRateMonitor.Data.Repositories;

namespace HeartRateMonitor.Services.DataService;

public class DataService : IDataService
{
    private readonly DatabaseInitializer _dbInitializer;
    private readonly HeartRateRepository _heartRateRepo;
    private readonly DeviceRepository _deviceRepo;
    private readonly ILogger _logger;
    private readonly IHeartRateCalculator _calculator;

    public DataService(
        DatabaseInitializer dbInitializer,
        HeartRateRepository heartRateRepo,
        DeviceRepository deviceRepo,
        ILogger logger,
        IHeartRateCalculator calculator)
    {
        _dbInitializer = dbInitializer;
        _heartRateRepo = heartRateRepo;
        _deviceRepo = deviceRepo;
        _logger = logger;
        _calculator = calculator;
    }

    public async Task InitializeAsync()
    {
        await _dbInitializer.InitializeAsync();
        _logger.Info("Database initialized");
    }

    public async Task SaveHeartRateRecordAsync(HeartRateData data, string? deviceId = null)
    {
        var entity = new HeartRateRecordEntity
        {
            HeartRate = data.HeartRate,
            Timestamp = new DateTimeOffset(data.Timestamp).ToUnixTimeSeconds(),
            RRInterval = data.RRInterval,
            IsSensorContact = data.IsSensorContact,
            DeviceId = deviceId
        };

        await _heartRateRepo.InsertAsync(entity);
    }

    public async Task<List<HeartRateData>> GetHeartRateRecordsAsync(DateTime startTime, DateTime endTime)
    {
        var startTs = new DateTimeOffset(startTime).ToUnixTimeSeconds();
        var endTs = new DateTimeOffset(endTime).ToUnixTimeSeconds();

        var entities = await _heartRateRepo.GetByTimeRangeAsync(startTs, endTs);

        return entities.Select(e => new HeartRateData
        {
            HeartRate = e.HeartRate,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(e.Timestamp).LocalDateTime,
            RRInterval = e.RRInterval,
            IsSensorContact = e.IsSensorContact
        }).ToList();
    }

    public async Task<DailyReport> GetDailyReportAsync(DateTime date)
    {
        var records = await GetHeartRateRecordsAsync(date.Date, date.Date.AddDays(1));
        return _calculator.GenerateDailyReport(records, date);
    }

    public async Task SaveDeviceInfoAsync(BleDevice device)
    {
        var entity = new DeviceInfoEntity
        {
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            LastConnected = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            IsAutoConnect = true
        };

        await _deviceRepo.UpsertAsync(entity);
    }

    public async Task<BleDevice?> GetLastConnectedDeviceAsync()
    {
        var entity = await _deviceRepo.GetLastConnectedAsync();
        if (entity == null) return null;

        return new BleDevice
        {
            DeviceId = entity.DeviceId,
            DeviceName = entity.DeviceName
        };
    }

    public async Task<List<BleDevice>> GetPairedDevicesAsync()
    {
        var entities = await _deviceRepo.GetAllAsync();
        return entities.Select(e => new BleDevice
        {
            DeviceId = e.DeviceId,
            DeviceName = e.DeviceName
        }).ToList();
    }

    public async Task CleanupOldDataAsync(TimeSpan retention)
    {
        var cutoff = DateTimeOffset.UtcNow - retention;
        await _heartRateRepo.DeleteBeforeAsync(cutoff.ToUnixTimeSeconds());
        _logger.Info($"Cleaned up data older than {retention.TotalDays} days");
    }
}
