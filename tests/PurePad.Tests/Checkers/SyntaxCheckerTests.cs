using PurePad.Diagnostics;
using PurePad.Formatting.Checkers;

namespace PurePad.Tests.Checkers;

public sealed class JsonSyntaxCheckerTests
{
    private readonly JsonSyntaxChecker _checker = new();

    [Fact]
    public void Valid_json_reports_no_problems()
    {
        Assert.Empty(_checker.Check("{\"a\": 1}"));
    }

    [Fact]
    public void Invalid_json_reports_an_error_with_location()
    {
        IReadOnlyList<Diagnostic> diagnostics = _checker.Check("{\"a\": }");

        Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);
        Assert.True(diagnostics[0].Line >= 1);
    }

    [Fact]
    public void Empty_input_is_clean()
    {
        Assert.Empty(_checker.Check("   "));
    }
}

public sealed class XmlSyntaxCheckerTests
{
    private readonly XmlSyntaxChecker _checker = new();

    [Fact]
    public void Valid_xml_reports_no_problems()
    {
        Assert.Empty(_checker.Check("<root><a>1</a></root>"));
    }

    [Fact]
    public void Mismatched_tags_report_an_error()
    {
        IReadOnlyList<Diagnostic> diagnostics = _checker.Check("<root><a></root>");

        Assert.NotEmpty(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);
    }
}

public sealed class NullSyntaxCheckerTests
{
    [Fact]
    public void Never_reports_and_cannot_check()
    {
        Assert.False(NullSyntaxChecker.Instance.CanCheck);
        Assert.Empty(NullSyntaxChecker.Instance.Check("anything at all"));
    }
}
