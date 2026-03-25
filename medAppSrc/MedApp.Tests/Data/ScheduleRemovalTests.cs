using MedApp.Data;
using MedApp.Models;

namespace MedApp.Tests.Data;

/// <summary>
/// Tests for removing daily schedule times from a medication
/// and verifying delete-gating based on active schedules.
/// </summary>
public class ScheduleRemovalTests : IDisposable
{
    private readonly DatabaseContext _db;
    private readonly MedicationRepository _repo;

    public ScheduleRemovalTests()
    {
        _db = new DatabaseContext();
        _db.InitAsync().GetAwaiter().GetResult();
        _repo = new MedicationRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Change 1: Delete a Daily schedule time ───────────────────────────────

    [Fact]
    public async Task RemoveOneSchedule_LeavesRemainingSchedule()
    {
        var med = new Medication { Name = "Aspirin" };
        await _repo.InsertAsync(med);

        var s1 = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(8, 0, 0) };
        var s2 = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(20, 0, 0) };
        await _repo.InsertScheduleAsync(s1);
        await _repo.InsertScheduleAsync(s2);

        // Remove the first schedule
        await _repo.DeleteScheduleAsync(s1.Id);

        var remaining = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        Assert.Single(remaining);
        Assert.Equal(new TimeSpan(20, 0, 0), remaining[0].TimeOfDay);
    }

    [Fact]
    public async Task RemoveAllSchedules_LeavesEmptyList()
    {
        var med = new Medication { Name = "Ibuprofen" };
        await _repo.InsertAsync(med);

        var s1 = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(9, 0, 0) };
        await _repo.InsertScheduleAsync(s1);

        // Remove the only schedule
        await _repo.DeleteScheduleAsync(s1.Id);

        var remaining = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task RemoveScheduleAndReAdd_WorksCorrectly()
    {
        var med = new Medication { Name = "Metformin" };
        await _repo.InsertAsync(med);

        var s1 = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(8, 0, 0) };
        await _repo.InsertScheduleAsync(s1);

        // Remove, then add a different time
        await _repo.DeleteScheduleAsync(s1.Id);
        var s2 = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(12, 0, 0) };
        await _repo.InsertScheduleAsync(s2);

        var schedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        Assert.Single(schedules);
        Assert.Equal(new TimeSpan(12, 0, 0), schedules[0].TimeOfDay);
    }

    [Fact]
    public async Task SaveMedicationWithZeroSchedules_PersistsMedicationOnly()
    {
        // Simulates the edit flow: user removes all schedules and saves
        var med = new Medication { Name = "Lisinopril", Dosage = "10mg" };
        await _repo.InsertAsync(med);

        // Originally had a schedule
        var s1 = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(8, 0, 0) };
        await _repo.InsertScheduleAsync(s1);

        // Edit: remove all schedules, update name
        var toEdit = await _repo.GetByIdAsync(med.Id);
        toEdit!.Name = "Lisinopril Updated";
        await _repo.UpdateAsync(toEdit);

        var oldSchedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        foreach (var old in oldSchedules)
            await _repo.DeleteScheduleAsync(old.Id);

        // No new schedules inserted (user removed all)

        // Verify medication persists with no schedules
        var all = await _repo.GetAllAsync();
        var found = Assert.Single(all);
        Assert.Equal("Lisinopril Updated", found.Name);

        var schedules = await _repo.GetSchedulesByMedicationIdAsync(found.Id);
        Assert.Empty(schedules);
    }

    // ── Change 2: Delete Medicine (gated by schedules) ───────────────────────

    [Fact]
    public async Task MedicationWithSchedules_HasNonEmptyScheduleList()
    {
        var med = new Medication { Name = "Atorvastatin" };
        await _repo.InsertAsync(med);
        await _repo.InsertScheduleAsync(new Schedule
        {
            MedicationId = med.Id,
            TimeOfDay = new TimeSpan(21, 0, 0)
        });

        var schedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);

        // Delete should be blocked — schedules exist
        Assert.NotEmpty(schedules);
    }

    [Fact]
    public async Task MedicationWithNoSchedules_CanBeSoftDeleted()
    {
        var med = new Medication { Name = "OldMed", Dosage = "5mg" };
        await _repo.InsertAsync(med);

        // No schedules added — medication is eligible for deletion
        var schedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        Assert.Empty(schedules);

        await _repo.SoftDeleteAsync(med.Id);

        var deleted = await _repo.GetByIdAsync(med.Id);
        Assert.False(deleted!.IsActive);

        // Should not appear in active list
        var all = await _repo.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task RemoveAllSchedulesThenDelete_FullWorkflow()
    {
        // Full workflow: add med + schedule → remove schedule → delete med
        var med = new Medication { Name = "Omeprazole" };
        await _repo.InsertAsync(med);
        var s1 = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(7, 0, 0) };
        await _repo.InsertScheduleAsync(s1);

        // Step 1: Verify can't delete yet (has schedules)
        var schedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        Assert.NotEmpty(schedules);

        // Step 2: Remove all schedules via edit flow
        foreach (var s in schedules)
            await _repo.DeleteScheduleAsync(s.Id);

        // Step 3: Verify schedules are gone
        var remaining = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        Assert.Empty(remaining);

        // Step 4: Now delete is allowed
        await _repo.SoftDeleteAsync(med.Id);

        var all = await _repo.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task DeleteMedication_PreservesDoseHistory()
    {
        var med = new Medication { Name = "TrackedMed" };
        await _repo.InsertAsync(med);
        var s1 = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(10, 0, 0) };
        await _repo.InsertScheduleAsync(s1);

        // Create a dose log for this schedule
        var logRepo = new DoseLogRepository(_db);
        await logRepo.InsertAsync(new DoseLog
        {
            ScheduleId = s1.Id,
            ScheduledAt = DateTime.Today.Add(s1.TimeOfDay),
            Status = DoseStatus.Taken,
            TakenAt = DateTime.Now
        });

        // Remove schedule, then delete medication
        await _repo.DeleteScheduleAsync(s1.Id);
        await _repo.SoftDeleteAsync(med.Id);

        // Dose history should still exist
        var logs = await logRepo.GetByScheduleAndDateAsync(s1.Id, DateTime.Today);
        Assert.NotNull(logs);
        Assert.Equal(DoseStatus.Taken, logs!.Status);
    }
}
