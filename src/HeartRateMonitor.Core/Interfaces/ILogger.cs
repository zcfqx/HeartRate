namespace HeartRateMonitor.Core.Interfaces;

public interface ILogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
    void Debug(string message);
}
