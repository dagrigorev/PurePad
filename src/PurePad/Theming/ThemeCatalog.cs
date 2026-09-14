using System.Drawing;
using PurePad.Formatting;

namespace PurePad.Theming;

/// <summary>Registry of available themes. Add a theme here and it appears in the View → Theme menu (Open/Closed).</summary>
public interface IThemeCatalog
{
    IReadOnlyList<Theme> Themes { get; }

    Theme Default { get; }

    Theme ResolveById(string? id);
}

/// <summary>Default catalog shipping a Vista-style Light theme and a Dark theme.</summary>
public sealed class ThemeCatalog : IThemeCatalog
{
    private readonly List<Theme> _themes;
    private readonly Dictionary<string, Theme> _byId;

    public ThemeCatalog()
    {
        _themes = new List<Theme> { CreateLight(), CreateDark() };
        _byId = _themes.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<Theme> Themes => _themes;

    public Theme Default => _themes[0];

    public Theme ResolveById(string? id) =>
        id is not null && _byId.TryGetValue(id, out var theme) ? theme : Default;

    private static Theme CreateLight() => new(
        id: "light",
        displayName: "Light (Vista)",
        palette: new ThemePalette
        {
            EditorBackground = SystemColors.Window,
            EditorForeground = SystemColors.WindowText,
            GutterBackground = Color.FromArgb(240, 240, 240),
            GutterForeground = Color.FromArgb(110, 110, 110),
            GutterBorder = Color.FromArgb(210, 210, 210),
            ChromeBackground = SystemColors.Control,
            ChromeForeground = SystemColors.ControlText,
            ChromeBorder = SystemColors.ControlDark,
            Accent = Color.FromArgb(51, 153, 255),
            IsDark = false,
        },
        syntax: SyntaxTheme.CreateDefault());

    private static Theme CreateDark() => new(
        id: "dark",
        displayName: "Dark",
        palette: new ThemePalette
        {
            EditorBackground = Color.FromArgb(30, 30, 30),
            EditorForeground = Color.FromArgb(220, 220, 220),
            GutterBackground = Color.FromArgb(45, 45, 48),
            GutterForeground = Color.FromArgb(133, 133, 133),
            GutterBorder = Color.FromArgb(62, 62, 66),
            ChromeBackground = Color.FromArgb(45, 45, 48),
            ChromeForeground = Color.FromArgb(222, 222, 222),
            ChromeBorder = Color.FromArgb(62, 62, 66),
            Accent = Color.FromArgb(0, 122, 204),
            IsDark = true,
        },
        syntax: SyntaxTheme.CreateDark());
}
