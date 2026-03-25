using MedApp.Services;

namespace MedApp.Tests.Helpers;

/// <summary>
/// Test double for IClock that allows controlling the current time.
/// </summary>
public class FakeClock : IClock
{
    public FakeClock(DateTime now)
    {
        Now = now;
    }

    public DateTime Now { get; set; }
    public DateTime Today => Now.Date;

    /// <summary>Advance the clock by the specified amount.</summary>
    public void Advance(TimeSpan duration) => Now = Now.Add(duration);
}
