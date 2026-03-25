using MedApp.Models;

namespace MedApp.Data;

/// <summary>
/// CRUD for Medication rows and their associated Schedule rows.
/// </summary>
public class MedicationRepository
{
    private readonly DatabaseContext _context;

    public MedicationRepository(DatabaseContext context) => _context = context;

    // ── Medications ───────────────────────────────────────────────────────────

    public Task<List<Medication>> GetAllAsync() =>
        _context.Database.Table<Medication>()
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync();

    public async Task<Medication?> GetByIdAsync(int id) =>
        await _context.Database.Table<Medication>()
            .Where(m => m.Id == id)
            .FirstOrDefaultAsync();

    public Task<int> InsertAsync(Medication medication) =>
        _context.Database.InsertAsync(medication);

    public Task<int> UpdateAsync(Medication medication) =>
        _context.Database.UpdateAsync(medication);

    /// <summary>
    /// Soft-delete: marks the medication and all its schedules as inactive
    /// so that historical dose logs remain intact.
    /// </summary>
    public async Task SoftDeleteAsync(int medicationId)
    {
        var med = await GetByIdAsync(medicationId);
        if (med is null) return;

        med.IsActive = false;
        await _context.Database.UpdateAsync(med);

        var schedules = await GetSchedulesByMedicationIdAsync(medicationId);
        foreach (var s in schedules)
        {
            s.IsActive = false;
            await _context.Database.UpdateAsync(s);
        }
    }

    // ── Schedules ─────────────────────────────────────────────────────────────

    public Task<List<Schedule>> GetSchedulesByMedicationIdAsync(int medicationId) =>
        _context.Database.Table<Schedule>()
            .Where(s => s.MedicationId == medicationId && s.IsActive)
            .OrderBy(s => s.TimeOfDayTicks)
            .ToListAsync();

    /// <summary>Returns every active schedule across all medications (used by the notification engine).</summary>
    public Task<List<Schedule>> GetAllActiveSchedulesAsync() =>
        _context.Database.Table<Schedule>()
            .Where(s => s.IsActive)
            .ToListAsync();

    public Task<int> InsertScheduleAsync(Schedule schedule) =>
        _context.Database.InsertAsync(schedule);

    public Task<int> UpdateScheduleAsync(Schedule schedule) =>
        _context.Database.UpdateAsync(schedule);

    public Task<int> DeleteScheduleAsync(int scheduleId) =>
        _context.Database.DeleteAsync<Schedule>(scheduleId);
}
