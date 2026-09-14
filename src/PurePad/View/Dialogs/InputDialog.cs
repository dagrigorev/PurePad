using System.Drawing;
using System.Windows.Forms;

namespace PurePad.View.Dialogs;

/// <summary>A small modal prompt for a single line of text (new name, etc.).</summary>
public sealed class InputDialog : Form
{
    private readonly TextBox _input = new();

    public InputDialog(string title, string prompt, string initialValue)
    {
        BuildLayout(title, prompt, initialValue);
    }

    /// <summary>The text the user entered.</summary>
    public string Value => _input.Text.Trim();

    /// <summary>Show the dialog; returns the entered text, or null if cancelled/empty.</summary>
    public static string? Ask(IWin32Window owner, string title, string prompt, string initialValue = "")
    {
        using var dialog = new InputDialog(title, prompt, initialValue);
        return dialog.ShowDialog(owner) == DialogResult.OK && dialog.Value.Length > 0 ? dialog.Value : null;
    }

    private void BuildLayout(string title, string prompt, string initialValue)
    {
        SuspendLayout();

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(340, 104);
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        var label = new Label { Text = prompt, Location = new Point(12, 12), Size = new Size(316, 20) };

        _input.Location = new Point(12, 34);
        _input.Size = new Size(316, 23);
        _input.Text = initialValue;
        _input.SelectAll();

        var ok = new Button { Text = "OK", Location = new Point(172, 68), Size = new Size(74, 24), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Location = new Point(254, 68), Size = new Size(74, 24), DialogResult = DialogResult.Cancel };

        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(label);
        Controls.Add(_input);
        Controls.Add(ok);
        Controls.Add(cancel);

        ResumeLayout(performLayout: true);
    }
}
