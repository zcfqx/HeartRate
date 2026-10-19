namespace HeartRateMonitor.Core.Enums;

/// <summary>
/// 蓝牙无线电/适配器的可用状态。
/// </summary>
public enum BleAvailability
{
    /// <summary>适配器存在且蓝牙开关已打开，可以正常扫描。</summary>
    Ready = 0,

    /// <summary>适配器存在，但系统蓝牙开关处于关闭状态。</summary>
    RadioOff,

    /// <summary>未检测到支持低功耗蓝牙（BLE）的适配器。</summary>
    NoAdapter,

    /// <summary>系统拒绝了蓝牙访问。</summary>
    AccessDenied,

    /// <summary>其它未知错误。</summary>
    Unknown
}

/// <summary>
/// <see cref="BleAvailability"/> 的界面文案。
/// </summary>
public static class BleAvailabilityExtensions
{
    /// <summary>
    /// 返回可直接展示给用户的中文说明与处理建议。
    /// </summary>
    public static string ToUserMessage(this BleAvailability availability) => availability switch
    {
        BleAvailability.Ready =>
            "蓝牙已就绪",
        BleAvailability.RadioOff =>
            "蓝牙已关闭：请在「设置 → 蓝牙和其他设备」中打开蓝牙（或按 Win+A 点击蓝牙快捷开关）后重试。",
        BleAvailability.NoAdapter =>
            "未检测到可用的低功耗蓝牙（BLE）适配器，请确认电脑支持蓝牙且驱动正常。",
        BleAvailability.AccessDenied =>
            "系统拒绝了蓝牙访问，请检查 Windows 设置中的蓝牙开关与隐私权限。",
        _ =>
            "蓝牙当前不可用，请稍后重试。"
    };

    /// <summary>
    /// 返回简短的状态文字（用于状态栏等窄小位置）。
    /// </summary>
    public static string ToShortStatus(this BleAvailability availability) => availability switch
    {
        BleAvailability.Ready => "未扫描",
        BleAvailability.RadioOff => "蓝牙未开启",
        BleAvailability.NoAdapter => "无蓝牙适配器",
        BleAvailability.AccessDenied => "蓝牙权限被拒绝",
        _ => "蓝牙不可用"
    };
}
