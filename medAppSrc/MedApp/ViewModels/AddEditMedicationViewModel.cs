using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MedApp.Data;
using MedApp.Messages;
using MedApp.Models;
using MedApp.Services;

namespace MedApp.ViewModels;

public partial class AddEditMedicationViewModel : ObservableObject, IQueryAttributable
{
    private readonly MedicationRepository _repo;
    private readonly NotificationService _notifications;
    private int? _editingId;

    public AddEditMedicationViewModel(MedicationRepository repo, NotificationService notifications)
    {
        _repo = repo;
        _notifications = notifications;
        // Start with one default time slot (next whole hour)
        var nextHour = TimeSpan.FromHours(DateTime.Now.Hour + 1);
        if (nextHour.TotalHours >= 24) nextHour = TimeSpan.FromHours(8);
        ScheduleTimes.Add(nextHour);
    }

    // ── Bindable properties ──────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _dosage = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _pageTitle = "Add Medication";

    [ObservableProperty]
    private string? _validationError;

    public ObservableCollection<TimeSpan> ScheduleTimes { get; } = [];

    // ── Query parameter: ?id=123 for edit mode ───────────────────────────────

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && int.TryParse(raw?.ToString(), out var id))
        {
            _editingId = id;
            PageTitle = "Edit Medication";
            LoadExistingCommand.Execute(id);
        }
    }

    [RelayCommand]
    private async Task LoadExistingAsync(int id)
    {
        var med = await _repo.GetByIdAsync(id);
        if (med is null) return;

        Name = med.Name;
        Dosage = med.Dosage;
        Notes = med.Notes;

        ScheduleTimes.Clear();
        var schedules = await _repo.GetSchedulesByMedicationIdAsync(id);
        foreach (var s in schedules)
            ScheduleTimes.Add(s.TimeOfDay);

        if (ScheduleTimes.Count == 0)
            ScheduleTimes.Add(TimeSpan.FromHours(8));
    }

    // ── Schedule time management ─────────────────────────────────────────────

    [RelayCommand]
    private void AddTime()
    {
        var nextHour = TimeSpan.FromHours(DateTime.Now.Hour + 1);
        if (nextHour.TotalHours >= 24) nextHour = TimeSpan.FromHours(8);
        ScheduleTimes.Add(nextHour);
    }

    [RelayCommand]
    private void RemoveTime(int index)
    {
        if (index >= 0 && index < ScheduleTimes.Count)
            ScheduleTimes.RemoveAt(index);
    }

    /// <summary>
    /// Called from the page code-behind when a TimePicker value changes,
    /// because TimePicker doesn't support two-way binding to a collection index.
    /// </summary>
    public void UpdateTime(int index, TimeSpan newTime)
    {
        if (index >= 0 && index < ScheduleTimes.Count)
            ScheduleTimes[index] = newTime;
    }

    // ── Save ─────────────────────────────────────────────────────────────────

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        // Validate
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "Medication name is required.";
            return;
        }
        ValidationError = null;

        int savedId;

        if (_editingId is int existingId)
        {
            // ── Update existing ──────────────────────────────────────────
            var med = await _repo.GetByIdAsync(existingId);
            if (med is null) return;

            med.Name = Name.Trim();
            med.Dosage = Dosage.Trim();
            med.Notes = Notes.Trim();
            await _repo.UpdateAsync(med);

            // Cancel old notifications BEFORE deleting schedules from DB,
            // otherwise the old notification IDs are lost and linger in Android's scheduler.
            await _notifications.CancelForMedicationAsync(existingId);

            // Replace schedules: delete old, insert new
            var oldSchedules = await _repo.GetSchedulesByMedicationIdAsync(existingId);
            foreach (var old in oldSchedules)
                await _repo.DeleteScheduleAsync(old.Id);

            foreach (var time in ScheduleTimes)
            {
                await _repo.InsertScheduleAsync(new Schedule
                {
                    MedicationId = existingId,
                    TimeOfDay = time
                });
            }

            savedId = existingId;
        }
        else
        {
            // ── Insert new ───────────────────────────────────────────────
            var med = new Medication
            {
                Name = Name.Trim(),
                Dosage = Dosage.Trim(),
                Notes = Notes.Trim()
            };
            await _repo.InsertAsync(med);

            foreach (var time in ScheduleTimes)
            {
                await _repo.InsertScheduleAsync(new Schedule
                {
                    MedicationId = med.Id,
                    TimeOfDay = time
                });
            }

            savedId = med.Id;
        }

        // Reschedule notifications for this medication
        if (_editingId.HasValue)
        {
            await _notifications.ScheduleForMedicationAsync(savedId);
        }
        else
        {
            await _notifications.ScheduleAllAsync();
        }

        // Re-check for missed doses in case the user set a time that already passed
        await _notifications.DetectMissedDosesAsync();

        // Notify list pages to refresh (OnAppearing is unreliable on Android Shell pop)
        WeakReferenceMessenger.Default.Send(new MedicationSavedMessage(savedId));

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
