using SQLite;

namespace MedApp.Models;

/// <summary>
/// One daily alarm time for a medication.
/// A medication can have many schedules (e.g. 8 AM and 8 PM).
/// </summary>
[Table("schedules")]
public class Schedule
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull]
    public int MedicationId { get; set; }

    /// <summary>
    /// Stored as ticks so SQLite can handle it as a plain long.
    /// Use the <see cref="TimeOfDay"/> property for all application logic.
    /// </summary>
    public long TimeOfDayTicks { get; set; }

    [Ignore]
    public TimeSpan TimeOfDay
    {
        get => TimeSpan.FromTicks(TimeOfDayTicks);
        set => TimeOfDayTicks = value.Ticks;
    }

    public bool IsActive { get; set; } = true;

    public DateTime StartDate { get; set; } = DateTime.Today;

    /// <summary>Null means the schedule repeats indefinitely.</summary>
    public DateTime? EndDate { get; set; }
}
