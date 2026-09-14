using System.Xml;
using PurePad.Diagnostics;

namespace PurePad.Formatting.Checkers;

/// <summary>
/// Validates XML well-formedness with <see cref="XmlReader"/>, reporting the first
/// structural error with its location. Empty input is treated as clean.
/// </summary>
public sealed class XmlSyntaxChecker : ISyntaxChecker
{
    public bool CanCheck => true;

    public IReadOnlyList<Diagnostic> Check(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<Diagnostic>();
        }

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };

        try
        {
            using var reader = XmlReader.Create(new StringReader(text), settings);
            while (reader.Read())
            {
                // Reading to the end forces validation of the whole document.
            }

            return Array.Empty<Diagnostic>();
        }
        catch (XmlException ex)
        {
            return new[] { new Diagnostic(DiagnosticSeverity.Error, ex.LineNumber, ex.LinePosition, ex.Message) };
        }
    }
}
