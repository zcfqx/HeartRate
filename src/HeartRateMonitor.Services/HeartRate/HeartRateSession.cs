using System.Diagnostics;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Services.HeartRate;

/// <summary>
/// 实时心率会话实现。
/// <para>
/// 数据源是 <see cref="IBleService"/>：本类负责订阅、聚合（环形缓冲 + 增量统计 + 卡路里）、
/// 持久化，并把事件切回创建时的同步上下文（通常是 UI 线程），让各组件无需各自做跨线程调度。
/// </para>
/// </summary>
public sealed class HeartRateSession : IHeartRateSession, IDisposable
{
    private const int MaxPoints = 240;
    private const int SaveIntervalMs = 1000;

    private readonly IBleService _ble;
    private readonly IHeartRateService _heartRateService;
    private readonly IDataService _dataService;
    private readonly IHeartRateCalculator _calculator;
    private readonly ILogger _logger;
    private readonly SynchronizationContext? _syncContext;

    private readonly HeartRatePoint[] _ring = new HeartRatePoint[MaxPoints];
    private readonly object _gate = new();
    private readonly Stopwatch _saveStopwatch = Stopwatch.StartNew();

    private int _head;
    private int _count;
    private int _currentBpm;
    private long _sumBpm;
    private int _minBpm;
    private int _maxBpm;
    private int _sampleCount;
    private double _calories;
    private DateTime? _lastSampleAt;
    private DateTime? _startedAt;
    private HeartRateData? _latest;
    private bool _disposed;

    public event EventHandler<HeartRateChangedEventArgs>? SampleReceived;
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler? SessionReset;

    public HeartRateSession(
        IBleService ble,
        IHeartRateService heartRateService,
        IDataService dataService,
        IHeartRateCalculator calculator,
        ILogger logger)
    {
        _ble = ble;
        _heartRateService = heartRateService;
        _dataService = dataService;
        _calculator = calculator;
        _logger = logger;
        _syncContext = SynchronizationContext.Current;

        _ble.HeartRateReceived += OnHeartRateReceived;
        _ble.ConnectionStateChanged += OnBleStateChanged;
    }

    public int CurrentBpm => Volatile.Read(ref _currentBpm);
    public HeartRateData? Latest => _latest;

    public DateTime? StartedAt
    {
        get
        {
            lock (_gate)
            {
                return _startedAt;
            }
        }
    }

    public TimeSpan Duration
    {
        get
        {
            lock (_gate)
            {
                return _startedAt is { } start ? DateTime.Now - start : TimeSpan.Zero;
            }
        }
    }

    public int MinBpm
    {
        get
        {
            lock (_gate)
            {
                return _minBpm;
            }
        }
    }

    public int MaxBpm
    {
        get
        {
            lock (_gate)
            {
                return _maxBpm;
            }
        }
    }

    public double AverageBpm
    {
        get
        {
            lock (_gate)
            {
                return _sampleCount == 0 ? 0 : (double)_sumBpm / _sampleCount;
            }
        }
    }

    public int SampleCount
    {
        get
        {
            lock (_gate)
            {
                return _sampleCount;
            }
        }
    }

    public double Calories
    {
        get
        {
            lock (_gate)
            {
                return _calories;
            }
        }
    }

    public IReadOnlyList<HeartRatePoint> Points
    {
        get
        {
            lock (_gate)
            {
                var result = new HeartRatePoint[_count];
                for (var i = 0; i < _count; i++)
                {
                    // 环形缓冲中最旧的元素位于 (_head - _count + MaxPoints) % MaxPoints
                    result[i] = _ring[(_head - _count + i + MaxPoints * 2) % MaxPoints];
                }

                return result;
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _head = 0;
            _count = 0;
            _sumBpm = 0;
            _minBpm = 0;
            _maxBpm = 0;
            _sampleCount = 0;
            _calories = 0;
            _lastSampleAt = null;
            _startedAt = DateTime.Now;
        }

        Volatile.Write(ref _currentBpm, 0);
        _latest = null;
        Raise(SessionReset);
        _logger.Debug("心率会话已重置");
    }

    private void OnHeartRateReceived(object? sender, HeartRateChangedEventArgs e)
    {
        var data = e.Data;

        lock (_gate)
        {
            _startedAt ??= data.Timestamp;
            _latest = data;

            if (_sampleCount == 0)
            {
                _minBpm = data.HeartRate;
                _maxBpm = data.HeartRate;
            }
            else
            {
                if (data.HeartRate < _minBpm) _minBpm = data.HeartRate;
                if (data.HeartRate > _maxBpm) _maxBpm = data.HeartRate;
            }

            _ring[_head] = new HeartRatePoint(data.HeartRate, data.Timestamp);
            _head = (_head + 1) % MaxPoints;
            if (_count < MaxPoints) _count++;

            _sumBpm += data.HeartRate;
            _sampleCount++;
            Volatile.Write(ref _currentBpm, data.HeartRate);

            if (_lastSampleAt is { } last)
            {
                var seconds = Math.Clamp((data.Timestamp - last).TotalSeconds, 0, 10);
                if (seconds > 0)
                {
                    _calories += _calculator.CalculateCalories(data.HeartRate, TimeSpan.FromSeconds(seconds));
                }
            }

            _lastSampleAt = data.Timestamp;
        }

        // 保持既有服务链可用（统计/日报仍可通过 IHeartRateService 查询）
        _heartRateService.UpdateHeartRate(data);

        if (_saveStopwatch.ElapsedMilliseconds >= SaveIntervalMs)
        {
            _saveStopwatch.Restart();
            var deviceId = _ble.ConnectedDevice?.DeviceId;
            _ = PersistAsync(data, deviceId);
        }

        Raise(SampleReceived, new HeartRateChangedEventArgs(data));
    }

    private async Task PersistAsync(HeartRateData data, string? deviceId)
    {
        try
        {
            await _dataService.SaveHeartRateRecordAsync(data, deviceId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warning($"保存心率记录失败: {ex.Message}");
        }
    }

    private void OnBleStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.State == ConnectionState.Connected)
        {
            // 新连接视为新会话，避免把上一次连接的数据混入本次统计。
            Reset();
        }

        Raise(ConnectionStateChanged, e);
    }

    private void Raise(EventHandler? handler)
    {
        if (handler is null) return;
        Dispatch(() => handler(this, EventArgs.Empty));
    }

    private void Raise<T>(EventHandler<T>? handler, T args) where T : EventArgs
    {
        if (handler is null) return;
        Dispatch(() => handler(this, args));
    }

    /// <summary>把事件切回创建会话时捕获的线程（UI 线程），避免每个订阅者各自 Dispatcher.Invoke。</summary>
    private void Dispatch(Action callback)
    {
        if (_syncContext is not null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Post(_ => callback(), null);
        }
        else
        {
            callback();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ble.HeartRateReceived -= OnHeartRateReceived;
        _ble.ConnectionStateChanged -= OnBleStateChanged;
    }
}
