namespace MedApp.Services;

/// <summary>
/// Abstracts platform notification API so NotificationService can be tested
/// without Plugin.LocalNotification.
/// </summary>
public interface INotificationDispatcher
{
    Task ShowAsync(int notificationId, string title, string description,
        DateTime notifyTime, bool repeatsDaily, string returningData);
    void Cancel(int notificationId);
    void CancelAll();
}
