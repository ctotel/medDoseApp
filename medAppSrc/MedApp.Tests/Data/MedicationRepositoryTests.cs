using MedApp.Data;
using MedApp.Models;

namespace MedApp.Tests.Data;

public class MedicationRepositoryTests : IDisposable
{
    private readonly DatabaseContext _db;
    private readonly MedicationRepository _repo;

    public MedicationRepositoryTests()
    {
        _db = new DatabaseContext();
        _db.InitAsync().GetAwaiter().GetResult();
        _repo = new MedicationRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task InsertAsync_AssignsId()
    {
        var med = new Medication { Name = "Aspirin", Dosage = "100mg" };

        await _repo.InsertAsync(med);

        Assert.True(med.Id > 0);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsInsertedMedication()
    {
        var med = new Medication { Name = "Ibuprofen", Dosage = "200mg" };
        await _repo.InsertAsync(med);

        var result = await _repo.GetByIdAsync(med.Id);

        Assert.NotNull(result);
        Assert.Equal("Ibuprofen", result.Name);
        Assert.Equal("200mg", result.Dosage);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _repo.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActive()
    {
        await _repo.InsertAsync(new Medication { Name = "Active1" });
        await _repo.InsertAsync(new Medication { Name = "Active2" });
        var inactive = new Medication { Name = "Deleted", IsActive = false };
        await _db.Database.InsertAsync(inactive);

        var result = await _repo.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.True(m.IsActive));
    }

    [Fact]
    public async Task GetAllAsync_OrdersByName()
    {
        await _repo.InsertAsync(new Medication { Name = "Zoloft" });
        await _repo.InsertAsync(new Medication { Name = "Aspirin" });
        await _repo.InsertAsync(new Medication { Name = "Metformin" });

        var result = await _repo.GetAllAsync();

        Assert.Equal("Aspirin", result[0].Name);
        Assert.Equal("Metformin", result[1].Name);
        Assert.Equal("Zoloft", result[2].Name);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var med = new Medication { Name = "OldName" };
        await _repo.InsertAsync(med);

        med.Name = "NewName";
        await _repo.UpdateAsync(med);

        var result = await _repo.GetByIdAsync(med.Id);
        Assert.Equal("NewName", result!.Name);
    }

    [Fact]
    public async Task SoftDeleteAsync_DeactivatesMedicationAndSchedules()
    {
        var med = new Medication { Name = "ToDelete" };
        await _repo.InsertAsync(med);
        await _repo.InsertScheduleAsync(new Schedule
        {
            MedicationId = med.Id,
            TimeOfDay = new TimeSpan(8, 0, 0)
        });

        await _repo.SoftDeleteAsync(med.Id);

        var deletedMed = await _repo.GetByIdAsync(med.Id);
        Assert.False(deletedMed!.IsActive);

        var schedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        Assert.Empty(schedules); // filtered by IsActive
    }

    [Fact]
    public async Task SoftDeleteAsync_NoOp_WhenNotFound()
    {
        await _repo.SoftDeleteAsync(999); // should not throw
    }

    // ── Schedule tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task InsertScheduleAsync_AssignsId()
    {
        var med = new Medication { Name = "Test" };
        await _repo.InsertAsync(med);

        var schedule = new Schedule
        {
            MedicationId = med.Id,
            TimeOfDay = new TimeSpan(9, 0, 0)
        };
        await _repo.InsertScheduleAsync(schedule);

        Assert.True(schedule.Id > 0);
    }

    [Fact]
    public async Task GetSchedulesByMedicationIdAsync_ReturnsCorrectSchedules()
    {
        var med1 = new Medication { Name = "Med1" };
        var med2 = new Medication { Name = "Med2" };
        await _repo.InsertAsync(med1);
        await _repo.InsertAsync(med2);

        await _repo.InsertScheduleAsync(new Schedule { MedicationId = med1.Id, TimeOfDay = new TimeSpan(8, 0, 0) });
        await _repo.InsertScheduleAsync(new Schedule { MedicationId = med1.Id, TimeOfDay = new TimeSpan(20, 0, 0) });
        await _repo.InsertScheduleAsync(new Schedule { MedicationId = med2.Id, TimeOfDay = new TimeSpan(12, 0, 0) });

        var schedules = await _repo.GetSchedulesByMedicationIdAsync(med1.Id);

        Assert.Equal(2, schedules.Count);
        Assert.All(schedules, s => Assert.Equal(med1.Id, s.MedicationId));
    }

    [Fact]
    public async Task GetSchedulesByMedicationIdAsync_OrdersByTimeOfDay()
    {
        var med = new Medication { Name = "Test" };
        await _repo.InsertAsync(med);
        await _repo.InsertScheduleAsync(new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(20, 0, 0) });
        await _repo.InsertScheduleAsync(new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(8, 0, 0) });

        var schedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);

        Assert.Equal(new TimeSpan(8, 0, 0), schedules[0].TimeOfDay);
        Assert.Equal(new TimeSpan(20, 0, 0), schedules[1].TimeOfDay);
    }

    [Fact]
    public async Task GetAllActiveSchedulesAsync_ExcludesInactive()
    {
        var med = new Medication { Name = "Test" };
        await _repo.InsertAsync(med);
        await _repo.InsertScheduleAsync(new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(8, 0, 0) });
        var inactive = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(12, 0, 0), IsActive = false };
        await _db.Database.InsertAsync(inactive);

        var schedules = await _repo.GetAllActiveSchedulesAsync();

        Assert.Single(schedules);
    }

    [Fact]
    public async Task DeleteScheduleAsync_RemovesSchedule()
    {
        var med = new Medication { Name = "Test" };
        await _repo.InsertAsync(med);
        var schedule = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(8, 0, 0) };
        await _repo.InsertScheduleAsync(schedule);

        await _repo.DeleteScheduleAsync(schedule.Id);

        var schedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        Assert.Empty(schedules);
    }
}
