namespace PurePad.Formatting.Formatters;

/// <summary>
/// Null Object formatter: returns text unchanged and reports that it cannot format.
/// Used for languages that have no meaningful reformat, so callers never deal with null.
/// </summary>
public sealed class NullFormatter : ITextFormatter
{
    public static readonly NullFormatter Instance = new();

    private NullFormatter()
    {
    }

    public bool CanFormat => false;

    public string Format(string text) => text;
}
