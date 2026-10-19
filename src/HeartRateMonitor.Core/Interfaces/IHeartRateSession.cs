using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Core.Interfaces;

/// <summary>
/// 实时心率会话：把蓝牙上报的原始数据聚合成界面与插件直接可用的轻量状态。
/// <para>
/// 整个应用只维护一份会话数据（环形缓冲 + 增量统计），各组件订阅同一份状态，
/// 避免每个界面各自维护一份历史列表造成重复计算与内存浪费。
/// </para>
/// </summary>
public interface IHeartRateSession
{
    /// <summary>最近一次心率值（bpm）。会话未开始时为 0。</summary>
    int CurrentBpm { get; }

    /// <summary>最近一次原始数据（含 RR 间期）。</summary>
    HeartRateData? Latest { get; }

    /// <summary>本次会话开始时间。未开始时为 null。</summary>
    DateTime? StartedAt { get; }

    /// <summary>本次会话已持续时间。</summary>
    TimeSpan Duration { get; }

    int MinBpm { get; }
    int MaxBpm { get; }
    double AverageBpm { get; }

    /// <summary>本次会话累计样本数。</summary>
    int SampleCount { get; }

    /// <summary>本次会话估算消耗卡路里。</summary>
    double Calories { get; }

    /// <summary>最近采样点（按时间升序，容量有限）。</summary>
    IReadOnlyList<HeartRatePoint> Points { get; }

    /// <summary>收到新采样点（已切回创建会话时的同步上下文）。</summary>
    event EventHandler<HeartRateChangedEventArgs>? SampleReceived;

    /// <summary>蓝牙连接状态变化。</summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>会话被重置（开始新会话）。</summary>
    event EventHandler? SessionReset;

    /// <summary>清空当前会话数据并重新计时。</summary>
    void Reset();
}
