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

        _connectionString = $"Data Source={path}";
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

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
