using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Core.Interfaces;

public interface IHeartRateParser
{
    HeartRateData Parse(byte[] rawData);
}
