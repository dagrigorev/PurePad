using System.Drawing;
using System.Windows.Forms;

namespace PurePad.View.Dialogs;

/// <summary>
/// The modal "Go To Line" dialog. It only collects a line number; the caller performs
/// the navigation. Returns <see cref="DialogResult.OK"/> with <see cref="LineNumber"/> set.
/// </summary>
public sealed class GoToDialog : Form
{
    private readonly TextBox _lineBox = new();

    public GoToDialog(int currentLine)
    {
        BuildLayout(currentLine);
    }

    /// <summary>The 1-based line number the user entered.</summary>
    public int LineNumber { get; private set; }

    private void BuildLayout(int currentLine)
    {
        SuspendLayout();

        Text = "Go To Line";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(268, 100);
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        var label = new Label
        {
            Text = "&Line number:",
            Location = new Point(10, 12),
            Size = new Size(120, 20),
        };

        _lineBox.Location = new Point(10, 34);
        _lineBox.Size = new Size(180, 23);
        _lineBox.Text = currentLine.ToString();

        var ok = new Button
        {
            Text = "Go To",
            Location = new Point(100, 68),
            Size = new Size(74, 24),
            DialogResult = DialogResult.None,
        };
        ok.Click += OnOk;

        var cancel = new Button
        {
            Text = "Cancel",
            Location = new Point(184, 68),
            Size = new Size(74, 24),
            DialogResult = DialogResult.Cancel,
        };

        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(label);
        Controls.Add(_lineBox);
        Controls.Add(ok);
        Controls.Add(cancel);

        ResumeLayout(performLayout: true);
    }

    private void OnOk(object? sender, EventArgs e)
    {
        if (int.TryParse(_lineBox.Text.Trim(), out int line) && line >= 1)
        {
            LineNumber = line;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        MessageBox.Show(
            this,
            "The line number is beyond the total number of lines.",
            "PurePad",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        _lineBox.SelectAll();
        _lineBox.Focus();
    }
}
