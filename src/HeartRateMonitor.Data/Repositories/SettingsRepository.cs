using Dapper;
using HeartRateMonitor.Data.Database;
using HeartRateMonitor.Data.Entities;

namespace HeartRateMonitor.Data.Repositories;

public class SettingsRepository
{
    private readonly DatabaseInitializer _db;

    public SettingsRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<string?> GetValueAsync(string key)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<string?>(
            "SELECT Value FROM Settings WHERE Key = @Key",
            new { Key = key });
    }

    public async Task SetValueAsync(string key, string value, string? description = null)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            @"INSERT OR REPLACE INTO Settings (Key, Value, Description) VALUES (@Key, @Value, @Description)",
            new { Key = key, Value = value, Description = description });
    }

    /// <summary>
    /// 在一个事务里写入多个设置项：设置保存从“20 次独立连接”变成一次事务，
    /// 既快又不会和心率数据落库互相抢写锁。
    /// </summary>
    public async Task SetManyAsync(IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync(
            @"INSERT OR REPLACE INTO Settings (Key, Value, Description) VALUES (@Key, @Value, NULL)",
            values.Select(kv => new { Key = kv.Key, Value = kv.Value }),
            transaction);
        await transaction.CommitAsync();
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        var entries = await connection.QueryAsync<SettingsEntity>("SELECT * FROM Settings");
        return entries.ToDictionary(e => e.Key, e => e.Value);
    }

    public async Task DeleteAsync(string key)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("DELETE FROM Settings WHERE Key = @Key", new { Key = key });
    }
}
