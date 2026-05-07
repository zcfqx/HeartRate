using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Services.HeartRate;

public class HeartRateParser : IHeartRateParser
{
    private const byte HeartRateValueFormatMask = 0x01;
    private const byte SensorContactStatusMask = 0x06;
    private const byte EnergyExpendedStatusMask = 0x08;
    private const byte RRIntervalMask = 0x10;

    public HeartRateData Parse(byte[] rawData)
    {
        if (rawData == null || rawData.Length < 2)
        {
            throw new ArgumentException("Invalid heart rate data: too short");
        }

        var data = new HeartRateData
        {
            Timestamp = DateTime.Now,
            IsSensorContact = (rawData[0] & SensorContactStatusMask) == SensorContactStatusMask
        };

        int offset;
        bool is16Bit = (rawData[0] & HeartRateValueFormatMask) != 0;

        if (is16Bit)
        {
            if (rawData.Length < 3)
            {
                throw new ArgumentException("Invalid 16-bit heart rate data: too short");
            }
            data.HeartRate = rawData[1] | (rawData[2] << 8);
            offset = 3;
        }
        else
        {
            data.HeartRate = rawData[1];
            offset = 2;
        }

        if ((rawData[0] & EnergyExpendedStatusMask) != 0)
        {
            offset += 2;
        }

        if ((rawData[0] & RRIntervalMask) != 0 && offset + 1 < rawData.Length)
        {
            int rrRaw = rawData[offset] | (rawData[offset + 1] << 8);
            data.RRInterval = (int)(rrRaw / 1024.0 * 1000.0);
        }

        return data;
    }
}
