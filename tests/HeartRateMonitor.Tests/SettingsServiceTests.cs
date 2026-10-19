using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Data.Database;
using HeartRateMonitor.Data.Repositories;
using HeartRateMonitor.Services.Settings;
using Microsoft.Data.Sqlite;

namespace HeartRateMonitor.Tests;

[TestClass]
public class SettingsServiceTests
{
    private string _dbPath = null!;
    private DatabaseInitializer _db = null!;
    private SettingsRepository _repository = null!;
    private SettingsService _settings = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_settings_{Guid.NewGuid()}.db");
        _db = new DatabaseInitializer(_dbPath);
        await _db.InitializeAsync();
        _repository = new SettingsRepository(_db);
        _settings = new SettingsService(_repository, new NullLogger());
        await _settings.LoadAsync();
    }

    [TestCleanup]
    public void Cleanup()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Thread.Sleep(100);

        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }

            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                if (File.Exists(_dbPath + suffix))
                {
                    File.Delete(_dbPath + suffix);
                }
            }
        }
        catch
        {
            // SQLite 可能短暂占用文件，忽略清理失败
        }
    }

    [TestMethod]
    public async Task Initialize_Uses_Wal_Journal_Mode()
    {
        await using var connection = new SqliteConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var mode = (string?)await command.ExecuteScalarAsync();

        Assert.AreEqual("wal", mode?.ToLowerInvariant());
    }

    [TestMethod]
    public void MinimalShowChart_Defaults_To_True()
    {
        Assert.IsTrue(_settings.MinimalShowChart);
    }

    [TestMethod]
    public async Task MinimalShowChart_RoundTrips()
    {
        _settings.MinimalShowChart = false;
        await _settings.SaveAsync();

        var reloaded = new SettingsService(_repository, new NullLogger());
        await reloaded.LoadAsync();

        Assert.IsFalse(reloaded.MinimalShowChart);
    }

    [TestMethod]
    public async Task OverlayOpacity_Zero_RoundTrips()
    {
        _settings.OverlayOpacity = 0;
        await _settings.SaveAsync();

        var saved = await _repository.GetValueAsync("OverlayOpacity");
        var reloaded = new SettingsService(_repository, new NullLogger());
        await reloaded.LoadAsync();

        Assert.AreEqual(0d, reloaded.OverlayOpacity, 0.001);
        Assert.IsNotNull(saved);
        Assert.AreEqual(0d, double.Parse(saved), 0.001);
    }

    [TestMethod]
    public async Task Concurrent_Saves_Do_Not_Throw_Database_Is_Locked()
    {
        var tasks = new List<Task>();
        for (var i = 0; i < 8; i++)
        {
            var opacity = 0.5 + i * 0.05;
            tasks.Add(Task.Run(async () =>
            {
                var service = new SettingsService(_repository, new NullLogger());
                await service.LoadAsync();
                service.OverlayOpacity = opacity;
                service.MinimalMode = i % 2 == 0;
                await service.SaveAsync();
            }));
        }

        await Task.WhenAll(tasks);

        var reloaded = new SettingsService(_repository, new NullLogger());
        await reloaded.LoadAsync();
        Assert.IsTrue(reloaded.OverlayOpacity is >= 0.5 and <= 0.9);
    }

    private sealed class NullLogger : ILogger
    {
        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }

        public void Debug(string message)
        {
        }
    }
}
