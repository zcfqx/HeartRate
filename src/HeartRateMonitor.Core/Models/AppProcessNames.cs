namespace HeartRateMonitor.Core.Models;

/// <summary>
/// 识别“属于本应用”的进程名，用于单实例接管与僵尸进程清理。
/// 单文件发布的 exe 常带版本号（如 HeartRate-1.3.1-win-x64-self-contained.exe），
/// 与开发时运行的 HeartRateMonitor.App.exe 不同名；如果只按自身进程名去查找，
/// 发布版就无法结束旧版本留下的僵尸进程，启动时会误判“已经在运行”。
/// </summary>
public static class AppProcessNames
{
    /// <summary>可能的进程名基名：仓库名前缀 HeartRate，以及历史名 HeartRateMonitor。</summary>
    private static readonly string[] BaseNames = { "HeartRateMonitor", "HeartRate" };

    /// <summary>
    /// 判断进程名是否属于本应用。允许精确基名、带 . 的旧名，以及
    /// HeartRate-1.3.1-win-x64 这类用 - / _ 分隔的发布名。
    /// </summary>
    public static bool IsAppProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var name = processName.Trim();
        if (Path.GetExtension(name).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = Path.GetFileNameWithoutExtension(name);
        }

        foreach (var baseName in BaseNames)
        {
            if (name.Equals(baseName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (name.Length <= baseName.Length ||
                !name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separator = name[baseName.Length];
            if (separator is '.' or '-' or '_')
            {
                return true;
            }
        }

        return false;
    }
}
