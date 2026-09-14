using System.Text.Json;
using System.Text.Json.Serialization;

namespace PurePad.Configuration;

/// <summary>
/// Stores <see cref="AppSettings"/> as indented JSON under the user's application-data
/// folder. Reads and writes are best-effort: any IO or parse failure falls back to
/// defaults (load) or is ignored (save), so a corrupt file never stops the editor starting.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public JsonSettingsStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>Standard location: <c>%APPDATA%\PurePad\settings.json</c>.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PurePad",
        "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persistence is a convenience; never surface a save failure to the user.
        }
    }
}
