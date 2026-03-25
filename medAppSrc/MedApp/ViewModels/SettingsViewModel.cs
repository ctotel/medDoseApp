using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MedApp.Services;
using Plugin.LocalNotification;

namespace MedApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly NotificationService _notifications;

    public SettingsViewModel(NotificationService notifications)
    {
        _notifications = notifications;

        // Load persisted values
        _snoozeDuration = Preferences.Get("snooze_duration", 10);
        _quietHoursEnabled = Preferences.Get("quiet_enabled", false);
        _quietStart = TimeSpan.FromTicks(Preferences.Get("quiet_start_ticks", TimeSpan.FromHours(22).Ticks));
        _quietEnd = TimeSpan.FromTicks(Preferences.Get("quiet_end_ticks", TimeSpan.FromHours(7).Ticks));
    }

    // ── Snooze ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private int _snoozeDuration;

    partial void OnSnoozeDurationChanged(int value)
    {
        Preferences.Set("snooze_duration", value);
    }

    // ── Quiet hours ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _quietHoursEnabled;

    [ObservableProperty]
    private TimeSpan _quietStart;

    [ObservableProperty]
    private TimeSpan _quietEnd;

    partial void OnQuietHoursEnabledChanged(bool value)
    {
        Preferences.Set("quiet_enabled", value);
        RescheduleCommand.Execute(null);
    }

    partial void OnQuietStartChanged(TimeSpan value)
    {
        Preferences.Set("quiet_start_ticks", value.Ticks);
        if (QuietHoursEnabled) RescheduleCommand.Execute(null);
    }

    partial void OnQuietEndChanged(TimeSpan value)
    {
        Preferences.Set("quiet_end_ticks", value.Ticks);
        if (QuietHoursEnabled) RescheduleCommand.Execute(null);
    }

    // ── Reschedule notifications when quiet hours change ─────────────────────

    [RelayCommand]
    private async Task RescheduleAsync()
    {
        await _notifications.ScheduleAllAsync();
    }

    // ── Notification permission ──────────────────────────────────────────────

    [ObservableProperty]
    private string _permissionStatus = "Checking...";

    [RelayCommand]
    private async Task CheckPermissionAsync()
    {
        var granted = await LocalNotificationCenter.Current.AreNotificationsEnabled();
        PermissionStatus = granted ? "Granted" : "Not granted";
    }

    [RelayCommand]
    private async Task RequestPermissionAsync()
    {
        await LocalNotificationCenter.Current.RequestNotificationPermission();
        await CheckPermissionAsync();
    }
}
