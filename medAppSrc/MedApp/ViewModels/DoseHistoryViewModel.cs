using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MedApp.Data;
using MedApp.Models;

namespace MedApp.ViewModels;

public partial class DoseHistoryViewModel : ObservableObject
{
    private readonly MedicationRepository _medRepo;
    private readonly DoseLogRepository _logRepo;

    public DoseHistoryViewModel(MedicationRepository medRepo, DoseLogRepository logRepo)
    {
        _medRepo = medRepo;
        _logRepo = logRepo;
    }

    public ObservableCollection<HistoryItem> Logs { get; } = [];

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private bool _isRefreshing;

    partial void OnSelectedDateChanged(DateTime value)
    {
        LoadCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var doseLogs = await _logRepo.GetByDateAsync(SelectedDate).ConfigureAwait(false);

            // Build a schedule → medication lookup
            var schedules = await _medRepo.GetAllActiveSchedulesAsync().ConfigureAwait(false);
            var medCache = new Dictionary<int, Medication>();
            var scheduleToMed = new Dictionary<int, Medication>();

            foreach (var s in schedules)
            {
                if (!medCache.TryGetValue(s.MedicationId, out var med))
                {
                    med = await _medRepo.GetByIdAsync(s.MedicationId).ConfigureAwait(false);
                    if (med is not null) medCache[s.MedicationId] = med;
                }
                if (med is not null)
                    scheduleToMed[s.Id] = med;
            }

            var items = doseLogs.Select(log =>
            {
                scheduleToMed.TryGetValue(log.ScheduleId, out var med);
                return new HistoryItem
                {
                    DoseLogId = log.Id,
                    MedicationName = med?.Name ?? "Unknown",
                    Dosage = med?.Dosage ?? "",
                    ScheduledAt = log.ScheduledAt,
                    Status = log.Status
                };
            }).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Logs.Clear();
                foreach (var item in items)
                    Logs.Add(item);
            });
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
