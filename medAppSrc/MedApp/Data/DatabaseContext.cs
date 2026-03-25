using MedApp.Models;
using SQLite;

namespace MedApp.Data;

/// <summary>
/// Owns the single SQLiteAsyncConnection for the app lifetime.
/// Call <see cref="InitAsync"/> once at startup (done in App.xaml.cs).
/// </summary>
public class DatabaseContext
{
    private const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache;

    private readonly SQLiteAsyncConnection _db;

    public DatabaseContext()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "medapp.db3");
        _db = new SQLiteAsyncConnection(dbPath, Flags);
    }

    public SQLiteAsyncConnection Database => _db;

    /// <summary>Creates tables if they don't exist. Safe to call on every startup.</summary>
    public async Task InitAsync()
    {
        await _db.CreateTableAsync<Medication>().ConfigureAwait(false);
        await _db.CreateTableAsync<Schedule>().ConfigureAwait(false);
        await _db.CreateTableAsync<DoseLog>().ConfigureAwait(false);
    }
}
