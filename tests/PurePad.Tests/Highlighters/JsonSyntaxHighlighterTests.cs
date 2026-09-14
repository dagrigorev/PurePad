using PurePad.Formatting;
using PurePad.Formatting.Highlighters;

namespace PurePad.Tests.Highlighters;

public sealed class JsonSyntaxHighlighterTests
{
    private readonly JsonSyntaxHighlighter _highlighter = new();

    [Fact]
    public void Classifies_property_names_separately_from_string_values()
    {
        const string json = "{\"name\": \"PurePad\"}";

        var tokens = _highlighter.Tokenize(json).ToList();

        Assert.Contains("\"name\"", tokens.TextsOf(json, TokenKind.Property));
        Assert.Contains("\"PurePad\"", tokens.TextsOf(json, TokenKind.String));
    }

    [Fact]
    public void Classifies_numbers_booleans_and_null()
    {
        const string json = "{\"n\": 12.5, \"ok\": true, \"x\": null}";

        var tokens = _highlighter.Tokenize(json).ToList();

        Assert.Contains("12.5", tokens.TextsOf(json, TokenKind.Number));
        Assert.Contains("true", tokens.TextsOf(json, TokenKind.Boolean));
        Assert.Contains("null", tokens.TextsOf(json, TokenKind.Null));
    }

    [Fact]
    public void Treats_a_colon_string_as_a_property_even_with_whitespace()
    {
        const string json = "{ \"key\"   :  1 }";

        var tokens = _highlighter.Tokenize(json).ToList();

        Assert.Contains("\"key\"", tokens.TextsOf(json, TokenKind.Property));
    }

    [Fact]
    public void Does_not_throw_on_unterminated_string()
    {
        const string json = "{\"broken: 1";

        var tokens = _highlighter.Tokenize(json).ToList();

        tokens.AssertWithinBounds(json);
    }

    [Fact]
    public void Handles_escaped_quotes_inside_strings()
    {
        const string json = "\"a\\\"b\"";

        var tokens = _highlighter.Tokenize(json).ToList();

        // The whole literal, including the escaped quote, is one string token.
        Assert.Contains(json, tokens.TextsOf(json, TokenKind.String));
    }
}
