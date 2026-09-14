using PurePad.Formatting;
using PurePad.Formatting.Highlighters;

namespace PurePad.Tests.Highlighters;

public sealed class MarkdownSyntaxHighlighterTests
{
    private readonly MarkdownSyntaxHighlighter _highlighter = new();

    [Fact]
    public void Classifies_heading_lines()
    {
        const string md = "# Title\nbody";

        var tokens = _highlighter.Tokenize(md).ToList();

        Assert.Contains("# Title", tokens.TextsOf(md, TokenKind.Heading));
    }

    [Fact]
    public void Classifies_bold_emphasis()
    {
        const string md = "a **bold** b";

        var tokens = _highlighter.Tokenize(md).ToList();

        Assert.Contains("**bold**", tokens.TextsOf(md, TokenKind.Emphasis));
    }

    [Fact]
    public void Classifies_inline_code()
    {
        const string md = "run `dotnet build` now";

        var tokens = _highlighter.Tokenize(md).ToList();

        Assert.Contains("`dotnet build`", tokens.TextsOf(md, TokenKind.String));
    }

    [Fact]
    public void Treats_fenced_block_contents_as_code()
    {
        const string md = "```\nline\n```";

        var tokens = _highlighter.Tokenize(md).ToList();

        Assert.Contains("line", tokens.TextsOf(md, TokenKind.String));
    }

    [Fact]
    public void Does_not_throw_on_unterminated_emphasis()
    {
        const string md = "a *unterminated";

        var tokens = _highlighter.Tokenize(md).ToList();

        tokens.AssertWithinBounds(md);
    }
}
