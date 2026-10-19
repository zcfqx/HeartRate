using System.Diagnostics;
using System.Text;
using HeartRateMonitor.Core.Interfaces;

namespace HeartRateMonitor.Services.Logging;

/// <summary>
/// 轻量文件日志：替代 Serilog（去掉第三方依赖与相应的程序集体积）。
/// <para>按天滚动，只保留最近 7 天；写入失败时静默忽略，绝不影响主流程。</para>
/// </summary>
public sealed class FileLogger : ILogger
{
    private const int MaxFileBytes = 4 * 1024 * 1024;
    private const int RetainedDays = 7;

    private readonly string _directory;
    private readonly object _gate = new();
    private readonly Stopwatch _sinceFlush = Stopwatch.StartNew();
    private DateTime _today;
    private string? _currentPath;

    /// <summary>额外的实时日志接收器（用于启动早期诊断输出）。</summary>
    public Action<string>? Sink { get; set; }

    public FileLogger(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeartRateMonitor", "logs");

        try
        {
            Directory.CreateDirectory(_directory);
            Cleanup();
        }
        catch
        {
            // 日志目录不可用时降级为仅输出到 Sink
        }
    }

    public void Debug(string message) => Write("DBG", message);
    public void Info(string message) => Write("INF", message);
    public void Warning(string message) => Write("WRN", message);

    public void Error(string message, Exception? exception = null)
        => Write("ERR", exception == null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
        Sink?.Invoke(line);

        try
        {
            lock (_gate)
            {
                var now = DateTime.Today;
                if (now != _today || _currentPath is null)
                {
                    _today = now;
                    _currentPath = Path.Combine(_directory, $"app-{now:yyyyMMdd}.log");
                    Cleanup();
                }

                var file = new FileInfo(_currentPath);
                if (file.Exists && file.Length > MaxFileBytes)
                {
                    File.Move(_currentPath, _currentPath + ".bak", overwrite: true);
                }

                File.AppendAllText(_currentPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志不可写时静默降级
        }
    }

    private void Cleanup()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetainedDays);
            foreach (var file in Directory.EnumerateFiles(_directory, "app-*.log*"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // 忽略清理失败
        }
    }
}
