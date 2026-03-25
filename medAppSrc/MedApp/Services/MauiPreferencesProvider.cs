namespace MedApp.Services;

/// <summary>
/// Production implementation that delegates to MAUI Preferences.
/// Handles the type-specific overloads that MAUI Preferences requires.
/// </summary>
public class MauiPreferencesProvider : IPreferencesProvider
{
    public T Get<T>(string key, T defaultValue)
    {
        return defaultValue switch
        {
            string s => (T)(object)Preferences.Get(key, s),
            int i => (T)(object)Preferences.Get(key, i),
            long l => (T)(object)Preferences.Get(key, l),
            bool b => (T)(object)Preferences.Get(key, b),
            double d => (T)(object)Preferences.Get(key, d),
            float f => (T)(object)Preferences.Get(key, f),
            _ => defaultValue
        };
    }
}
