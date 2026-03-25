using MedApp.Data;
using MedApp.Models;

namespace MedApp.Tests.Data;

/// <summary>
/// Verifies that medication names are correctly persisted and retrieved
/// through insert, update, and list-reload flows.
/// </summary>
public class MedicationNamePersistenceTests : IDisposable
{
    private readonly DatabaseContext _db;
    private readonly MedicationRepository _repo;

    public MedicationNamePersistenceTests()
    {
        _db = new DatabaseContext();
        _db.InitAsync().GetAwaiter().GetResult();
        _repo = new MedicationRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task InsertedMedicationName_AppearsInGetAllAsync()
    {
        var med = new Medication { Name = "Aspirin", Dosage = "100mg" };
        await _repo.InsertAsync(med);

        var all = await _repo.GetAllAsync();

        var found = Assert.Single(all);
        Assert.Equal("Aspirin", found.Name);
        Assert.Equal("100mg", found.Dosage);
    }

    [Fact]
    public async Task UpdatedMedicationName_AppearsInGetAllAsync()
    {
        // Simulate: user adds medication, then edits the name
        var med = new Medication { Name = "OldName", Dosage = "50mg" };
        await _repo.InsertAsync(med);

        // Edit flow: fetch by id, update name, save
        var toEdit = await _repo.GetByIdAsync(med.Id);
        Assert.NotNull(toEdit);
        toEdit!.Name = "NewName";
        toEdit.Dosage = "200mg";
        await _repo.UpdateAsync(toEdit);

        // List reload (same query MedicationListViewModel.LoadAsync uses)
        var all = await _repo.GetAllAsync();

        var found = Assert.Single(all);
        Assert.Equal("NewName", found.Name);
        Assert.Equal("200mg", found.Dosage);
    }

    [Fact]
    public async Task MultipleMedications_AllNamesAppearInGetAllAsync()
    {
        await _repo.InsertAsync(new Medication { Name = "Med A", Dosage = "10mg" });
        await _repo.InsertAsync(new Medication { Name = "Med B", Dosage = "20mg" });
        await _repo.InsertAsync(new Medication { Name = "Med C", Dosage = "30mg" });

        var all = await _repo.GetAllAsync();

        Assert.Equal(3, all.Count);
        // GetAllAsync orders by Name
        Assert.Equal("Med A", all[0].Name);
        Assert.Equal("Med B", all[1].Name);
        Assert.Equal("Med C", all[2].Name);
    }

    [Fact]
    public async Task SaveWithSchedules_NameAndTimesPersistedCorrectly()
    {
        // Full add-medication flow: insert med, insert schedules, reload
        var med = new Medication { Name = "Metformin", Dosage = "500mg" };
        await _repo.InsertAsync(med);

        await _repo.InsertScheduleAsync(new Schedule
        {
            MedicationId = med.Id,
            TimeOfDay = new TimeSpan(8, 0, 0)
        });
        await _repo.InsertScheduleAsync(new Schedule
        {
            MedicationId = med.Id,
            TimeOfDay = new TimeSpan(20, 0, 0)
        });

        // Reload like the list page does
        var all = await _repo.GetAllAsync();
        var found = Assert.Single(all);
        Assert.Equal("Metformin", found.Name);

        var schedules = await _repo.GetSchedulesByMedicationIdAsync(found.Id);
        Assert.Equal(2, schedules.Count);
    }

    [Fact]
    public async Task EditWithScheduleReplacement_NameAndTimesUpdated()
    {
        // Add medication with one schedule
        var med = new Medication { Name = "OldPill", Dosage = "25mg" };
        await _repo.InsertAsync(med);
        var oldSchedule = new Schedule
        {
            MedicationId = med.Id,
            TimeOfDay = new TimeSpan(9, 0, 0)
        };
        await _repo.InsertScheduleAsync(oldSchedule);

        // Edit flow: update name, replace schedules
        var toEdit = await _repo.GetByIdAsync(med.Id);
        toEdit!.Name = "NewPill";
        toEdit.Dosage = "50mg";
        await _repo.UpdateAsync(toEdit);

        // Delete old schedules, insert new ones (same as SaveAsync edit path)
        var oldSchedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
        foreach (var old in oldSchedules)
            await _repo.DeleteScheduleAsync(old.Id);

        await _repo.InsertScheduleAsync(new Schedule
        {
            MedicationId = med.Id,
            TimeOfDay = new TimeSpan(7, 30, 0)
        });

        // Reload
        var all = await _repo.GetAllAsync();
        var found = Assert.Single(all);
        Assert.Equal("NewPill", found.Name);
        Assert.Equal("50mg", found.Dosage);

        var schedules = await _repo.GetSchedulesByMedicationIdAsync(found.Id);
        Assert.Single(schedules);
        Assert.Equal(new TimeSpan(7, 30, 0), schedules[0].TimeOfDay);
    }
}
