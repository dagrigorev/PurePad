using PurePad.Diagnostics;
using PurePad.Formatting.Checkers;
using PurePad.Formatting.Profiles;

namespace PurePad.Tests.Profiles;

public sealed class ProfileSyntaxCheckerTests
{
    private static LanguageProfile ProfileWith(params DiagnosticRule[] rules) => new()
    {
        Id = "t",
        Brackets = new() { new() { "{", "}" }, new() { "(", ")" } },
        Diagnostics = new(rules),
    };

    [Fact]
    public void Reports_unclosed_bracket()
    {
        var checker = ProfileSyntaxChecker.Create(ProfileWith());

        var problems = checker.Check("func f() {\n  x\n");

        Assert.Contains(problems, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("Unclosed '{'"));
    }

    [Fact]
    public void Reports_mismatched_bracket()
    {
        var checker = ProfileSyntaxChecker.Create(ProfileWith());

        var problems = checker.Check("a(b}");

        Assert.Contains(problems, d => d.Message.Contains("Mismatched"));
    }

    [Fact]
    public void Brackets_inside_strings_are_ignored()
    {
        var checker = ProfileSyntaxChecker.Create(ProfileWith());

        var problems = checker.Check("x = \"a { b (\" ;");

        Assert.Empty(problems);
    }

    [Fact]
    public void Regex_rule_becomes_a_diagnostic_with_position()
    {
        var checker = ProfileSyntaxChecker.Create(ProfileWith(
            new DiagnosticRule { Pattern = @"\bTODO\b", Message = "todo", Severity = "info" }));

        var problems = checker.Check("ok\n  TODO later\n");

        var todo = Assert.Single(problems, d => d.Message == "todo");
        Assert.Equal(DiagnosticSeverity.Info, todo.Severity);
        Assert.Equal(2, todo.Line);
        Assert.Equal(3, todo.Column);
    }

    [Fact]
    public void No_rules_yields_the_null_checker()
    {
        var checker = ProfileSyntaxChecker.Create(new LanguageProfile { Id = "empty" });

        Assert.False(checker.CanCheck);
    }
}
