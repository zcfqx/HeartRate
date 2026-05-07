using System.Collections.Concurrent;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Services.HeartRate;

public class HeartRateService : IHeartRateService
{
    private readonly IHeartRateCalculator _calculator;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<HeartRateData> _history = new();
    private readonly object _lock = new();
    private const int MaxHistorySize = 10000;

    private int _currentHeartRate;
    private HeartRateData? _latestData;

    public event EventHandler<HeartRateChangedEventArgs>? HeartRateUpdated;

    public int CurrentHeartRate => _currentHeartRate;
    public HeartRateData? LatestData => _latestData;
    public IReadOnlyList<HeartRateData> RecentHistory
    {
        get
        {
            lock (_lock)
            {
                return _history.ToArray();
            }
        }
    }

    public HeartRateService(IHeartRateCalculator calculator, ILogger logger)
    {
        _calculator = calculator;
        _logger = logger;
    }

    public void UpdateHeartRate(HeartRateData data)
    {
        if (data == null) return;

        _currentHeartRate = data.HeartRate;
        _latestData = data;

        _history.Enqueue(data);

        while (_history.Count > MaxHistorySize)
        {
            _history.TryDequeue(out _);
        }

        HeartRateUpdated?.Invoke(this, new HeartRateChangedEventArgs(data));
    }

    public Task<HeartRateStatistics> GetStatisticsAsync(DateTime startTime, DateTime endTime)
    {
        IReadOnlyList<HeartRateData> snapshot;
        lock (_lock)
        {
            snapshot = _history.ToArray();
        }

        var stats = _calculator.CalculateStatistics(snapshot, startTime, endTime);
        return Task.FromResult(stats);
    }

    public Task<DailyReport> GetDailyReportAsync(DateTime date)
    {
        IReadOnlyList<HeartRateData> snapshot;
        lock (_lock)
        {
            snapshot = _history.ToArray();
        }

        var report = _calculator.GenerateDailyReport(snapshot, date);
        return Task.FromResult(report);
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            while (_history.TryDequeue(out _)) { }
        }
    }
}
