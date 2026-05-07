using HeartRateMonitor.Data.Database;

namespace HeartRateMonitor.Tests;

[TestClass]
public class DatabaseTests
{
    private string _dbPath = null!;
    private DatabaseInitializer _dbInitializer = null!;

    [TestInitialize]
    public void Setup()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_heartrate_{Guid.NewGuid()}.db");
        _dbInitializer = new DatabaseInitializer(_dbPath);
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
                File.Delete(_dbPath);

            var walPath = _dbPath + "-wal";
            var shmPath = _dbPath + "-shm";
            if (File.Exists(walPath)) File.Delete(walPath);
            if (File.Exists(shmPath)) File.Delete(shmPath);
        }
        catch
        {
            // SQLite may still hold the file briefly
        }
    }

    [TestMethod]
    public async Task InitializeAsync_Creates_Database_File()
    {
        await _dbInitializer.InitializeAsync();
        Assert.IsTrue(File.Exists(_dbPath));
    }

    [TestMethod]
    public async Task InitializeAsync_Can_Be_Called_Multiple_Times()
    {
        await _dbInitializer.InitializeAsync();
        await _dbInitializer.InitializeAsync();
        Assert.IsTrue(File.Exists(_dbPath));
    }
}
