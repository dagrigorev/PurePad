using System.Drawing;
using System.Windows.Forms;

namespace PurePad.Theming;

/// <summary>
/// Applies a theme's colours to an ordinary WinForms dialog and its child controls, so
/// PurePad's own dialogs (Find, Replace, Go To, About) follow the active theme instead of
/// staying system-white. OS-owned dialogs (Open/Save/Font) cannot be themed and are left
/// to the system. Centralising the walk keeps every dialog consistent.
/// </summary>
public static class DialogThemer
{
    public static void Apply(Form dialog, ThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        dialog.BackColor = palette.ChromeBackground;
        dialog.ForeColor = palette.ChromeForeground;
        ThemeChildren(dialog.Controls, palette);

        // Title bar follows the theme once the handle exists.
        if (dialog.IsHandleCreated)
        {
            WindowChrome.Apply(dialog.Handle, palette);
        }
        else
        {
            dialog.HandleCreated += (s, e) => WindowChrome.Apply(dialog.Handle, palette);
        }
    }

    private static void ThemeChildren(Control.ControlCollection controls, ThemePalette palette)
    {
        foreach (Control control in controls)
        {
            ThemeControl(control, palette);
            if (control.HasChildren)
            {
                ThemeChildren(control.Controls, palette);
            }
        }
    }

    private static void ThemeControl(Control control, ThemePalette palette)
    {
        switch (control)
        {
            case TextBox textBox:
                textBox.BackColor = palette.EditorBackground;
                textBox.ForeColor = palette.EditorForeground;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case Button button:
                button.ForeColor = palette.ChromeForeground;
                button.BackColor = palette.IsDark ? ControlPaint.Light(palette.ChromeBackground, 0.15f) : SystemColors.Control;
                button.FlatStyle = palette.IsDark ? FlatStyle.Flat : FlatStyle.Standard;
                button.FlatAppearance.BorderColor = palette.ChromeBorder;
                break;

            case ListView listView:
                listView.BackColor = palette.IsDark ? ControlPaint.Light(palette.ChromeBackground, 0.05f) : Color.White;
                listView.ForeColor = palette.ChromeForeground;
                break;

            // Labels, GroupBoxes, CheckBoxes and RadioButtons inherit the parent colours;
            // setting ForeColor keeps their text legible on a dark background.
            case Label:
            case GroupBox:
            case CheckBox:
            case RadioButton:
                control.ForeColor = palette.ChromeForeground;
                control.BackColor = Color.Transparent;
                break;
        }
    }
}
