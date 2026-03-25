using MedApp.Models;

namespace MedApp.Data;

/// <summary>
/// Reads and writes DoseLog records.
/// Today's view, history, and reports all query through here.
/// </summary>
public class DoseLogRepository
{
    private readonly DatabaseContext _context;

    public DoseLogRepository(DatabaseContext context) => _context = context;

    // ── Queries ───────────────────────────────────────────────────────────────

    public Task<List<DoseLog>> GetByDateAsync(DateTime date)
    {
        var start = date.Date;
        var end   = start.AddDays(1);
        return _context.Database.Table<DoseLog>()
            .Where(d => d.ScheduledAt >= start && d.ScheduledAt < end)
            .OrderBy(d => d.ScheduledAt)
            .ToListAsync();
    }

    public Task<List<DoseLog>> GetByDateRangeAsync(DateTime start, DateTime end) =>
        _context.Database.Table<DoseLog>()
            .Where(d => d.ScheduledAt >= start && d.ScheduledAt < end)
            .OrderBy(d => d.ScheduledAt)
            .ToListAsync();

    public Task<List<DoseLog>> GetByScheduleIdAsync(int scheduleId) =>
        _context.Database.Table<DoseLog>()
            .Where(d => d.ScheduleId == scheduleId)
            .OrderByDescending(d => d.ScheduledAt)
            .ToListAsync();

    /// <summary>
    /// Looks up the log entry for a specific schedule on a specific calendar day.
    /// Returns null if the dose hasn't been logged yet.
    /// </summary>
    public async Task<DoseLog?> GetByScheduleAndDateAsync(int scheduleId, DateTime date)
    {
        var start = date.Date;
        var end   = start.AddDays(1);
        return await _context.Database.Table<DoseLog>()
            .Where(d => d.ScheduleId == scheduleId &&
                        d.ScheduledAt >= start &&
                        d.ScheduledAt < end)
            .FirstOrDefaultAsync();
    }

    public Task<int> GetMissedCountAsync(DateTime start, DateTime end) =>
        _context.Database.Table<DoseLog>()
            .Where(d => d.ScheduledAt >= start &&
                        d.ScheduledAt < end &&
                        d.Status == DoseStatus.Missed)
            .CountAsync();

    // ── Writes ────────────────────────────────────────────────────────────────

    public Task<int> InsertAsync(DoseLog log) =>
        _context.Database.InsertAsync(log);

    public Task<int> UpdateAsync(DoseLog log) =>
        _context.Database.UpdateAsync(log);
}
