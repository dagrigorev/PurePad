namespace PurePad.Configuration;

/// <summary>
/// Abstraction over loading and persisting <see cref="AppSettings"/>. The view depends on
/// this interface, not the filesystem, so settings can be faked in tests (Dependency
/// Inversion) and the storage backend can change without touching callers.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Load saved settings, returning defaults when none exist or they are unreadable.</summary>
    AppSettings Load();

    /// <summary>Persist settings on a best-effort basis; failures must not disrupt the app.</summary>
    void Save(AppSettings settings);
}
