using PurePad.Formatting;

namespace PurePad.Theming;

/// <summary>
/// A complete visual theme: its chrome <see cref="ThemePalette"/> plus the matching
/// <see cref="SyntaxTheme"/> used to colour code. Bundling them keeps chrome and syntax
/// colours consistent when the theme changes.
/// </summary>
public sealed class Theme
{
    public Theme(string id, string displayName, ThemePalette palette, SyntaxTheme syntax)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Palette = palette ?? throw new ArgumentNullException(nameof(palette));
        Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
    }

    public string Id { get; }

    public string DisplayName { get; }

    public ThemePalette Palette { get; }

    public SyntaxTheme Syntax { get; }
}
