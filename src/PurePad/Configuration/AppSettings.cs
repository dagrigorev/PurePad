using System.Drawing;

namespace PurePad.Configuration;

/// <summary>
/// The user's remembered preferences. A plain, serialisable data object with sensible
/// defaults — no behaviour — so it is trivial to persist and to reason about. The view
/// reads it on startup and writes it back on close.
/// </summary>
public sealed class AppSettings
{
    // Appearance
    public string ThemeId { get; set; } = "light";
    public string FontFamily { get; set; } = "Lucida Console";
    public float FontSize { get; set; } = 10f;
    public FontStyle FontStyle { get; set; } = FontStyle.Regular;

    // View toggles
    public bool WordWrap { get; set; }
    public bool StatusBarVisible { get; set; }
    public bool LineNumbersVisible { get; set; } = true;
    public bool ProblemsPanelVisible { get; set; }
    public bool AutoFormatOnSave { get; set; }
    public bool MinimapVisible { get; set; }
    public bool LineGuideVisible { get; set; }

    // Overflow: "NoWrap" | "WrapWindow" | "WrapColumn"
    public string OverflowStrategy { get; set; } = "NoWrap";
    public int LineWidthColumns { get; set; } = 80;

    /// <summary>Most-recently-opened file paths, newest first.</summary>
    public List<string> RecentFiles { get; set; } = new();

    // Folder sidebar
    public bool FolderViewVisible { get; set; }
    public string? FolderPath { get; set; }

    // Colourising: "auto" | "off" | "forced"
    public string SyntaxMode { get; set; } = "auto";
    public string? ForcedLanguageId { get; set; }

    // Window placement (-1 => centre on first run)
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public int WindowWidth { get; set; } = 660;
    public int WindowHeight { get; set; } = 480;
    public bool Maximized { get; set; }
}
