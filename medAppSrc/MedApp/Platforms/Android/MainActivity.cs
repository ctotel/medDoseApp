using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Provider;

namespace MedApp;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        CreateMedicationReminderChannel();
        RequestExactAlarmPermission();
    }

    /// <summary>
    /// Creates the notification channel with high importance and alarm sound
    /// so medication reminders actually ring on the device.
    /// </summary>
    private void CreateMedicationReminderChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var channel = new NotificationChannel(
            "medication_reminders",
            "Medication Reminders",
            NotificationImportance.High)
        {
            Description = "Alerts for scheduled medication doses",
            LockscreenVisibility = NotificationVisibility.Public
        };

        // Use the device default alarm sound so the phone actually rings
        var alarmSound = RingtoneManager.GetDefaultUri(RingtoneType.Alarm);
        var audioAttributes = new AudioAttributes.Builder()
            ?.SetUsage(AudioUsageKind.Alarm)
            ?.SetContentType(AudioContentType.Sonification)
            ?.Build();

        channel.SetSound(alarmSound, audioAttributes);
        channel.EnableVibration(true);
        channel.SetBypassDnd(true);

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(channel);
    }

    /// <summary>
    /// On Android 12+ (API 31+), checks whether the app can schedule exact alarms.
    /// If not, sends the user to the system settings page to grant permission.
    /// </summary>
    private void RequestExactAlarmPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S) return;

        var alarmManager = (AlarmManager?)GetSystemService(AlarmService);
        if (alarmManager is null || alarmManager.CanScheduleExactAlarms()) return;

        var intent = new Intent(Settings.ActionRequestScheduleExactAlarm);
        intent.SetData(Android.Net.Uri.Parse($"package:{PackageName}"));
        StartActivity(intent);
    }
}
