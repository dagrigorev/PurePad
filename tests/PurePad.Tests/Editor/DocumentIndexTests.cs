using PurePad.Editor;

namespace PurePad.Tests.Editor;

public sealed class DocumentIndexTests
{
    [Fact]
    public void Indexes_function_definition_with_preceding_doc_comment()
    {
        const string code =
            "/// Adds two numbers.\n" +
            "int add(int a, int b) { return a + b; }\n";

        var index = DocumentIndex.Build(code);

        Assert.True(index.TryGetDefinition("add", out var def));
        Assert.Equal(SymbolKind.Function, def.Kind);
        Assert.Equal(1, def.Line);
        Assert.Contains("Adds two numbers", def.Doc);
    }

    [Fact]
    public void Indexes_macro_and_type_and_include()
    {
        const string code =
            "#include \"sensors.h\"\n" +
            "#define MAX 10\n" +
            "struct Point { int x; };\n";

        var index = DocumentIndex.Build(code);

        Assert.True(index.TryGetDefinition("MAX", out var macro));
        Assert.Equal(SymbolKind.Macro, macro.Kind);
        Assert.True(index.TryGetDefinition("Point", out var type));
        Assert.Equal(SymbolKind.Type, type.Kind);
        Assert.True(index.TryGetDefinition("sensors.h", out var inc));
        Assert.Equal(SymbolKind.Include, inc.Kind);
    }

    [Fact]
    public void Control_keywords_are_not_definitions()
    {
        const string code = "if (ready) { run(); }\nfor (int i = 0; i < n; i++) {}\n";

        var index = DocumentIndex.Build(code);

        Assert.False(index.TryGetDefinition("if", out _));
        Assert.False(index.TryGetDefinition("for", out _));
    }

    [Fact]
    public void Completion_matches_prefix_excluding_exact_word()
    {
        const string code = "int counter; int count; int country;\n";

        var index = DocumentIndex.Build(code);
        var hits = index.CompletionsFor("count", Array.Empty<string>());

        Assert.Contains("counter", hits);
        Assert.Contains("country", hits);
        Assert.DoesNotContain("count", hits); // exact word is not its own suggestion
    }

    [Fact]
    public void WordAt_and_IncludeTargetAt_locate_tokens()
    {
        Assert.Equal("sensors", DocumentIndex.WordAt("int sensors = 3;", 6));
        Assert.Equal("ui.h", DocumentIndex.IncludeTargetAt("#include \"ui.h\"", 12));
        Assert.Null(DocumentIndex.IncludeTargetAt("int x = 1;", 3));
    }
}
