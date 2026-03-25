using MedApp.Data;
using MedApp.Services;
using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;

namespace MedApp;

public partial class App : Application
{
    private readonly NotificationService _notifications;
    private IDispatcherTimer? _overdueTimer;

    // Must match the action IDs registered in MauiProgram.cs
    internal const int ActionTake   = 100;
    internal const int ActionSnooze = 101;
    internal const int ActionSkip   = 102;

    public App(DatabaseContext db, NotificationService notifications)
    {
        InitializeComponent();

        _notifications = notifications;

        // Initialize SQLite tables before any page loads.
        // Task.Run avoids deadlocking the UI-thread SynchronizationContext.
        Task.Run(() => db.InitAsync()).GetAwaiter().GetResult();

        // Listen for notification tap / action-button presses
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        base.OnStart();

        // Ensure notification permission is granted (required on Android 13+)
        Task.Run(async () =>
        {
            if (!await LocalNotificationCenter.Current.AreNotificationsEnabled())
                await LocalNotificationCenter.Current.RequestNotificationPermission();

            await _notifications.ScheduleAllAsync().ConfigureAwait(false);
            await _notifications.DetectMissedDosesAsync().ConfigureAwait(false);
        });

        // Start a foreground timer that checks for overdue doses every 30 seconds
        StartOverdueTimer();
    }

    protected override void OnResume()
    {
        base.OnResume();

        // Re-check missed doses when the app comes back to the foreground
        Task.Run(async () =>
        {
            await _notifications.DetectMissedDosesAsync().ConfigureAwait(false);
        });

        StartOverdueTimer();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        StopOverdueTimer();
    }

    private void StartOverdueTimer()
    {
        if (_overdueTimer is not null) return;

        var dispatcher = Current?.Dispatcher;
        if (dispatcher is null) return;

        _overdueTimer = dispatcher.CreateTimer();
        _overdueTimer.Interval = TimeSpan.FromSeconds(30);
        _overdueTimer.Tick += OnOverdueTimerTick;
        _overdueTimer.Start();
    }

    private void StopOverdueTimer()
    {
        if (_overdueTimer is null) return;
        _overdueTimer.Stop();
        _overdueTimer.Tick -= OnOverdueTimerTick;
        _overdueTimer = null;
    }

    private void OnOverdueTimerTick(object? sender, EventArgs e)
    {
        Task.Run(async () =>
        {
            await _notifications.CheckOverdueDosesAsync().ConfigureAwait(false);
        });
    }

    private void OnNotificationActionTapped(NotificationActionEventArgs e)
    {
        var data = e.Request?.ReturningData;

        // Map integer action IDs to the string keys NotificationService expects
        var actionId = e.ActionId switch
        {
            ActionTake   => "TAKE",
            ActionSnooze => "SNOOZE",
            ActionSkip   => "SKIP",
            _            => "TAKE" // Default tap (no action button) = Take
        };

        Task.Run(() => _notifications.HandleNotificationActionAsync(data, actionId));
    }
}
