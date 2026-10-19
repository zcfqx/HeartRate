using Microsoft.Data.Sqlite;

namespace HeartRateMonitor.Data.Database;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string? dbPath = null)
    {
        var path = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeartRateMonitor", "heartrate.db");

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // 连接池 + 15 秒忙等待：心率数据每秒落库与设置保存并发时不会直接抛 database is locked。
            Pooling = true,
            DefaultTimeout = 15
        };
        _connectionString = builder.ConnectionString;
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // WAL 允许读写并发；busy_timeout 让写冲突自动等待而不是立刻失败。
        var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=15000;";
        await pragma.ExecuteNonQueryAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS HeartRateRecord (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                HeartRate INTEGER NOT NULL,
                Timestamp INTEGER NOT NULL,
                RRInterval INTEGER,
                IsSensorContact INTEGER NOT NULL DEFAULT 1,
                DeviceId TEXT
            );

            CREATE INDEX IF NOT EXISTS IX_HeartRateRecord_Timestamp ON HeartRateRecord(Timestamp);

            CREATE TABLE IF NOT EXISTS DeviceInfo (
                DeviceId TEXT PRIMARY KEY,
                DeviceName TEXT NOT NULL,
                LastConnected INTEGER NOT NULL,
                IsAutoConnect INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                Description TEXT
            );
        ";

        await command.ExecuteNonQueryAsync();
    }
}
