using System.Text.Json;
using System.Text.Json.Serialization;

namespace PurePad.Tools;

/// <summary>One external command: the executable, its arguments and (for checkers) an output pattern.</summary>
public sealed class ToolCommand
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public List<string> Args { get; set; } = new();

    /// <summary>
    /// Regex with named groups <c>line</c>, <c>col</c>, <c>severity</c> and <c>message</c>
    /// used to turn checker output into diagnostics. Optional; a default is used if absent.
    /// </summary>
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    public bool IsValid => !string.IsNullOrWhiteSpace(Command);
}

/// <summary>The external format and/or check command configured for a single language.</summary>
public sealed class LanguageToolSettings
{
    [JsonPropertyName("format")]
    public ToolCommand? Format { get; set; }

    [JsonPropertyName("check")]
    public ToolCommand? Check { get; set; }
}

/// <summary>
/// The parsed <c>tools.json</c>: a map of language id (e.g. "json") to the external tools
/// configured for it. Loaded from the user profile / app directory; a missing or invalid
/// file yields an empty configuration so built-in behaviour is always the fallback.
/// </summary>
public sealed class ToolConfiguration
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public ToolConfiguration(IReadOnlyDictionary<string, LanguageToolSettings> languages)
    {
        Languages = languages;
    }

    public IReadOnlyDictionary<string, LanguageToolSettings> Languages { get; }

    public static ToolConfiguration Empty { get; } =
        new(new Dictionary<string, LanguageToolSettings>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Load configuration from <paramref name="path"/>, or <see cref="Empty"/> if it is absent/invalid.</summary>
    public static ToolConfiguration LoadOrEmpty(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        try
        {
            string json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, LanguageToolSettings>>(json, Options);
            if (parsed is null)
            {
                return Empty;
            }

            return new ToolConfiguration(new Dictionary<string, LanguageToolSettings>(parsed, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Empty;
        }
    }

    /// <summary>Standard location of the config file under the user's application-data folder.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PurePad",
        "tools.json");
}
