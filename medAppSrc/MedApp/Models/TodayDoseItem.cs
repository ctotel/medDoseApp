namespace MedApp.Models;

/// <summary>
/// View-friendly object combining Schedule + Medication name + current DoseLog status
/// for display on the Today screen.
/// </summary>
public class TodayDoseItem
{
    public int ScheduleId { get; set; }
    public int MedicationId { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public TimeSpan TimeOfDay { get; set; }
    public string TimeDisplay => DateTime.Today.Add(TimeOfDay).ToString("h:mm tt");
    public DoseStatus Status { get; set; } = DoseStatus.Pending;

    public string StatusText => Status switch
    {
        DoseStatus.Taken   => "Taken",
        DoseStatus.Missed  => "Missed",
        DoseStatus.Snoozed => "Snoozed",
        DoseStatus.Skipped => "Skipped",
        _                  => "Pending"
    };

    public Color StatusColor => Status switch
    {
        DoseStatus.Taken   => Color.FromArgb("#2E7D32"), // Success
        DoseStatus.Missed  => Color.FromArgb("#C62828"), // Danger
        DoseStatus.Snoozed => Color.FromArgb("#F57F17"), // Warning
        DoseStatus.Skipped => Color.FromArgb("#757575"), // Muted
        _                  => Color.FromArgb("#1565C0")  // Primary
    };
}
