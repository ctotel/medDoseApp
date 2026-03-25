using SQLite;

namespace MedApp.Models;

/// <summary>
/// Records whether a scheduled dose was taken, missed, snoozed, or skipped.
/// One row is created per scheduled dose per day.
/// </summary>
[Table("dose_logs")]
public class DoseLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull]
    public int ScheduleId { get; set; }

    /// <summary>The exact UTC moment this dose was due.</summary>
    public DateTime ScheduledAt { get; set; }

    public DoseStatus Status { get; set; } = DoseStatus.Pending;

    /// <summary>Set when the user taps "Take Now".</summary>
    public DateTime? TakenAt { get; set; }

    public string Notes { get; set; } = string.Empty;
}
