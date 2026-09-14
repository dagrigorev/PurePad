using System.Drawing;
using System.Windows.Forms;

namespace PurePad.Theming;

/// <summary>
/// A <see cref="ProfessionalColorTable"/> that paints menus and the status bar in a
/// theme's chrome colours. Used for dark themes, where the Vista system renderer cannot be
/// recoloured; light themes keep the authentic system renderer instead.
/// </summary>
public sealed class ThemedColorTable : ProfessionalColorTable
{
    private readonly ThemePalette _palette;

    public ThemedColorTable(ThemePalette palette)
    {
        _palette = palette;
        UseSystemColors = false;
    }

    private Color Hover => ControlPaint.Light(_palette.ChromeBackground, 0.15f);

    public override Color MenuItemSelected => Hover;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemPressedGradientBegin => _palette.ChromeBackground;
    public override Color MenuItemPressedGradientEnd => _palette.ChromeBackground;
    public override Color MenuItemBorder => _palette.Accent;
    public override Color MenuBorder => _palette.ChromeBorder;
    public override Color ToolStripDropDownBackground => _palette.ChromeBackground;
    public override Color ImageMarginGradientBegin => _palette.ChromeBackground;
    public override Color ImageMarginGradientMiddle => _palette.ChromeBackground;
    public override Color ImageMarginGradientEnd => _palette.ChromeBackground;
    public override Color ToolStripGradientBegin => _palette.ChromeBackground;
    public override Color ToolStripGradientMiddle => _palette.ChromeBackground;
    public override Color ToolStripGradientEnd => _palette.ChromeBackground;
    public override Color StatusStripGradientBegin => _palette.ChromeBackground;
    public override Color StatusStripGradientEnd => _palette.ChromeBackground;
    public override Color SeparatorDark => _palette.ChromeBorder;
    public override Color SeparatorLight => _palette.ChromeBorder;
}

/// <summary>A renderer that uses a <see cref="ThemedColorTable"/> and draws item text in the theme's foreground.</summary>
public sealed class ThemedToolStripRenderer : ToolStripProfessionalRenderer
{
    private readonly ThemePalette _palette;

    public ThemedToolStripRenderer(ThemePalette palette)
        : base(new ThemedColorTable(palette))
    {
        _palette = palette;
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _palette.ChromeForeground : ControlPaint.Dark(_palette.ChromeForeground, 0.2f);
        base.OnRenderItemText(e);
    }
}
