using PurePad.Formatting;
using PurePad.Formatting.Highlighters;

namespace PurePad.Tests.Highlighters;

public sealed class ScriptHighlightingTests
{
    private static string[] Kinds(CodeSyntaxHighlighter h, string code, TokenKind kind) =>
        h.Tokenize(code).TextsOf(code, kind);

    [Fact]
    public void JavaScript_regex_literal_is_classified()
    {
        var js = new CodeSyntaxHighlighter(new CodeSyntaxOptions { Keywords = CodeKeywords.JavaScript, RegexLiterals = true });

        Assert.Contains("/ab+c/gi", Kinds(js, "const r = /ab+c/gi;", TokenKind.Regex));
    }

    [Fact]
    public void Division_is_not_mistaken_for_a_regex()
    {
        var js = new CodeSyntaxHighlighter(new CodeSyntaxOptions { Keywords = CodeKeywords.JavaScript, RegexLiterals = true });

        Assert.Empty(js.Tokenize("var x = a / b / c;").TextsOf("var x = a / b / c;", TokenKind.Regex));
    }

    [Fact]
    public void Bash_colours_variables_and_hash_comments()
    {
        var bash = new CodeSyntaxHighlighter(new CodeSyntaxOptions
        {
            Keywords = CodeKeywords.Bash,
            LineComments = new[] { "#" },
            BlockComment = null,
            VariableSigil = '$',
        });
        const string code = "echo $HOME ${PATH} # a comment";

        Assert.Contains("$HOME", Kinds(bash, code, TokenKind.Variable));
        Assert.Contains("${PATH}", Kinds(bash, code, TokenKind.Variable));
        Assert.Contains("# a comment", Kinds(bash, code, TokenKind.Comment));
        Assert.Contains("echo", Kinds(bash, code, TokenKind.Keyword));
    }

    [Fact]
    public void Variables_are_coloured_inside_double_quoted_strings_only()
    {
        var bash = new CodeSyntaxHighlighter(new CodeSyntaxOptions
        {
            Keywords = CodeKeywords.Bash,
            LineComments = new[] { "#" },
            BlockComment = null,
            VariableSigil = '$',
        });
        const string dq = "echo \"Hi ${NAME} and $f\"";
        var dqVars = Kinds(bash, dq, TokenKind.Variable);
        Assert.Contains("${NAME}", dqVars);
        Assert.Contains("$f", dqVars);

        const string sq = "echo 'Hi $NAME'"; // single quotes: literal, no interpolation
        Assert.Empty(Kinds(bash, sq, TokenKind.Variable));
    }

    [Fact]
    public void Template_literal_interpolation_is_coloured()
    {
        var js = new CodeSyntaxHighlighter(new CodeSyntaxOptions { Keywords = CodeKeywords.JavaScript, RegexLiterals = true });
        const string code = "const s = `count is ${count} now`;";

        Assert.Contains("${count}", Kinds(js, code, TokenKind.Variable));
    }

    [Fact]
    public void PowerShell_block_comment_and_case_insensitive_keywords()
    {
        var ps = new CodeSyntaxHighlighter(new CodeSyntaxOptions
        {
            Keywords = CodeKeywords.PowerShell,
            LineComments = new[] { "#" },
            BlockComment = ("<#", "#>"),
            VariableSigil = '$',
            CaseInsensitiveKeywords = true,
        });
        const string code = "<# doc #> Function Get-X { param($a) }";

        Assert.Contains("<# doc #>", Kinds(ps, code, TokenKind.Comment));
        Assert.Contains("Function", Kinds(ps, code, TokenKind.Keyword)); // case-insensitive
        Assert.Contains("$a", Kinds(ps, code, TokenKind.Variable));
        Assert.Equal(("<#", "#>"), ((IBlockCommentDelimiters)ps).BlockComment);
    }

    [Fact]
    public void Batch_rem_and_percent_variables()
    {
        var cmd = new CodeSyntaxHighlighter(new CodeSyntaxOptions
        {
            Keywords = CodeKeywords.Cmd,
            LineComments = new[] { "::" },
            BlockComment = null,
            VariableSigil = '%',
            RemLineComments = true,
            CaseInsensitiveKeywords = true,
        });

        Assert.Contains("REM a note", Kinds(cmd, "REM a note", TokenKind.Comment));
        Assert.Contains(":: also a note", Kinds(cmd, ":: also a note", TokenKind.Comment));
        Assert.Contains("%PATH%", Kinds(cmd, "echo %PATH%", TokenKind.Variable));
        Assert.Contains("ECHO", Kinds(cmd, "ECHO hi", TokenKind.Keyword)); // case-insensitive
    }
}
