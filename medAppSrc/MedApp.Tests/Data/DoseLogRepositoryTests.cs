using MedApp.Data;
using MedApp.Models;

namespace MedApp.Tests.Data;

public class DoseLogRepositoryTests : IDisposable
{
    private readonly DatabaseContext _db;
    private readonly DoseLogRepository _repo;
    private readonly MedicationRepository _medRepo;

    public DoseLogRepositoryTests()
    {
        _db = new DatabaseContext();
        _db.InitAsync().GetAwaiter().GetResult();
        _repo = new DoseLogRepository(_db);
        _medRepo = new MedicationRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Schedule> CreateScheduleAsync()
    {
        var med = new Medication { Name = "TestMed" };
        await _medRepo.InsertAsync(med);
        var schedule = new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(8, 0, 0) };
        await _medRepo.InsertScheduleAsync(schedule);
        return schedule;
    }

    [Fact]
    public async Task InsertAsync_AssignsId()
    {
        var schedule = await CreateScheduleAsync();
        var log = new DoseLog
        {
            ScheduleId = schedule.Id,
            ScheduledAt = DateTime.Today.AddHours(8),
            Status = DoseStatus.Taken,
            TakenAt = DateTime.Now
        };

        await _repo.InsertAsync(log);

        Assert.True(log.Id > 0);
    }

    [Fact]
    public async Task GetByDateAsync_ReturnsLogsForThatDay()
    {
        var schedule = await CreateScheduleAsync();
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);

        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddHours(8), Status = DoseStatus.Taken });
        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddHours(20), Status = DoseStatus.Pending });
        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = yesterday.AddHours(8), Status = DoseStatus.Taken });

        var result = await _repo.GetByDateAsync(today);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByDateAsync_OrdersByScheduledAt()
    {
        var schedule = await CreateScheduleAsync();
        var today = DateTime.Today;

        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddHours(20), Status = DoseStatus.Pending });
        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddHours(8), Status = DoseStatus.Taken });

        var result = await _repo.GetByDateAsync(today);

        Assert.True(result[0].ScheduledAt < result[1].ScheduledAt);
    }

    [Fact]
    public async Task GetByDateRangeAsync_ReturnsLogsInRange()
    {
        var schedule = await CreateScheduleAsync();
        var monday = new DateTime(2026, 3, 16);

        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = monday.AddHours(8), Status = DoseStatus.Taken });
        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = monday.AddDays(1).AddHours(8), Status = DoseStatus.Taken });
        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = monday.AddDays(7).AddHours(8), Status = DoseStatus.Taken });

        var result = await _repo.GetByDateRangeAsync(monday, monday.AddDays(7));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByScheduleAndDateAsync_ReturnsMatchingLog()
    {
        var schedule = await CreateScheduleAsync();
        var today = DateTime.Today;
        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddHours(8), Status = DoseStatus.Taken });

        var result = await _repo.GetByScheduleAndDateAsync(schedule.Id, today);

        Assert.NotNull(result);
        Assert.Equal(DoseStatus.Taken, result.Status);
    }

    [Fact]
    public async Task GetByScheduleAndDateAsync_ReturnsNull_WhenNoMatch()
    {
        var schedule = await CreateScheduleAsync();

        var result = await _repo.GetByScheduleAndDateAsync(schedule.Id, DateTime.Today);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByScheduleIdAsync_ReturnsAllForSchedule()
    {
        var schedule = await CreateScheduleAsync();
        var today = DateTime.Today;

        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddHours(8), Status = DoseStatus.Taken });
        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddDays(-1).AddHours(8), Status = DoseStatus.Missed });

        var result = await _repo.GetByScheduleIdAsync(schedule.Id);

        Assert.Equal(2, result.Count);
        // Should be ordered descending by ScheduledAt
        Assert.True(result[0].ScheduledAt > result[1].ScheduledAt);
    }

    [Fact]
    public async Task GetMissedCountAsync_CountsOnlyMissed()
    {
        var schedule = await CreateScheduleAsync();
        var today = DateTime.Today;

        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddHours(8), Status = DoseStatus.Missed });
        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddHours(12), Status = DoseStatus.Missed });
        await _repo.InsertAsync(new DoseLog { ScheduleId = schedule.Id, ScheduledAt = today.AddHours(20), Status = DoseStatus.Taken });

        var count = await _repo.GetMissedCountAsync(today, today.AddDays(1));

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var schedule = await CreateScheduleAsync();
        var log = new DoseLog
        {
            ScheduleId = schedule.Id,
            ScheduledAt = DateTime.Today.AddHours(8),
            Status = DoseStatus.Pending
        };
        await _repo.InsertAsync(log);

        log.Status = DoseStatus.Taken;
        log.TakenAt = DateTime.Now;
        await _repo.UpdateAsync(log);

        var result = await _repo.GetByScheduleAndDateAsync(schedule.Id, DateTime.Today);
        Assert.Equal(DoseStatus.Taken, result!.Status);
        Assert.NotNull(result.TakenAt);
    }
}
