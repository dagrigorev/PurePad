using PurePad.Formatting;
using PurePad.Formatting.Formatters;

namespace PurePad.Tests.Formatters;

public sealed class JsonFormatterTests
{
    private readonly JsonFormatter _formatter = new();

    [Fact]
    public void CanFormat_is_true()
    {
        Assert.True(_formatter.CanFormat);
    }

    [Fact]
    public void Indents_compact_json()
    {
        string result = _formatter.Format("{\"a\":1,\"b\":[2,3]}");

        Assert.Contains("\n", result);
        Assert.Contains("\"a\": 1", result);
    }

    [Fact]
    public void Invalid_json_throws_text_format_exception()
    {
        Assert.Throws<TextFormatException>(() => _formatter.Format("{not valid"));
    }

    [Fact]
    public void Whitespace_only_input_is_returned_unchanged()
    {
        Assert.Equal("   ", _formatter.Format("   "));
    }
}

public sealed class XmlFormatterTests
{
    private readonly XmlFormatter _formatter = new();

    [Fact]
    public void Indents_compact_xml()
    {
        string result = _formatter.Format("<root><child>1</child></root>");

        Assert.Contains("\n", result);
        Assert.Contains("<child>1</child>", result);
    }

    [Fact]
    public void Invalid_xml_throws_text_format_exception()
    {
        Assert.Throws<TextFormatException>(() => _formatter.Format("<root><child></root>"));
    }
}

public sealed class NullFormatterTests
{
    [Fact]
    public void Reports_it_cannot_format_and_returns_input_unchanged()
    {
        ITextFormatter formatter = NullFormatter.Instance;

        Assert.False(formatter.CanFormat);
        Assert.Equal("abc", formatter.Format("abc"));
    }
}
