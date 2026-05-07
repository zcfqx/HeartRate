using Dapper;
using HeartRateMonitor.Data.Database;
using HeartRateMonitor.Data.Entities;

namespace HeartRateMonitor.Data.Repositories;

public class DeviceRepository
{
    private readonly DatabaseInitializer _db;

    public DeviceRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task UpsertAsync(DeviceInfoEntity device)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            @"INSERT OR REPLACE INTO DeviceInfo (DeviceId, DeviceName, LastConnected, IsAutoConnect)
              VALUES (@DeviceId, @DeviceName, @LastConnected, @IsAutoConnect)",
            device);
    }

    public async Task<DeviceInfoEntity?> GetByDeviceIdAsync(string deviceId)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        return await connection.QueryFirstOrDefaultAsync<DeviceInfoEntity>(
            "SELECT * FROM DeviceInfo WHERE DeviceId = @DeviceId",
            new { DeviceId = deviceId });
    }

    public async Task<DeviceInfoEntity?> GetLastConnectedAsync()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        return await connection.QueryFirstOrDefaultAsync<DeviceInfoEntity>(
            "SELECT * FROM DeviceInfo ORDER BY LastConnected DESC LIMIT 1");
    }

    public async Task<IEnumerable<DeviceInfoEntity>> GetAllAsync()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        return await connection.QueryAsync<DeviceInfoEntity>(
            "SELECT * FROM DeviceInfo ORDER BY LastConnected DESC");
    }
}
