using System.Drawing;

namespace PurePad.Theming;

/// <summary>
/// The chrome colours for a theme: editor surface, gutter, menu, status bar and accent.
/// Pure data, separate from any control, so a theme can be defined without touching the UI.
/// </summary>
public sealed class ThemePalette
{
    public required Color EditorBackground { get; init; }

    public required Color EditorForeground { get; init; }

    public required Color GutterBackground { get; init; }

    public required Color GutterForeground { get; init; }

    public required Color GutterBorder { get; init; }

    public required Color ChromeBackground { get; init; }

    public required Color ChromeForeground { get; init; }

    public required Color ChromeBorder { get; init; }

    public required Color Accent { get; init; }

    /// <summary>True for dark themes, so controls can pick dark-appropriate system rendering.</summary>
    public required bool IsDark { get; init; }
}
