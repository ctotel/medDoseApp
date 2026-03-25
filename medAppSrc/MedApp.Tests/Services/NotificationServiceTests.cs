using MedApp.Data;
using MedApp.Models;
using MedApp.Services;
using MedApp.Tests.Helpers;

namespace MedApp.Tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly DatabaseContext _db;
    private readonly MedicationRepository _medRepo;
    private readonly DoseLogRepository _logRepo;
    private readonly FakeClock _clock;
    private readonly FakeNotificationDispatcher _dispatcher;
    private readonly FakePreferencesProvider _prefs;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _db = new DatabaseContext();
        _db.InitAsync().GetAwaiter().GetResult();
        _medRepo = new MedicationRepository(_db);
        _logRepo = new DoseLogRepository(_db);
        _clock = new FakeClock(new DateTime(2026, 3, 17, 10, 0, 0)); // 10:00 AM
        _dispatcher = new FakeNotificationDispatcher();
        _prefs = new FakePreferencesProvider();
        _sut = new NotificationService(_medRepo, _logRepo, _clock, _dispatcher, _prefs);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(Medication med, Schedule schedule)> CreateMedWithScheduleAsync(
        TimeSpan timeOfDay, string name = "TestMed", string dosage = "10mg")
    {
        var med = new Medication { Name = name, Dosage = dosage };
        await _medRepo.InsertAsync(med);
        var schedule = new Schedule { MedicationId = med.Id, TimeOfDay = timeOfDay };
        await _medRepo.InsertScheduleAsync(schedule);
        return (med, schedule);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CalculateNextFireTime — scheduling decisions
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateNextFireTime_FutureTime_SchedulesToday()
    {
        // Clock is 10:00 AM, schedule is 2:00 PM → should fire today at 2 PM
        var result = _sut.CalculateNextFireTime(new TimeSpan(14, 0, 0));

        Assert.Equal(_clock.Today.AddHours(14), result);
    }

    [Fact]
    public void CalculateNextFireTime_PastTime_BeyondGrace_SchedulesTomorrow()
    {
        // Clock is 10:00 AM, schedule was 7:41 AM (2h19m ago) → tomorrow
        var result = _sut.CalculateNextFireTime(new TimeSpan(7, 41, 0));

        Assert.Equal(_clock.Today.AddDays(1).Add(new TimeSpan(7, 41, 0)), result);
    }

    [Fact]
    public void CalculateNextFireTime_PastTime_WithinGrace_SchedulesNow()
    {
        // Clock is 10:00 AM, schedule is 9:59 AM (1 min ago, within 2-min grace) → fire soon
        var result = _sut.CalculateNextFireTime(new TimeSpan(9, 59, 0));

        // Should be clock.Now + 5 seconds
        Assert.Equal(_clock.Now.AddSeconds(5), result);
    }

    [Fact]
    public void CalculateNextFireTime_ExactlyNow_WithinGrace_SchedulesNow()
    {
        // Clock is 10:00 AM, schedule is 10:00 AM → within grace → fire soon
        var result = _sut.CalculateNextFireTime(new TimeSpan(10, 0, 0));

        Assert.Equal(_clock.Now.AddSeconds(5), result);
    }

    [Fact]
    public void CalculateNextFireTime_JustPastGrace_SchedulesTomorrow()
    {
        // Clock is 10:00 AM, schedule was 9:57:59 AM (2m1s ago) → outside 2-min grace → tomorrow
        _clock.Now = new DateTime(2026, 3, 17, 10, 0, 0);
        var scheduleTime = new TimeSpan(9, 57, 59);
        var result = _sut.CalculateNextFireTime(scheduleTime);

        Assert.Equal(_clock.Today.AddDays(1).Add(scheduleTime), result);
    }

    [Fact]
    public void CalculateNextFireTime_FarFutureTime_SchedulesToday()
    {
        // Clock is 10:00 AM, schedule is 11:59 PM → today
        var result = _sut.CalculateNextFireTime(new TimeSpan(23, 59, 0));

        Assert.Equal(_clock.Today.Add(new TimeSpan(23, 59, 0)), result);
    }

    [Fact]
    public void CalculateNextFireTime_OneSecondInFuture_SchedulesToday()
    {
        // Clock is 10:00:00, schedule is 10:00:01 → still in future → today
        var scheduleTime = new TimeSpan(10, 0, 1);
        var result = _sut.CalculateNextFireTime(scheduleTime);

        Assert.Equal(_clock.Today.Add(scheduleTime), result);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ApplyQuietHours
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ApplyQuietHours_Disabled_NoChange()
    {
        _prefs.Set("quiet_enabled", false);
        var original = new DateTime(2026, 3, 17, 22, 30, 0);

        var result = _sut.ApplyQuietHours(original);

        Assert.Equal(original, result);
    }

    [Fact]
    public void ApplyQuietHours_InQuietWindow_DefersToEnd()
    {
        // Quiet hours 22:00 to 07:00
        _prefs.Set("quiet_enabled", true);
        _prefs.Set("quiet_start_ticks", TimeSpan.FromHours(22).Ticks);
        _prefs.Set("quiet_end_ticks", TimeSpan.FromHours(7).Ticks);

        var notifyTime = new DateTime(2026, 3, 17, 23, 0, 0); // 11 PM, in quiet

        var result = _sut.ApplyQuietHours(notifyTime);

        Assert.Equal(new DateTime(2026, 3, 17, 7, 0, 0), result); // deferred to 7 AM same date
    }

    [Fact]
    public void ApplyQuietHours_OutsideQuietWindow_NoChange()
    {
        _prefs.Set("quiet_enabled", true);
        _prefs.Set("quiet_start_ticks", TimeSpan.FromHours(22).Ticks);
        _prefs.Set("quiet_end_ticks", TimeSpan.FromHours(7).Ticks);

        var notifyTime = new DateTime(2026, 3, 17, 14, 0, 0); // 2 PM, outside

        var result = _sut.ApplyQuietHours(notifyTime);

        Assert.Equal(notifyTime, result);
    }

    [Fact]
    public void ApplyQuietHours_SameDayRange_InQuiet_Defers()
    {
        // Quiet hours 13:00 to 15:00 (non-wrapping)
        _prefs.Set("quiet_enabled", true);
        _prefs.Set("quiet_start_ticks", TimeSpan.FromHours(13).Ticks);
        _prefs.Set("quiet_end_ticks", TimeSpan.FromHours(15).Ticks);

        var notifyTime = new DateTime(2026, 3, 17, 14, 0, 0); // 2 PM, in quiet

        var result = _sut.ApplyQuietHours(notifyTime);

        Assert.Equal(new DateTime(2026, 3, 17, 15, 0, 0), result);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ScheduleAllAsync — alarm scheduling
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleAllAsync_FutureDose_CreatesNotification()
    {
        // Schedule at 2:00 PM, clock is 10:00 AM → should fire today
        await CreateMedWithScheduleAsync(new TimeSpan(14, 0, 0), "Aspirin", "100mg");

        await _sut.ScheduleAllAsync();

        Assert.Single(_dispatcher.Scheduled);
        var n = _dispatcher.Scheduled[0];
        Assert.Equal("Aspirin", n.Title);
        Assert.Equal("Take 100mg", n.Description);
        Assert.Equal(_clock.Today.AddHours(14), n.NotifyTime);
        Assert.True(n.RepeatsDaily);
    }

    [Fact]
    public async Task ScheduleAllAsync_PastDose_SchedulesTomorrow()
    {
        // Schedule at 7:41 AM, clock is 10:00 AM → should fire tomorrow
        await CreateMedWithScheduleAsync(new TimeSpan(7, 41, 0), "Vitamin D");

        await _sut.ScheduleAllAsync();

        Assert.Single(_dispatcher.Scheduled);
        Assert.Equal(_clock.Today.AddDays(1).Add(new TimeSpan(7, 41, 0)),
            _dispatcher.Scheduled[0].NotifyTime);
    }

    [Fact]
    public async Task ScheduleAllAsync_RecentlyPastDose_SchedulesNow()
    {
        // Schedule at 9:59 AM, clock is 10:00 AM → within 2-min grace → fire now
        await CreateMedWithScheduleAsync(new TimeSpan(9, 59, 0));

        await _sut.ScheduleAllAsync();

        Assert.Single(_dispatcher.Scheduled);
        Assert.Equal(_clock.Now.AddSeconds(5), _dispatcher.Scheduled[0].NotifyTime);
    }

    [Fact]
    public async Task ScheduleAllAsync_CancelsAllBefore()
    {
        await CreateMedWithScheduleAsync(new TimeSpan(14, 0, 0));

        await _sut.ScheduleAllAsync();

        Assert.Equal(1, _dispatcher.CancelAllCount);
    }

    [Fact]
    public async Task ScheduleAllAsync_InactiveMed_NoNotification()
    {
        var med = new Medication { Name = "Inactive", IsActive = false };
        await _medRepo.InsertAsync(med);
        await _medRepo.InsertScheduleAsync(new Schedule { MedicationId = med.Id, TimeOfDay = new TimeSpan(14, 0, 0) });

        await _sut.ScheduleAllAsync();

        Assert.Empty(_dispatcher.Scheduled);
    }

    [Fact]
    public async Task ScheduleAllAsync_MultipleMeds_SchedulesAll()
    {
        await CreateMedWithScheduleAsync(new TimeSpan(14, 0, 0), "Med A");
        await CreateMedWithScheduleAsync(new TimeSpan(18, 0, 0), "Med B");
        await CreateMedWithScheduleAsync(new TimeSpan(22, 0, 0), "Med C");

        await _sut.ScheduleAllAsync();

        Assert.Equal(3, _dispatcher.Scheduled.Count);
    }

    [Fact]
    public async Task ScheduleAllAsync_PastEndDate_NoNotification()
    {
        var med = new Medication { Name = "Expired" };
        await _medRepo.InsertAsync(med);
        var schedule = new Schedule
        {
            MedicationId = med.Id,
            TimeOfDay = new TimeSpan(14, 0, 0),
            EndDate = new DateTime(2026, 3, 16) // yesterday
        };
        await _medRepo.InsertScheduleAsync(schedule);

        await _sut.ScheduleAllAsync();

        Assert.Empty(_dispatcher.Scheduled);
    }

    // ════════════════════════════════════════════════════════════════════════
    // DetectMissedDosesAsync — startup missed detection
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DetectMissedDoses_PastUnloggedDose_MarksMissed()
    {
        // Clock 10 AM, dose was at 7:41 AM, not logged → should create Missed log
        await CreateMedWithScheduleAsync(new TimeSpan(7, 41, 0));

        await _sut.DetectMissedDosesAsync();

        var logs = await _logRepo.GetByDateAsync(_clock.Today);
        Assert.Single(logs);
        Assert.Equal(DoseStatus.Missed, logs[0].Status);
        Assert.Equal(_clock.Today.Add(new TimeSpan(7, 41, 0)), logs[0].ScheduledAt);
    }

    [Fact]
    public async Task DetectMissedDoses_FutureDose_NoLog()
    {
        // Clock 10 AM, dose at 2 PM → not yet due → no log
        await CreateMedWithScheduleAsync(new TimeSpan(14, 0, 0));

        await _sut.DetectMissedDosesAsync();

        var logs = await _logRepo.GetByDateAsync(_clock.Today);
        Assert.Empty(logs);
    }

    [Fact]
    public async Task DetectMissedDoses_AlreadyLogged_NoDoubleLog()
    {
        var (_, schedule) = await CreateMedWithScheduleAsync(new TimeSpan(7, 0, 0));
        // Already taken
        await _logRepo.InsertAsync(new DoseLog
        {
            ScheduleId = schedule.Id,
            ScheduledAt = _clock.Today.AddHours(7),
            Status = DoseStatus.Taken,
            TakenAt = _clock.Today.AddHours(7).AddMinutes(5)
        });

        await _sut.DetectMissedDosesAsync();

        var logs = await _logRepo.GetByDateAsync(_clock.Today);
        Assert.Single(logs);
        Assert.Equal(DoseStatus.Taken, logs[0].Status); // stays Taken
    }

    [Fact]
    public async Task DetectMissedDoses_MultiplePastDoses_AllMarkedMissed()
    {
        await CreateMedWithScheduleAsync(new TimeSpan(6, 0, 0), "Morning");
        await CreateMedWithScheduleAsync(new TimeSpan(8, 0, 0), "MidMorning");
        await CreateMedWithScheduleAsync(new TimeSpan(14, 0, 0), "Afternoon");

        await _sut.DetectMissedDosesAsync();

        var logs = await _logRepo.GetByDateAsync(_clock.Today);
        Assert.Equal(2, logs.Count); // 6 AM and 8 AM missed; 2 PM not yet due
        Assert.All(logs, l => Assert.Equal(DoseStatus.Missed, l.Status));
    }

    // ════════════════════════════════════════════════════════════════════════
    // CheckOverdueDosesAsync — foreground timer status updates
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckOverdue_PastDose_NoLog_CreatesMissedLog()
    {
        await CreateMedWithScheduleAsync(new TimeSpan(9, 0, 0));

        await _sut.CheckOverdueDosesAsync();

        var logs = await _logRepo.GetByDateAsync(_clock.Today);
        Assert.Single(logs);
        Assert.Equal(DoseStatus.Missed, logs[0].Status);
    }

    [Fact]
    public async Task CheckOverdue_PastDose_PendingLog_UpdatesToMissed()
    {
        var (_, schedule) = await CreateMedWithScheduleAsync(new TimeSpan(9, 0, 0));
        // Simulate a Pending log that was created before the time passed
        await _logRepo.InsertAsync(new DoseLog
        {
            ScheduleId = schedule.Id,
            ScheduledAt = _clock.Today.AddHours(9),
            Status = DoseStatus.Pending
        });

        await _sut.CheckOverdueDosesAsync();

        var log = await _logRepo.GetByScheduleAndDateAsync(schedule.Id, _clock.Today);
        Assert.Equal(DoseStatus.Missed, log!.Status);
    }

    [Fact]
    public async Task CheckOverdue_PastDose_TakenLog_StaysTaken()
    {
        var (_, schedule) = await CreateMedWithScheduleAsync(new TimeSpan(9, 0, 0));
        await _logRepo.InsertAsync(new DoseLog
        {
            ScheduleId = schedule.Id,
            ScheduledAt = _clock.Today.AddHours(9),
            Status = DoseStatus.Taken,
            TakenAt = _clock.Today.AddHours(9).AddMinutes(2)
        });

        await _sut.CheckOverdueDosesAsync();

        var log = await _logRepo.GetByScheduleAndDateAsync(schedule.Id, _clock.Today);
        Assert.Equal(DoseStatus.Taken, log!.Status);
    }

    [Fact]
    public async Task CheckOverdue_PastDose_SnoozedLog_StaysSnoozed()
    {
        var (_, schedule) = await CreateMedWithScheduleAsync(new TimeSpan(9, 0, 0));
        await _logRepo.InsertAsync(new DoseLog
        {
            ScheduleId = schedule.Id,
            ScheduledAt = _clock.Today.AddHours(9),
            Status = DoseStatus.Snoozed
        });

        await _sut.CheckOverdueDosesAsync();

        var log = await _logRepo.GetByScheduleAndDateAsync(schedule.Id, _clock.Today);
        Assert.Equal(DoseStatus.Snoozed, log!.Status);
    }

    [Fact]
    public async Task CheckOverdue_FutureDose_NoChanges()
    {
        await CreateMedWithScheduleAsync(new TimeSpan(14, 0, 0));

        await _sut.CheckOverdueDosesAsync();

        var logs = await _logRepo.GetByDateAsync(_clock.Today);
        Assert.Empty(logs);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Time progression scenario — simulates user editing a dose time
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Scenario_UserEditsDoseToNearFuture_AlarmFiresAndStatusChanges()
    {
        // 1. User creates a med scheduled at 7:41 AM, it's 10 AM now
        var (med, _) = await CreateMedWithScheduleAsync(new TimeSpan(7, 41, 0), "Metformin");

        // 2. DetectMissedDoses marks the 7:41 AM dose as Missed
        await _sut.DetectMissedDosesAsync();
        var logs = await _logRepo.GetByDateAsync(_clock.Today);
        Assert.Single(logs);
        Assert.Equal(DoseStatus.Missed, logs[0].Status);

        // 3. User edits the schedule time to 10:05 AM (5 minutes from now)
        var oldSchedules = await _medRepo.GetSchedulesByMedicationIdAsync(med.Id);
        foreach (var old in oldSchedules)
            await _medRepo.DeleteScheduleAsync(old.Id);

        var newSchedule = new Schedule
        {
            MedicationId = med.Id,
            TimeOfDay = new TimeSpan(10, 5, 0)
        };
        await _medRepo.InsertScheduleAsync(newSchedule);

        // 4. Reschedule notifications (like AddEditMedicationViewModel.SaveAsync does)
        await _sut.ScheduleForMedicationAsync(med.Id);

        // Notification should be scheduled for today at 10:05 AM (5 min in future)
        Assert.Single(_dispatcher.Scheduled);
        Assert.Equal(_clock.Today.Add(new TimeSpan(10, 5, 0)),
            _dispatcher.Scheduled[0].NotifyTime);

        // 5. Time passes to 10:06 AM
        _clock.Now = new DateTime(2026, 3, 17, 10, 6, 0);

        // 6. Overdue check runs (foreground timer fires)
        await _sut.CheckOverdueDosesAsync();

        // The 10:05 dose should now be Missed (no log existed, time passed)
        var updatedLog = await _logRepo.GetByScheduleAndDateAsync(newSchedule.Id, _clock.Today);
        Assert.NotNull(updatedLog);
        Assert.Equal(DoseStatus.Missed, updatedLog!.Status);
    }

    [Fact]
    public async Task Scenario_UserEditsDoseToJustPast_GracePeriodFires()
    {
        // Clock is 10:00 AM. User edits schedule to 9:59 AM (1 min ago, within grace)
        await CreateMedWithScheduleAsync(new TimeSpan(9, 59, 0), "QuickMed");

        await _sut.ScheduleAllAsync();

        // Should schedule immediately (now + 5 seconds), not push to tomorrow
        Assert.Single(_dispatcher.Scheduled);
        Assert.Equal(_clock.Now.AddSeconds(5), _dispatcher.Scheduled[0].NotifyTime);
        Assert.True(_dispatcher.Scheduled[0].NotifyTime.Date == _clock.Today);
    }

    [Fact]
    public async Task Scenario_AppStartsAt10AM_741AM_Dose_IsMissed_Not_Pending()
    {
        // This reproduces the exact bug from the user report:
        // App starts at 10 AM, 7:41 AM dose should be Missed, not Pending
        _clock.Now = new DateTime(2026, 3, 17, 10, 0, 0);
        await CreateMedWithScheduleAsync(new TimeSpan(7, 41, 0), "Morning Pill");

        // Simulate app startup
        await _sut.ScheduleAllAsync();
        await _sut.DetectMissedDosesAsync();

        var logs = await _logRepo.GetByDateAsync(_clock.Today);
        Assert.Single(logs);
        Assert.Equal(DoseStatus.Missed, logs[0].Status);

        // The notification should be scheduled for tomorrow, not today
        Assert.Single(_dispatcher.Scheduled);
        Assert.Equal(_clock.Today.AddDays(1).Add(new TimeSpan(7, 41, 0)),
            _dispatcher.Scheduled[0].NotifyTime);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ScheduleForMedicationAsync — after edit
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleForMedication_CancelsOldAndSchedulesNew()
    {
        var (med, schedule) = await CreateMedWithScheduleAsync(new TimeSpan(14, 0, 0));

        await _sut.ScheduleForMedicationAsync(med.Id);

        // Should have cancelled the old notification IDs
        Assert.Contains(schedule.Id * 1000, _dispatcher.Cancelled);
        Assert.Contains(schedule.Id * 1000 + 999, _dispatcher.Cancelled);
        // And scheduled a new one
        Assert.Single(_dispatcher.Scheduled);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SnoozeAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SnoozeAsync_SchedulesNonRepeatingNotification()
    {
        var (_, schedule) = await CreateMedWithScheduleAsync(new TimeSpan(10, 0, 0), "SnoozeMed", "5mg");

        await _sut.SnoozeAsync(schedule.Id, 15);

        Assert.Single(_dispatcher.Scheduled);
        var n = _dispatcher.Scheduled[0];
        Assert.Equal(schedule.Id * 1000 + 999, n.NotificationId);
        Assert.StartsWith("Snoozed:", n.Title);
        Assert.Equal(_clock.Now.AddMinutes(15), n.NotifyTime);
        Assert.False(n.RepeatsDaily);
    }

    // ════════════════════════════════════════════════════════════════════════
    // HandleNotificationActionAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task HandleAction_Take_SetsStatusToTaken()
    {
        var (_, schedule) = await CreateMedWithScheduleAsync(new TimeSpan(9, 0, 0));

        await _sut.HandleNotificationActionAsync($"{schedule.Id}", "TAKE");

        var log = await _logRepo.GetByScheduleAndDateAsync(schedule.Id, _clock.Today);
        Assert.NotNull(log);
        Assert.Equal(DoseStatus.Taken, log!.Status);
        Assert.NotNull(log.TakenAt);
    }

    [Fact]
    public async Task HandleAction_Skip_SetsStatusToSkipped()
    {
        var (_, schedule) = await CreateMedWithScheduleAsync(new TimeSpan(9, 0, 0));

        await _sut.HandleNotificationActionAsync($"{schedule.Id}", "SKIP");

        var log = await _logRepo.GetByScheduleAndDateAsync(schedule.Id, _clock.Today);
        Assert.NotNull(log);
        Assert.Equal(DoseStatus.Skipped, log!.Status);
    }

    [Fact]
    public async Task HandleAction_Snooze_SetsStatusAndSchedulesSnooze()
    {
        var (_, schedule) = await CreateMedWithScheduleAsync(new TimeSpan(9, 0, 0));
        _prefs.Set("snooze_duration", 15);

        // Create an existing log
        await _logRepo.InsertAsync(new DoseLog
        {
            ScheduleId = schedule.Id,
            ScheduledAt = _clock.Today.AddHours(9),
            Status = DoseStatus.Pending
        });

        await _sut.HandleNotificationActionAsync($"{schedule.Id}", "SNOOZE");

        var log = await _logRepo.GetByScheduleAndDateAsync(schedule.Id, _clock.Today);
        Assert.Equal(DoseStatus.Snoozed, log!.Status);
        Assert.Single(_dispatcher.Scheduled); // snooze notification
    }

    [Fact]
    public async Task HandleAction_InvalidData_DoesNothing()
    {
        await _sut.HandleNotificationActionAsync("not-a-number", "TAKE");

        var logs = await _logRepo.GetByDateAsync(_clock.Today);
        Assert.Empty(logs);
    }
}
