using PurePad.Formatting;
using PurePad.Formatting.Highlighters;

namespace PurePad.Tests.Highlighters;

public sealed class XmlSyntaxHighlighterTests
{
    private readonly XmlSyntaxHighlighter _highlighter = new();

    [Fact]
    public void Classifies_attribute_names_and_values()
    {
        const string xml = "<a href=\"x\">hi</a>";

        var tokens = _highlighter.Tokenize(xml).ToList();

        Assert.Contains("href", tokens.TextsOf(xml, TokenKind.AttributeName));
        Assert.Contains("\"x\"", tokens.TextsOf(xml, TokenKind.AttributeValue));
    }

    [Fact]
    public void Classifies_comments()
    {
        const string xml = "<a/><!-- note -->";

        var tokens = _highlighter.Tokenize(xml).ToList();

        Assert.Contains("<!-- note -->", tokens.TextsOf(xml, TokenKind.Comment));
    }

    [Fact]
    public void Marks_tag_names()
    {
        const string xml = "<root></root>";

        var tokens = _highlighter.Tokenize(xml).ToList();

        string[] tags = tokens.TextsOf(xml, TokenKind.Tag);
        Assert.Contains("<root", tags);
        Assert.Contains("</root", tags);
    }

    [Fact]
    public void Does_not_throw_on_unclosed_tag()
    {
        const string xml = "<a href=";

        var tokens = _highlighter.Tokenize(xml).ToList();

        tokens.AssertWithinBounds(xml);
    }
}
