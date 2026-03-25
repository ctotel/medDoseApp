namespace MedApp.Services;

/// <summary>
/// Abstracts DateTime.Now/Today so services can be tested with controlled time.
/// </summary>
public interface IClock
{
    DateTime Now { get; }
    DateTime Today { get; }
}

public class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
    public DateTime Today => DateTime.Today;
}
