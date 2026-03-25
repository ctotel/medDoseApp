using SQLite;

namespace MedApp.Models;

[Table("medications")]
public class Medication
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string Dosage { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    /// <summary>False after the user deletes the medication; history is preserved.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
