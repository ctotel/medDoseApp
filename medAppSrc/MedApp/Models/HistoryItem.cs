namespace MedApp.Models;

/// <summary>
/// View-friendly object for the History page, combining DoseLog fields
/// with the medication name for display.
/// </summary>
public class HistoryItem
{
    public int DoseLogId { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string TimeDisplay => ScheduledAt.ToString("h:mm tt");
    public string DateDisplay => ScheduledAt.ToString("ddd, MMM d");
    public DoseStatus Status { get; set; }

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
        DoseStatus.Taken   => Color.FromArgb("#2E7D32"),
        DoseStatus.Missed  => Color.FromArgb("#C62828"),
        DoseStatus.Snoozed => Color.FromArgb("#F57F17"),
        DoseStatus.Skipped => Color.FromArgb("#757575"),
        _                  => Color.FromArgb("#1565C0")
    };
}
