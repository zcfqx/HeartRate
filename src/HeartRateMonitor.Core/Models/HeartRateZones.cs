namespace HeartRateMonitor.Core.Models;

/// <summary>心率区间定义（用于界面着色与区间统计）。</summary>
public sealed record HeartRateZoneInfo(
    string Key,
    string Name,
    string ShortName,
    string Color,
    int Min,
    int Max);

/// <summary>
/// 心率区间目录：全局唯一的区间边界与配色，界面与统计共用，避免各处硬编码不一致。
/// </summary>
public static class HeartRateZones
{
    /// <summary>最大心率（用于计算百分比，默认 220 - 年龄 的常见估算上限）。</summary>
    public const int MaxHeartRate = 200;

    public static readonly IReadOnlyList<HeartRateZoneInfo> All =
    [
        new("rest", "放松", "放松", "#6366F1", 0, 99),
        new("fatburn", "燃脂", "燃脂", "#22C55E", 100, 139),
        new("cardio", "有氧", "有氧", "#F59E0B", 140, 169),
        new("peak", "无氧", "无氧", "#F97316", 170, 199),
        new("max", "极限", "极限", "#EF4444", 200, int.MaxValue)
    ];

    /// <summary>返回该心率所属区间。负数按放松处理。</summary>
    public static HeartRateZoneInfo Classify(int heartRate)
    {
        foreach (var zone in All)
        {
            if (heartRate <= zone.Max && heartRate >= zone.Min)
            {
                return zone;
            }
        }

        return heartRate < 0 ? All[0] : All[^1];
    }

    /// <summary>返回该心率所属区间的序号（0 起）。</summary>
    public static int IndexOf(int heartRate)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (heartRate <= All[i].Max && heartRate >= All[i].Min)
            {
                return i;
            }
        }

        return heartRate < 0 ? 0 : All.Count - 1;
    }
}
