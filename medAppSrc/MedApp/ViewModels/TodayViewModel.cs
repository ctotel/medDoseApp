using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MedApp.Data;
using MedApp.Messages;
using MedApp.Models;
using MedApp.Services;

namespace MedApp.ViewModels;

public partial class TodayViewModel : ObservableObject
{
    private readonly MedicationRepository _medRepo;
    private readonly DoseLogRepository _logRepo;
    private readonly NotificationService _notifications;
    private IDispatcherTimer? _refreshTimer;

    public TodayViewModel(
        MedicationRepository medRepo,
        DoseLogRepository logRepo,
        NotificationService notifications)
    {
        _medRepo = medRepo;
        _logRepo = logRepo;
        _notifications = notifications;

        // Refresh when a medication is added or edited
        WeakReferenceMessenger.Default.Register<MedicationSavedMessage>(this, (_, _) =>
        {
            LoadCommand.Execute(null);
        });
    }

    public ObservableCollection<TodayDoseItem> Doses { get; } = [];

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>
    /// Start a periodic timer that checks for overdue doses and refreshes the UI.
    /// Call from the page's OnAppearing.
    /// </summary>
    public void StartAutoRefresh(IDispatcher dispatcher)
    {
        if (_refreshTimer is not null) return;

        _refreshTimer = dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(30);
        _refreshTimer.Tick += async (_, _) =>
        {
            await _notifications.CheckOverdueDosesAsync().ConfigureAwait(false);
            await LoadAsync();
        };
        _refreshTimer.Start();
    }

    /// <summary>
    /// Stop the periodic timer. Call from the page's OnDisappearing.
    /// </summary>
    public void StopAutoRefresh()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var schedules = await _medRepo.GetAllActiveSchedulesAsync().ConfigureAwait(false);
            var items = new List<TodayDoseItem>();

            foreach (var schedule in schedules)
            {
                var med = await _medRepo.GetByIdAsync(schedule.MedicationId).ConfigureAwait(false);
                if (med is null || !med.IsActive) continue;

                // Check if EndDate has passed
                if (schedule.EndDate.HasValue && DateTime.Today > schedule.EndDate.Value)
                    continue;

                var log = await _logRepo.GetByScheduleAndDateAsync(schedule.Id, DateTime.Today)
                    .ConfigureAwait(false);

                items.Add(new TodayDoseItem
                {
                    ScheduleId = schedule.Id,
                    MedicationId = med.Id,
                    MedicationName = med.Name,
                    Dosage = med.Dosage,
                    TimeOfDay = schedule.TimeOfDay,
                    Status = log?.Status ?? DoseStatus.Pending
                });
            }

            items.Sort((a, b) => a.TimeOfDay.CompareTo(b.TimeOfDay));

            // Update on UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Doses.Clear();
                foreach (var item in items)
                    Doses.Add(item);

                var taken = items.Count(i => i.Status == DoseStatus.Taken);
                SummaryText = items.Count == 0
                    ? "No medications scheduled today"
                    : $"{taken} of {items.Count} doses taken";
            });
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task TakeAsync(int scheduleId)
    {
        await LogDoseAsync(scheduleId, DoseStatus.Taken);
    }

    [RelayCommand]
    private async Task SnoozeAsync(int scheduleId)
    {
        var snoozeMins = Preferences.Get("snooze_duration", 10);
        await LogDoseAsync(scheduleId, DoseStatus.Snoozed);
        await _notifications.SnoozeAsync(scheduleId, snoozeMins);
    }

    [RelayCommand]
    private async Task SkipAsync(int scheduleId)
    {
        await LogDoseAsync(scheduleId, DoseStatus.Skipped);
    }

    private async Task LogDoseAsync(int scheduleId, DoseStatus status)
    {
        var log = await _logRepo.GetByScheduleAndDateAsync(scheduleId, DateTime.Today)
            .ConfigureAwait(false);

        if (log is not null)
        {
            log.Status = status;
            if (status == DoseStatus.Taken) log.TakenAt = DateTime.Now;
            await _logRepo.UpdateAsync(log).ConfigureAwait(false);
        }
        else
        {
            var schedule = (await _medRepo.GetAllActiveSchedulesAsync().ConfigureAwait(false))
                .FirstOrDefault(s => s.Id == scheduleId);

            await _logRepo.InsertAsync(new DoseLog
            {
                ScheduleId = scheduleId,
                ScheduledAt = DateTime.Today.Add(schedule?.TimeOfDay ?? TimeSpan.Zero),
                Status = status,
                TakenAt = status == DoseStatus.Taken ? DateTime.Now : null
            }).ConfigureAwait(false);
        }

        await LoadAsync();
    }
}
