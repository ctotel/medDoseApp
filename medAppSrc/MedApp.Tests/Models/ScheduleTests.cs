using MedApp.Models;

namespace MedApp.Tests.Models;

public class ScheduleTests
{
    [Fact]
    public void TimeOfDay_GetSet_RoundTrips()
    {
        var schedule = new Schedule();
        var expected = new TimeSpan(14, 30, 0); // 2:30 PM

        schedule.TimeOfDay = expected;

        Assert.Equal(expected, schedule.TimeOfDay);
        Assert.Equal(expected.Ticks, schedule.TimeOfDayTicks);
    }

    [Fact]
    public void TimeOfDay_SetViaTicks_ReturnsCorrectTimeSpan()
    {
        var schedule = new Schedule();
        var time = new TimeSpan(8, 0, 0); // 8:00 AM

        schedule.TimeOfDayTicks = time.Ticks;

        Assert.Equal(time, schedule.TimeOfDay);
    }

    [Fact]
    public void TimeOfDay_Midnight_HandledCorrectly()
    {
        var schedule = new Schedule();
        schedule.TimeOfDay = TimeSpan.Zero;

        Assert.Equal(TimeSpan.Zero, schedule.TimeOfDay);
        Assert.Equal(0L, schedule.TimeOfDayTicks);
    }

    [Fact]
    public void TimeOfDay_EndOfDay_HandledCorrectly()
    {
        var schedule = new Schedule();
        var endOfDay = new TimeSpan(23, 59, 59);

        schedule.TimeOfDay = endOfDay;

        Assert.Equal(endOfDay, schedule.TimeOfDay);
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var schedule = new Schedule();

        Assert.True(schedule.IsActive);
        Assert.Equal(DateTime.Today, schedule.StartDate);
        Assert.Null(schedule.EndDate);
        Assert.Equal(0, schedule.MedicationId);
    }
}
