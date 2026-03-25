namespace MedApp.Models;

public class ReportData
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsWeekly { get; set; }
    public int TotalDoses { get; set; }
    public int TakenCount { get; set; }
    public int MissedCount { get; set; }
    public int SkippedCount { get; set; }

    public double AdherencePercent => TotalDoses == 0 ? 0 : Math.Round(100.0 * TakenCount / TotalDoses, 1);

    public List<MedicationReportLine> Medications { get; set; } = [];
}

public class MedicationReportLine
{
    public string MedicationName { get; set; } = string.Empty;
    public int Taken { get; set; }
    public int Missed { get; set; }
    public int Skipped { get; set; }
    public int Total => Taken + Missed + Skipped;

    public double AdherencePercent => Total == 0 ? 0 : Math.Round(100.0 * Taken / Total, 1);
}
