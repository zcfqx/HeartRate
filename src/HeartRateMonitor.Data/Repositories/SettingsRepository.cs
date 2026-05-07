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
