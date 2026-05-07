using Dapper;
using HeartRateMonitor.Data.Database;
using HeartRateMonitor.Data.Entities;

namespace HeartRateMonitor.Data.Repositories;

public class HeartRateRepository
{
    private readonly DatabaseInitializer _db;

    public HeartRateRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task InsertAsync(HeartRateRecordEntity record)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "INSERT INTO HeartRateRecord (HeartRate, Timestamp, RRInterval, IsSensorContact, DeviceId) VALUES (@HeartRate, @Timestamp, @RRInterval, @IsSensorContact, @DeviceId)",
            record);
    }

    public async Task InsertBatchAsync(IEnumerable<HeartRateRecordEntity> records)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "INSERT INTO HeartRateRecord (HeartRate, Timestamp, RRInterval, IsSensorContact, DeviceId) VALUES (@HeartRate, @Timestamp, @RRInterval, @IsSensorContact, @DeviceId)",
            records);
    }

    public async Task<IEnumerable<HeartRateRecordEntity>> GetByTimeRangeAsync(long startTimestamp, long endTimestamp)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        return await connection.QueryAsync<HeartRateRecordEntity>(
            "SELECT * FROM HeartRateRecord WHERE Timestamp >= @Start AND Timestamp <= @End ORDER BY Timestamp",
            new { Start = startTimestamp, End = endTimestamp });
    }

    public async Task<int> GetAverageHeartRateAsync(long startTimestamp, long endTimestamp)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(AVG(HeartRate), 0) FROM HeartRateRecord WHERE Timestamp >= @Start AND Timestamp <= @End",
            new { Start = startTimestamp, End = endTimestamp });
    }

    public async Task<int> GetMaxHeartRateAsync(long startTimestamp, long endTimestamp)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(HeartRate), 0) FROM HeartRateRecord WHERE Timestamp >= @Start AND Timestamp <= @End",
            new { Start = startTimestamp, End = endTimestamp });
    }

    public async Task<int> GetMinHeartRateAsync(long startTimestamp, long endTimestamp)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MIN(HeartRate), 0) FROM HeartRateRecord WHERE Timestamp >= @Start AND Timestamp <= @End",
            new { Start = startTimestamp, End = endTimestamp });
    }

    public async Task DeleteBeforeAsync(long timestamp)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "DELETE FROM HeartRateRecord WHERE Timestamp < @Timestamp",
            new { Timestamp = timestamp });
    }
}
