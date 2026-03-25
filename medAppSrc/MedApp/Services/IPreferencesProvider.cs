namespace MedApp.Services;

/// <summary>
/// Abstracts MAUI Preferences so services can be tested without the platform.
/// </summary>
public interface IPreferencesProvider
{
    T Get<T>(string key, T defaultValue);
}
