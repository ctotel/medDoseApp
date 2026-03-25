using MedApp.Services;

namespace MedApp.Tests.Helpers;

/// <summary>
/// Test double for INotificationDispatcher that records all calls.
/// </summary>
public class FakeNotificationDispatcher : INotificationDispatcher
{
    public List<ScheduledNotification> Scheduled { get; } = [];
    public List<int> Cancelled { get; } = [];
    public int CancelAllCount { get; private set; }

    public Task ShowAsync(int notificationId, string title, string description,
        DateTime notifyTime, bool repeatsDaily, string returningData)
    {
        Scheduled.Add(new ScheduledNotification
        {
            NotificationId = notificationId,
            Title = title,
            Description = description,
            NotifyTime = notifyTime,
            RepeatsDaily = repeatsDaily,
            ReturningData = returningData
        });
        return Task.CompletedTask;
    }

    public void Cancel(int notificationId) => Cancelled.Add(notificationId);
    public void CancelAll() => CancelAllCount++;

    public record ScheduledNotification
    {
        public int NotificationId { get; init; }
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public DateTime NotifyTime { get; init; }
        public bool RepeatsDaily { get; init; }
        public string ReturningData { get; init; } = "";
    }
}
