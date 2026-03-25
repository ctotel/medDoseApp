using MedApp.Models;

namespace MedApp.Tests.Models;

public class MedicationTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var med = new Medication();

        Assert.Equal(string.Empty, med.Name);
        Assert.Equal(string.Empty, med.Dosage);
        Assert.Equal(string.Empty, med.Notes);
        Assert.True(med.IsActive);
    }

    [Fact]
    public void CreatedAt_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;
        var med = new Medication();
        var after = DateTime.UtcNow;

        Assert.InRange(med.CreatedAt, before, after);
    }
}

public class DoseLogTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var log = new DoseLog();

        Assert.Equal(DoseStatus.Pending, log.Status);
        Assert.Null(log.TakenAt);
        Assert.Equal(string.Empty, log.Notes);
    }

    [Fact]
    public void DoseStatus_Enum_HasExpectedValues()
    {
        Assert.Equal(0, (int)DoseStatus.Pending);
        Assert.Equal(1, (int)DoseStatus.Taken);
        Assert.Equal(2, (int)DoseStatus.Missed);
        Assert.Equal(3, (int)DoseStatus.Snoozed);
        Assert.Equal(4, (int)DoseStatus.Skipped);
    }
}
