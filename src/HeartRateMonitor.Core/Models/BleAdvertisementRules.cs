namespace HeartRateMonitor.Core.Models;

/// <summary>
/// BLE 广播的收录与展示规则。抽成纯函数，既方便单测，也让扫描服务与界面
/// 使用同一套判断，避免“服务发现了、界面却不显示”这类不一致。
/// </summary>
public static class BleAdvertisementRules
{
    /// <summary>未广播名称的心率设备使用的占位名称。</summary>
    public const string HeartRatePlaceholderName = "心率传感器";

    /// <summary>
    /// 是否把一条广播收录进候选设备列表：必须可连接，并且要么有名称，
    /// 要么广播里带了心率服务 UUID（部分心率带不广播名称）。
    /// </summary>
    public static bool ShouldDiscover(bool isConnectable, bool hasName, bool advertisesHeartRate)
        => isConnectable && (hasName || advertisesHeartRate);

    /// <summary>
    /// 合并同一次扫描中同一 MAC 的多条广播：优先采用广播里的真实名称，
    /// 没有名称时保留已有名称（或占位名），不把真实名称降级成占位名。
    /// </summary>
    public static string ResolveName(string? currentName, string? advertisedName, bool advertisesHeartRate)
    {
        if (!string.IsNullOrWhiteSpace(advertisedName))
        {
            return advertisedName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(currentName))
        {
            return currentName;
        }

        return advertisesHeartRate ? HeartRatePlaceholderName : string.Empty;
    }

    /// <summary>已有条目是否需要因为收到更完整的广播而更新名称。</summary>
    public static bool ShouldUpdateName(string? currentName, string? advertisedName)
    {
        if (string.IsNullOrWhiteSpace(advertisedName))
        {
            return false;
        }

        return !string.Equals(currentName, advertisedName.Trim(), StringComparison.Ordinal);
    }

    /// <summary>列表顺序：心率设备优先，其次按信号强度，最后按名称稳定排序。</summary>
    public static IReadOnlyList<BleDevice> SortForDisplay(IEnumerable<BleDevice> devices)
        => devices
            .OrderByDescending(device => device.AdvertisesHeartRate)
            .ThenByDescending(device => device.SignalStrength)
            .ThenBy(device => device.DeviceName, StringComparer.CurrentCulture)
            .ToList();
}
