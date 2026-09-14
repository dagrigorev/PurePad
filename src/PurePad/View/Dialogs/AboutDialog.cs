using System.Drawing;
using System.Windows.Forms;

namespace PurePad.View.Dialogs;

/// <summary>A small modal "About PurePad" box.</summary>
public sealed class AboutDialog : Form
{
    public AboutDialog()
    {
        BuildLayout();
    }

    private void BuildLayout()
    {
        SuspendLayout();

        Text = "About PurePad";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 168);
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        var title = new Label
        {
            Text = "PurePad",
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
            Location = new Point(16, 16),
            Size = new Size(328, 26),
        };

        var body = new Label
        {
            Text =
                "A faithful clone of Windows Vista Notepad,\n" +
                "with file-format colourising and reformatting.\n\n" +
                "Version 1.0",
            Location = new Point(18, 48),
            Size = new Size(328, 78),
        };

        var ok = new Button
        {
            Text = "OK",
            Location = new Point(268, 132),
            Size = new Size(78, 26),
            DialogResult = DialogResult.OK,
        };

        AcceptButton = ok;
        CancelButton = ok;

        Controls.Add(title);
        Controls.Add(body);
        Controls.Add(ok);

        ResumeLayout(performLayout: true);
    }
}
