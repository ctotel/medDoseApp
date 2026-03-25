using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

namespace MedApp.Services;

/// <summary>
/// Production implementation that delegates to Plugin.LocalNotification.
/// </summary>
public class MauiNotificationDispatcher : INotificationDispatcher
{
    public async Task ShowAsync(int notificationId, string title, string description,
        DateTime notifyTime, bool repeatsDaily, string returningData)
    {
        var request = new NotificationRequest
        {
            NotificationId = notificationId,
            Title = title,
            Description = description,
            CategoryType = NotificationCategoryType.Alarm,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime,
                RepeatType = repeatsDaily ? NotificationRepeat.Daily : NotificationRepeat.No
            },
            Android = new AndroidOptions
            {
                ChannelId = "medication_reminders",
                Priority = AndroidPriority.High,
                AutoCancel = false,
                Ongoing = false
            },
            ReturningData = returningData
        };

        await LocalNotificationCenter.Current.Show(request).ConfigureAwait(false);
    }

    public void Cancel(int notificationId)
    {
        LocalNotificationCenter.Current.Cancel(notificationId);
    }

    public void CancelAll()
    {
        LocalNotificationCenter.Current.CancelAll();
    }
}
