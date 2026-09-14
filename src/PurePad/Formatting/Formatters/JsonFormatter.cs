using System.Text.Json;
using System.Text.Json.Nodes;

namespace PurePad.Formatting.Formatters;

/// <summary>
/// Reformats JSON with two-space indentation using <see cref="System.Text.Json"/>.
/// Invalid JSON is reported through <see cref="TextFormatException"/> so the UI can show
/// a friendly message instead of corrupting the document.
/// </summary>
public sealed class JsonFormatter : ITextFormatter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public bool CanFormat => true;

    public string Format(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(text, nodeOptions: null, documentOptions: ParseOptions);
            return node?.ToJsonString(WriteOptions) ?? text;
        }
        catch (JsonException ex)
        {
            throw new TextFormatException($"The document is not valid JSON: {ex.Message}", ex);
        }
    }
}
