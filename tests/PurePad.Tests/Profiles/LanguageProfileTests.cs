using System.Drawing;
using PurePad.Formatting;
using PurePad.Formatting.Formatters;
using PurePad.Formatting.Profiles;

namespace PurePad.Tests.Profiles;

public sealed class LanguageProfileTests
{
    [Fact]
    public void Factory_builds_language_with_colours_font_and_extensions()
    {
        var profile = new LanguageProfile
        {
            Id = "mylang",
            Name = "My Language",
            Extensions = new() { "ml", ".mml" }, // one without a dot: normalized
            Syntax = new SyntaxProfile { Keywords = new() { "let" }, VariableSigil = "$" },
            Colors = new() { ["Keyword"] = "#112233", ["String"] = "#abc" },
            Font = new FontProfile { Family = "Consolas", Size = 12, Style = "Bold+Italic" },
        };

        LanguageDefinition lang = LanguageProfileFactory.Create(profile);

        Assert.Equal("mylang", lang.Id);
        Assert.Contains(".ml", lang.Extensions);
        Assert.Contains(".mml", lang.Extensions);
        Assert.Equal(Color.FromArgb(0x11, 0x22, 0x33), lang.ColorOverrides![TokenKind.Keyword]);
        Assert.Equal(Color.FromArgb(0xAA, 0xBB, 0xCC), lang.ColorOverrides![TokenKind.String]); // "#abc" expands
        Assert.NotNull(lang.Font);
        Assert.Equal("Consolas", lang.Font!.Family);
        Assert.Equal(FontStyle.Bold | FontStyle.Italic, lang.Font!.Style);
    }

    [Fact]
    public void Profile_overrides_a_built_in_extension()
    {
        var profile = new LanguageProfile
        {
            Id = "myjson",
            Name = "My JSON",
            Extensions = new() { ".json" },
            Syntax = new SyntaxProfile { Keywords = new() { "true" } },
        };

        var catalog = new LanguageCatalog(new[] { profile });

        Assert.Equal("myjson", catalog.ResolveByExtension(".json").Id); // profile wins over built-in json
    }

    [Fact]
    public void SyntaxTheme_override_replaces_only_named_kinds()
    {
        SyntaxTheme baseTheme = SyntaxTheme.CreateDark();
        var overrides = new Dictionary<TokenKind, Color> { [TokenKind.Keyword] = Color.Red };

        SyntaxTheme merged = baseTheme.WithOverrides(overrides);

        Assert.Equal(Color.Red, merged.ColorFor(TokenKind.Keyword));
        Assert.Equal(baseTheme.ColorFor(TokenKind.String), merged.ColorFor(TokenKind.String)); // untouched
    }

    [Fact]
    public void Formatter_reindents_by_brackets_and_trims()
    {
        var formatter = new ConfigurableFormatter(new FormatProfile { IndentSize = 2, ReindentByBrackets = true });
        const string messy = "{\n\"a\": [\n1,\n2\n],\n\"b\": 3\n}   ";

        string formatted = formatter.Format(messy);

        Assert.Equal(
            "{\n  \"a\": [\n    1,\n    2\n  ],\n  \"b\": 3\n}",
            formatted);
    }
}
