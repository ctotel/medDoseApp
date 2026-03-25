using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MedApp.Data;
using MedApp.Messages;
using MedApp.Models;
using MedApp.Services;
using MedApp.Views;

namespace MedApp.ViewModels;

public partial class MedicationListViewModel : ObservableObject
{
    private readonly MedicationRepository _repo;
    private readonly NotificationService _notifications;

    public MedicationListViewModel(MedicationRepository repo, NotificationService notifications)
    {
        _repo = repo;
        _notifications = notifications;

        // Refresh the list when a medication is added or edited
        WeakReferenceMessenger.Default.Register<MedicationSavedMessage>(this, (_, _) =>
        {
            LoadCommand.Execute(null);
        });
    }

    public ObservableCollection<MedicationSummary> Medications { get; } = [];

    [ObservableProperty]
    private bool _isRefreshing;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var meds = await _repo.GetAllAsync();
            Medications.Clear();

            foreach (var med in meds)
            {
                var schedules = await _repo.GetSchedulesByMedicationIdAsync(med.Id);
                var times = schedules
                    .Select(s => s.TimeOfDay)
                    .OrderBy(t => t)
                    .Select(t => DateTime.Today.Add(t).ToString("h:mm tt"))
                    .ToList();

                Medications.Add(new MedicationSummary
                {
                    Id = med.Id,
                    Name = med.Name,
                    Dosage = med.Dosage,
                    TimesDisplay = times.Count > 0
                        ? string.Join(", ", times)
                        : "No schedule"
                });
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddEditMedicationPage));
    }

    [RelayCommand]
    private async Task EditAsync(int medicationId)
    {
        await Shell.Current.GoToAsync($"{nameof(AddEditMedicationPage)}?id={medicationId}");
    }

    [RelayCommand]
    private async Task DeleteAsync(int medicationId)
    {
        var schedules = await _repo.GetSchedulesByMedicationIdAsync(medicationId);
        if (schedules.Count > 0)
        {
            await Shell.Current.DisplayAlert(
                "Cannot Delete",
                "This medication still has scheduled times. Edit the medication and remove all schedule times before deleting.",
                "OK");
            return;
        }

        var confirm = await Shell.Current.DisplayAlert(
            "Delete Medication",
            "This medication will be removed. Dose history is preserved. Continue?",
            "Delete", "Cancel");

        if (!confirm) return;

        await _notifications.CancelForMedicationAsync(medicationId);
        await _repo.SoftDeleteAsync(medicationId);
        await LoadAsync();
    }
}

/// <summary>
/// Lightweight view-object for the medication list — avoids exposing
/// the full model and pre-formats the schedule times for display.
/// </summary>
public class MedicationSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string TimesDisplay { get; set; } = string.Empty;
}
