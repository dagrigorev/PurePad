using System.Drawing;
using System.Windows.Forms;
using PurePad.Search;

namespace PurePad.View.Dialogs;

/// <summary>
/// The modeless "Find" dialog, laid out like Vista Notepad: a query box, a Match case
/// checkbox, an Up/Down direction group and Find Next / Cancel buttons. It is a passive
/// view — it raises <see cref="FindNextRequested"/> and lets the host perform the search.
/// </summary>
public sealed class FindDialog : Form
{
    private readonly TextBox _query = new();
    private readonly CheckBox _matchCase = new();
    private readonly RadioButton _up = new();
    private readonly RadioButton _down = new();

    public FindDialog()
    {
        BuildLayout();
    }

    /// <summary>Raised when the user clicks Find Next.</summary>
    public event EventHandler<SearchRequest>? FindNextRequested;

    /// <summary>Seed the query box (e.g. with the current selection) and focus it.</summary>
    public string Query
    {
        get => _query.Text;
        set => _query.Text = value;
    }

    public void FocusQuery()
    {
        _query.Focus();
        _query.SelectAll();
    }

    private SearchRequest BuildRequest() =>
        new(_query.Text, _matchCase.Checked, searchDown: _down.Checked);

    private void BuildLayout()
    {
        SuspendLayout();

        Text = "Find";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(378, 116);
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        var findLabel = new Label
        {
            Text = "Fi&nd what:",
            Location = new Point(8, 12),
            Size = new Size(64, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _query.Location = new Point(76, 9);
        _query.Size = new Size(212, 23);

        _matchCase.Text = "Match &case";
        _matchCase.Location = new Point(10, 66);
        _matchCase.Size = new Size(120, 20);

        var direction = new GroupBox
        {
            Text = "Direction",
            Location = new Point(140, 50),
            Size = new Size(148, 46),
        };
        _up.Text = "&Up";
        _up.Location = new Point(12, 18);
        _up.Size = new Size(50, 20);
        _down.Text = "&Down";
        _down.Location = new Point(72, 18);
        _down.Size = new Size(64, 20);
        _down.Checked = true;
        direction.Controls.Add(_up);
        direction.Controls.Add(_down);

        var findNext = new Button
        {
            Text = "&Find Next",
            Location = new Point(298, 8),
            Size = new Size(72, 24),
        };
        findNext.Click += (s, e) => FindNextRequested?.Invoke(this, BuildRequest());

        var cancel = new Button
        {
            Text = "Cancel",
            Location = new Point(298, 38),
            Size = new Size(72, 24),
        };
        cancel.Click += (s, e) => Hide();

        AcceptButton = findNext;
        CancelButton = cancel;

        Controls.Add(findLabel);
        Controls.Add(_query);
        Controls.Add(_matchCase);
        Controls.Add(direction);
        Controls.Add(findNext);
        Controls.Add(cancel);

        ResumeLayout(performLayout: true);
    }

    /// <summary>Hide instead of destroying, so search state persists between invocations.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }
}
