using PurePad.Formatting;
using PurePad.Formatting.Highlighters;

namespace PurePad.Tests.Highlighters;

public sealed class CodeSyntaxHighlighterTests
{
    private readonly CodeSyntaxHighlighter _highlighter =
        new(CodeKeywords.CFamily, hashLineComments: true);

    [Fact]
    public void Classifies_keywords_numbers_strings_and_line_comments()
    {
        const string code = "int x = 5; // note";

        var tokens = _highlighter.Tokenize(code).ToList();

        Assert.Contains("int", tokens.TextsOf(code, TokenKind.Keyword));
        Assert.Contains("5", tokens.TextsOf(code, TokenKind.Number));
        Assert.Contains("// note", tokens.TextsOf(code, TokenKind.Comment));
    }

    [Fact]
    public void Classifies_block_comments()
    {
        const string code = "a /* b\nc */ d";

        var tokens = _highlighter.Tokenize(code).ToList();

        Assert.Contains("/* b\nc */", tokens.TextsOf(code, TokenKind.Comment));
    }

    [Fact]
    public void Classifies_double_and_single_quoted_strings()
    {
        const string code = "x = \"hi\" + 'c'";

        var tokens = _highlighter.Tokenize(code).ToList();

        string[] strings = tokens.TextsOf(code, TokenKind.String);
        Assert.Contains("\"hi\"", strings);
        Assert.Contains("'c'", strings);
    }

    [Fact]
    public void Recognizes_hash_line_comments_when_enabled()
    {
        const string code = "value = 1  # trailing";

        var tokens = _highlighter.Tokenize(code).ToList();

        Assert.Contains("# trailing", tokens.TextsOf(code, TokenKind.Comment));
    }

    [Fact]
    public void String_ending_in_backslash_stays_within_bounds()
    {
        // A line ending with a backslash inside an unterminated string must not report a token
        // span past the text (which previously overran by one and could fault the renderer).
        const string code = "s = \"abc\\";

        var tokens = _highlighter.Tokenize(code).ToList();

        Assert.All(tokens, t => Assert.True(t.Start + t.Length <= code.Length,
            $"token [{t.Start},{t.Start + t.Length}) exceeds length {code.Length}"));
    }

    [Fact]
    public void Does_not_classify_keyword_substrings_inside_identifiers()
    {
        const string code = "internalValue intentional";

        var tokens = _highlighter.Tokenize(code).ToList();

        // "int" is inside "intentional"; neither word should be coloured as a keyword.
        Assert.Empty(tokens.TextsOf(code, TokenKind.Keyword));
    }
}
