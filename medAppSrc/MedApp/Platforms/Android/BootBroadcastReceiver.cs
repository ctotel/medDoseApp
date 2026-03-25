using Android.App;
using Android.Content;
using MedApp.Services;

namespace MedApp.Platforms.Android;

/// <summary>
/// Reschedules all medication notifications after the device reboots.
/// Requires RECEIVE_BOOT_COMPLETED permission in AndroidManifest.xml.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = false)]
[IntentFilter([Intent.ActionBootCompleted])]
public class BootBroadcastReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != Intent.ActionBootCompleted) return;

        // Resolve the NotificationService from the app's DI container
        var services = IPlatformApplication.Current?.Services;
        var notificationService = services?.GetService<NotificationService>();
        if (notificationService is null) return;

        Task.Run(async () =>
        {
            await notificationService.ScheduleAllAsync().ConfigureAwait(false);
        });
    }
}
