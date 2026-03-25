using MedApp.Services;

namespace MedApp.Tests.Helpers;

/// <summary>
/// Test double for IPreferencesProvider backed by a dictionary.
/// </summary>
public class FakePreferencesProvider : IPreferencesProvider
{
    private readonly Dictionary<string, object> _store = new();

    public void Set<T>(string key, T value) where T : notnull => _store[key] = value;

    public T Get<T>(string key, T defaultValue) =>
        _store.TryGetValue(key, out var val) ? (T)val : defaultValue;
}
