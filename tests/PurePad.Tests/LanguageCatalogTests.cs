using PurePad.Formatting;

namespace PurePad.Tests;

public sealed class LanguageCatalogTests
{
    private readonly LanguageCatalog _catalog = new();

    [Theory]
    [InlineData(".json", "json")]
    [InlineData(".JSON", "json")]
    [InlineData("json", "json")]
    [InlineData(".xml", "xml")]
    [InlineData(".cs", "code")]
    [InlineData(".md", "markdown")]
    public void Resolves_known_extensions(string extension, string expectedId)
    {
        Assert.Equal(expectedId, _catalog.ResolveByExtension(extension).Id);
    }

    [Theory]
    [InlineData(".unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_extension_resolves_to_plain_text(string? extension)
    {
        Assert.Same(_catalog.PlainText, _catalog.ResolveByExtension(extension));
    }

    [Fact]
    public void Resolves_by_id_case_insensitively()
    {
        Assert.Equal("xml", _catalog.ResolveById("XML").Id);
    }

    [Fact]
    public void Unknown_id_resolves_to_plain_text()
    {
        Assert.Same(_catalog.PlainText, _catalog.ResolveById("nope"));
    }

    [Fact]
    public void Plain_text_uses_null_object_collaborators()
    {
        Assert.Empty(_catalog.PlainText.Highlighter.Tokenize("anything"));
        Assert.False(_catalog.PlainText.Formatter.CanFormat);
    }

    [Fact]
    public void Languages_list_excludes_plain_text()
    {
        Assert.DoesNotContain(_catalog.Languages, l => ReferenceEquals(l, _catalog.PlainText));
    }
}
