using MedApp.Models;
using SQLite;

namespace MedApp.Data;

/// <summary>
/// Test-only DatabaseContext that uses a temp file instead of MAUI's FileSystem.
/// Matches the API surface that MedicationRepository and DoseLogRepository expect.
/// </summary>
public class DatabaseContext : IDisposable
{
    private const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache;

    private readonly string _dbPath;
    private readonly SQLiteAsyncConnection _db;

    public DatabaseContext()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medapp_test_{Guid.NewGuid():N}.db3");
        _db = new SQLiteAsyncConnection(_dbPath, Flags);
    }

    public SQLiteAsyncConnection Database => _db;

    public async Task InitAsync()
    {
        await _db.CreateTableAsync<Medication>();
        await _db.CreateTableAsync<Schedule>();
        await _db.CreateTableAsync<DoseLog>();
    }

    public void Dispose()
    {
        _db.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
