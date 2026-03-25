using MedApp.Data;
using MedApp.Models;

namespace MedApp.Services;

/// <summary>
/// Schedules, cancels, and snoozes local notifications for medication doses.
/// Uses injected abstractions so the scheduling logic can be unit-tested.
/// </summary>
public class NotificationService
{
    private readonly MedicationRepository _medRepo;
    private readonly DoseLogRepository _logRepo;
    private readonly IClock _clock;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IPreferencesProvider _prefs;

    // Notification IDs are built from ScheduleId * 1000 + offset to avoid collisions.
    private const int IdMultiplier = 1000;

    // If a scheduled time is within this window in the past, fire immediately
    // instead of pushing to tomorrow. Covers the save-then-schedule delay.
    private static readonly TimeSpan ScheduleGracePeriod = TimeSpan.FromMinutes(2);

    public NotificationService(
        MedicationRepository medRepo,
        DoseLogRepository logRepo,
        IClock clock,
        INotificationDispatcher dispatcher,
        IPreferencesProvider prefs)
    {
        _medRepo = medRepo;
        _logRepo = logRepo;
        _clock = clock;
        _dispatcher = dispatcher;
        _prefs = prefs;
    }

    /// <summary>
    /// Reschedule all active medication notifications.
    /// Called on app launch and after device reboot.
    /// </summary>
    public async Task ScheduleAllAsync()
    {
        _dispatcher.CancelAll();

        var schedules = await _medRepo.GetAllActiveSchedulesAsync().ConfigureAwait(false);
        foreach (var schedule in schedules)
        {
            var med = await _medRepo.GetByIdAsync(schedule.MedicationId).ConfigureAwait(false);
            if (med is null || !med.IsActive) continue;

            await ScheduleSingleAsync(med, schedule).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Schedule notifications for one medication (called after add/edit).
    /// </summary>
    public async Task ScheduleForMedicationAsync(int medicationId)
    {
        var med = await _medRepo.GetByIdAsync(medicationId).ConfigureAwait(false);
        if (med is null || !med.IsActive) return;

        var schedules = await _medRepo.GetSchedulesByMedicationIdAsync(medicationId).ConfigureAwait(false);
        foreach (var schedule in schedules)
        {
            CancelForSchedule(schedule.Id);
            await ScheduleSingleAsync(med, schedule).ConfigureAwait(false);
        }
    }

    /// <summary>Cancel all notifications for a medication (called on delete).</summary>
    public async Task CancelForMedicationAsync(int medicationId)
    {
        var schedules = await _medRepo.GetSchedulesByMedicationIdAsync(medicationId).ConfigureAwait(false);
        foreach (var schedule in schedules)
            CancelForSchedule(schedule.Id);
    }

    /// <summary>Schedule a one-off snooze notification N minutes from now.</summary>
    public async Task SnoozeAsync(int scheduleId, int minutes)
    {
        var schedule = (await _medRepo.GetAllActiveSchedulesAsync().ConfigureAwait(false))
            .FirstOrDefault(s => s.Id == scheduleId);
        if (schedule is null) return;

        var med = await _medRepo.GetByIdAsync(schedule.MedicationId).ConfigureAwait(false);
        if (med is null) return;

        var notifyTime = _clock.Now.AddMinutes(minutes);
        var notificationId = schedule.Id * IdMultiplier + 999; // snooze uses +999 offset

        await _dispatcher.ShowAsync(
            notificationId,
            $"Snoozed: {med.Name}",
            $"Time to take {med.Name} ({med.Dosage})",
            notifyTime,
            repeatsDaily: false,
            returningData: $"{schedule.Id}"
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Scan today's schedules and mark any past unlogged doses as Missed.
    /// Called on app launch and after medication edits.
    /// </summary>
    public async Task DetectMissedDosesAsync()
    {
        var now = _clock.Now;
        var today = _clock.Today;
        var schedules = await _medRepo.GetAllActiveSchedulesAsync().ConfigureAwait(false);

        foreach (var schedule in schedules)
        {
            var doseTime = today.Add(schedule.TimeOfDay);
            if (doseTime >= now) continue; // not yet due

            var existing = await _logRepo.GetByScheduleAndDateAsync(schedule.Id, today)
                .ConfigureAwait(false);
            if (existing is not null) continue; // already logged

            await _logRepo.InsertAsync(new DoseLog
            {
                ScheduleId = schedule.Id,
                ScheduledAt = doseTime,
                Status = DoseStatus.Missed
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Check for overdue doses while the app is in the foreground.
    /// Marks any Pending doses whose time has passed as Missed.
    /// Called periodically by the UI timer.
    /// </summary>
    public async Task CheckOverdueDosesAsync()
    {
        var now = _clock.Now;
        var today = _clock.Today;
        var schedules = await _medRepo.GetAllActiveSchedulesAsync().ConfigureAwait(false);

        foreach (var schedule in schedules)
        {
            var doseTime = today.Add(schedule.TimeOfDay);
            if (doseTime >= now) continue; // not yet due

            var existing = await _logRepo.GetByScheduleAndDateAsync(schedule.Id, today)
                .ConfigureAwait(false);

            if (existing is null)
            {
                // No log at all — create as Missed
                await _logRepo.InsertAsync(new DoseLog
                {
                    ScheduleId = schedule.Id,
                    ScheduledAt = doseTime,
                    Status = DoseStatus.Missed
                }).ConfigureAwait(false);
            }
            else if (existing.Status == DoseStatus.Pending)
            {
                // Has a Pending log but the time has passed — mark Missed
                existing.Status = DoseStatus.Missed;
                await _logRepo.UpdateAsync(existing).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Handle a notification tap action. Called from App.xaml.cs.
    /// </summary>
    public async Task HandleNotificationActionAsync(string? returningData, string actionId)
    {
        if (!int.TryParse(returningData, out var scheduleId)) return;

        var today = _clock.Today;
        var log = await _logRepo.GetByScheduleAndDateAsync(scheduleId, today)
            .ConfigureAwait(false);

        if (actionId == "TAKE" || string.IsNullOrEmpty(actionId))
        {
            if (log is not null)
            {
                log.Status = DoseStatus.Taken;
                log.TakenAt = _clock.Now;
                await _logRepo.UpdateAsync(log).ConfigureAwait(false);
            }
            else
            {
                await _logRepo.InsertAsync(new DoseLog
                {
                    ScheduleId = scheduleId,
                    ScheduledAt = today.Add(
                        (await _medRepo.GetAllActiveSchedulesAsync().ConfigureAwait(false))
                        .FirstOrDefault(s => s.Id == scheduleId)?.TimeOfDay ?? TimeSpan.Zero),
                    Status = DoseStatus.Taken,
                    TakenAt = _clock.Now
                }).ConfigureAwait(false);
            }
        }
        else if (actionId == "SNOOZE")
        {
            var snoozeMins = _prefs.Get("snooze_duration", 10);
            if (log is not null)
            {
                log.Status = DoseStatus.Snoozed;
                await _logRepo.UpdateAsync(log).ConfigureAwait(false);
            }
            await SnoozeAsync(scheduleId, snoozeMins).ConfigureAwait(false);
        }
        else if (actionId == "SKIP")
        {
            if (log is not null)
            {
                log.Status = DoseStatus.Skipped;
                await _logRepo.UpdateAsync(log).ConfigureAwait(false);
            }
            else
            {
                await _logRepo.InsertAsync(new DoseLog
                {
                    ScheduleId = scheduleId,
                    ScheduledAt = today,
                    Status = DoseStatus.Skipped
                }).ConfigureAwait(false);
            }
        }
    }

    // ── Internal for testing ─────────────────────────────────────────────────

    /// <summary>
    /// Calculates the next notification fire time for a schedule.
    /// Visible for testing.
    /// </summary>
    internal DateTime CalculateNextFireTime(TimeSpan timeOfDay)
    {
        var now = _clock.Now;
        var today = _clock.Today;
        var notifyTime = today.Add(timeOfDay);

        if (notifyTime <= now)
        {
            if (now - notifyTime <= ScheduleGracePeriod)
            {
                // Within grace period — fire a few seconds from now
                notifyTime = now.AddSeconds(5);
            }
            else
            {
                // Past the grace period — push to tomorrow
                notifyTime = today.AddDays(1).Add(timeOfDay);
            }
        }

        return notifyTime;
    }

    /// <summary>
    /// Adjusts a notification time for quiet hours if enabled.
    /// Visible for testing.
    /// </summary>
    internal DateTime ApplyQuietHours(DateTime notifyTime)
    {
        var quietStart = _prefs.Get("quiet_start_ticks", -1L);
        var quietEnd = _prefs.Get("quiet_end_ticks", -1L);
        if (quietStart < 0 || quietEnd < 0 || !_prefs.Get("quiet_enabled", false))
            return notifyTime;

        var qs = TimeSpan.FromTicks(quietStart);
        var qe = TimeSpan.FromTicks(quietEnd);
        var tod = notifyTime.TimeOfDay;
        bool inQuiet = qs < qe
            ? tod >= qs && tod < qe
            : tod >= qs || tod < qe; // wraps midnight

        if (inQuiet)
            notifyTime = notifyTime.Date.Add(qe);

        return notifyTime;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task ScheduleSingleAsync(Medication med, Schedule schedule)
    {
        var notifyTime = CalculateNextFireTime(schedule.TimeOfDay);

        // Respect EndDate
        if (schedule.EndDate.HasValue && notifyTime > schedule.EndDate.Value)
            return;

        notifyTime = ApplyQuietHours(notifyTime);

        var notificationId = schedule.Id * IdMultiplier;
        var description = string.IsNullOrWhiteSpace(med.Dosage)
            ? "Time to take your medication"
            : $"Take {med.Dosage}";

        await _dispatcher.ShowAsync(
            notificationId,
            med.Name,
            description,
            notifyTime,
            repeatsDaily: true,
            returningData: $"{schedule.Id}"
        ).ConfigureAwait(false);
    }

    private void CancelForSchedule(int scheduleId)
    {
        var id = scheduleId * IdMultiplier;
        _dispatcher.Cancel(id);
        _dispatcher.Cancel(scheduleId * IdMultiplier + 999); // snooze
    }
}
