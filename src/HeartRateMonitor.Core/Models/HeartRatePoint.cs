namespace HeartRateMonitor.Core.Models;

/// <summary>会话中的一个心率采样点（值类型，避免图表绘制时产生大量小对象）。</summary>
public readonly record struct HeartRatePoint(int Bpm, DateTime Timestamp);
