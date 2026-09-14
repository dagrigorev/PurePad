using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PurePad.Formatting.Formatters;

/// <summary>
/// Reformats XML with two-space indentation via <see cref="XDocument"/>. Malformed
/// markup is surfaced as a <see cref="TextFormatException"/> so the document is never
/// silently damaged.
/// </summary>
public sealed class XmlFormatter : ITextFormatter
{
    public bool CanFormat => true;

    public string Format(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        try
        {
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = document.Declaration is null,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            };

            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, settings))
            {
                document.Save(writer);
            }

            return builder.ToString();
        }
        catch (XmlException ex)
        {
            throw new TextFormatException($"The document is not valid XML: {ex.Message}", ex);
        }
    }
}
