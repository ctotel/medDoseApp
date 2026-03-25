using MedApp.Models;

namespace MedApp.Tests.Models;

public class ReportDataTests
{
    [Fact]
    public void AdherencePercent_AllTaken_Returns100()
    {
        var report = new ReportData { TotalDoses = 10, TakenCount = 10 };

        Assert.Equal(100.0, report.AdherencePercent);
    }

    [Fact]
    public void AdherencePercent_NoneTaken_Returns0()
    {
        var report = new ReportData { TotalDoses = 10, TakenCount = 0 };

        Assert.Equal(0.0, report.AdherencePercent);
    }

    [Fact]
    public void AdherencePercent_ZeroTotal_Returns0()
    {
        var report = new ReportData { TotalDoses = 0, TakenCount = 0 };

        Assert.Equal(0.0, report.AdherencePercent);
    }

    [Fact]
    public void AdherencePercent_Partial_RoundsToOneDecimal()
    {
        var report = new ReportData { TotalDoses = 3, TakenCount = 1 };

        Assert.Equal(33.3, report.AdherencePercent);
    }

    [Fact]
    public void AdherencePercent_TwoThirds_RoundsCorrectly()
    {
        var report = new ReportData { TotalDoses = 3, TakenCount = 2 };

        Assert.Equal(66.7, report.AdherencePercent);
    }
}

public class MedicationReportLineTests
{
    [Fact]
    public void Total_SumsTakenMissedSkipped()
    {
        var line = new MedicationReportLine
        {
            Taken = 5,
            Missed = 2,
            Skipped = 1
        };

        Assert.Equal(8, line.Total);
    }

    [Fact]
    public void AdherencePercent_CalculatesFromTotal()
    {
        var line = new MedicationReportLine
        {
            Taken = 7,
            Missed = 2,
            Skipped = 1
        };

        Assert.Equal(70.0, line.AdherencePercent);
    }

    [Fact]
    public void AdherencePercent_ZeroTotal_Returns0()
    {
        var line = new MedicationReportLine();

        Assert.Equal(0.0, line.AdherencePercent);
    }

    [Fact]
    public void AdherencePercent_AllMissed_Returns0()
    {
        var line = new MedicationReportLine { Taken = 0, Missed = 5, Skipped = 0 };

        Assert.Equal(0.0, line.AdherencePercent);
    }
}
