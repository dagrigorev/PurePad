using System.Drawing;
using System.Windows.Forms;
using PurePad.Search;

namespace PurePad.View.Dialogs;

/// <summary>
/// The modeless "Replace" dialog, laid out like Vista Notepad: Find what / Replace with
/// boxes, a Match case checkbox and Find Next / Replace / Replace All / Cancel buttons.
/// It raises intent events and leaves the actual editing to the host.
/// </summary>
public sealed class ReplaceDialog : Form
{
    private readonly TextBox _query = new();
    private readonly TextBox _replacement = new();
    private readonly CheckBox _matchCase = new();

    public ReplaceDialog()
    {
        BuildLayout();
    }

    public event EventHandler<SearchRequest>? FindNextRequested;

    public event EventHandler<SearchRequest>? ReplaceRequested;

    public event EventHandler<SearchRequest>? ReplaceAllRequested;

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
        new(_query.Text, _matchCase.Checked, searchDown: true, replacement: _replacement.Text);

    private void BuildLayout()
    {
        SuspendLayout();

        Text = "Replace";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(398, 128);
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        var findLabel = new Label
        {
            Text = "Fi&nd what:",
            Location = new Point(8, 14),
            Size = new Size(78, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _query.Location = new Point(90, 11);
        _query.Size = new Size(200, 23);

        var replaceLabel = new Label
        {
            Text = "Re&place with:",
            Location = new Point(8, 44),
            Size = new Size(78, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _replacement.Location = new Point(90, 41);
        _replacement.Size = new Size(200, 23);

        _matchCase.Text = "Match &case";
        _matchCase.Location = new Point(10, 90);
        _matchCase.Size = new Size(120, 20);

        var findNext = new Button
        {
            Text = "&Find Next",
            Location = new Point(306, 10),
            Size = new Size(84, 24),
        };
        findNext.Click += (s, e) => FindNextRequested?.Invoke(this, BuildRequest());

        var replace = new Button
        {
            Text = "&Replace",
            Location = new Point(306, 40),
            Size = new Size(84, 24),
        };
        replace.Click += (s, e) => ReplaceRequested?.Invoke(this, BuildRequest());

        var replaceAll = new Button
        {
            Text = "Replace &All",
            Location = new Point(306, 70),
            Size = new Size(84, 24),
        };
        replaceAll.Click += (s, e) => ReplaceAllRequested?.Invoke(this, BuildRequest());

        var cancel = new Button
        {
            Text = "Cancel",
            Location = new Point(306, 100),
            Size = new Size(84, 24),
        };
        cancel.Click += (s, e) => Hide();

        AcceptButton = findNext;
        CancelButton = cancel;

        Controls.Add(findLabel);
        Controls.Add(_query);
        Controls.Add(replaceLabel);
        Controls.Add(_replacement);
        Controls.Add(_matchCase);
        Controls.Add(findNext);
        Controls.Add(replace);
        Controls.Add(replaceAll);
        Controls.Add(cancel);

        ResumeLayout(performLayout: true);
    }

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
