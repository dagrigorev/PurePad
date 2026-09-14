using PurePad.Formatting;

namespace PurePad.Tools;

/// <summary>
/// An <see cref="ITextFormatter"/> backed by an external command (e.g. <c>prettier</c>,
/// <c>clang-format</c>). The document is piped to the tool's stdin and its stdout is the
/// result. This lets users swap in any formatter without a code change (Open/Closed,
/// Strategy) — the editor only ever sees the <see cref="ITextFormatter"/> interface.
/// </summary>
public sealed class ExternalToolFormatter : ITextFormatter
{
    private readonly ExternalToolRunner _runner;
    private readonly ToolCommand _command;

    public ExternalToolFormatter(ExternalToolRunner runner, ToolCommand command)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public bool CanFormat => _command.IsValid;

    public string Format(string text)
    {
        try
        {
            ToolResult result = _runner.Run(_command.Command, _command.Args, text);
            if (!result.Succeeded)
            {
                string detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"exit code {result.ExitCode}"
                    : result.StandardError.Trim();
                throw new TextFormatException($"'{_command.Command}' could not format the document: {detail}");
            }

            return result.StandardOutput;
        }
        catch (ToolExecutionException ex)
        {
            throw new TextFormatException(ex.Message, ex);
        }
    }
}
