using PurePad.Formatting;

namespace PurePad.Tools;

/// <summary>
/// Builds and holds the external formatter/checker (if any) configured for each language.
/// The editor consults this first and falls back to the built-in strategy when nothing is
/// configured, so external tools override the defaults without replacing them.
/// </summary>
public sealed class ExternalToolCatalog
{
    private readonly Dictionary<string, ITextFormatter> _formatters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISyntaxChecker> _checkers = new(StringComparer.OrdinalIgnoreCase);

    public ExternalToolCatalog(ToolConfiguration configuration, ExternalToolRunner runner)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(runner);

        foreach ((string languageId, LanguageToolSettings settings) in configuration.Languages)
        {
            if (settings.Format is { IsValid: true } formatCommand)
            {
                _formatters[languageId] = new ExternalToolFormatter(runner, formatCommand);
            }

            if (settings.Check is { IsValid: true } checkCommand)
            {
                _checkers[languageId] = new ExternalToolChecker(runner, checkCommand);
            }
        }
    }

    public bool TryGetFormatter(string languageId, out ITextFormatter formatter) =>
        _formatters.TryGetValue(languageId, out formatter!);

    public bool TryGetChecker(string languageId, out ISyntaxChecker checker) =>
        _checkers.TryGetValue(languageId, out checker!);

    /// <summary>A catalog with no external tools configured.</summary>
    public static ExternalToolCatalog Empty { get; } =
        new(ToolConfiguration.Empty, new ExternalToolRunner());
}
