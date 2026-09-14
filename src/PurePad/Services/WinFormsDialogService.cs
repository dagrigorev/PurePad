using System.Drawing;
using System.Windows.Forms;

namespace PurePad.Services;

/// <summary>
/// WinForms implementation of <see cref="IDialogService"/>. All dialogs are parented to
/// the supplied owner so they centre correctly and stay modal to the main window.
/// The filter mirrors classic Notepad ("Text Documents" / "All Files") plus a few of the
/// formats PurePad can colourise.
/// </summary>
public sealed class WinFormsDialogService : IDialogService
{
    private const string FileFilter =
        "Text Documents (*.txt)|*.txt|" +
        "All Files (*.*)|*.*";

    private readonly IWin32Window _owner;

    public WinFormsDialogService(IWin32Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public string? PromptOpenFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = FileFilter,
            FilterIndex = 1,
            RestoreDirectory = true,
            CheckFileExists = true,
        };

        return dialog.ShowDialog(_owner) == DialogResult.OK ? dialog.FileName : null;
    }

    public string? PromptSaveFile(string suggestedFileName)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = FileFilter,
            FilterIndex = 1,
            RestoreDirectory = true,
            FileName = suggestedFileName,
            DefaultExt = "txt",
            AddExtension = true,
        };

        return dialog.ShowDialog(_owner) == DialogResult.OK ? dialog.FileName : null;
    }

    public string? PromptOpenFolder()
    {
        using var dialog = new FolderBrowserDialog { ShowNewFolderButton = false };
        return dialog.ShowDialog(_owner) == DialogResult.OK ? dialog.SelectedPath : null;
    }

    public Font? PromptFont(Font current)
    {
        using var dialog = new FontDialog
        {
            Font = current,
            ShowColor = false,
            ShowEffects = true,
            FontMustExist = true,
        };

        return dialog.ShowDialog(_owner) == DialogResult.OK ? dialog.Font : null;
    }

    public SaveChangesResponse ConfirmSaveChanges(string documentName)
    {
        DialogResult result = MessageBox.Show(
            _owner,
            $"Do you want to save changes to {documentName}?",
            "PurePad",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);

        return result switch
        {
            DialogResult.Yes => SaveChangesResponse.Save,
            DialogResult.No => SaveChangesResponse.Discard,
            _ => SaveChangesResponse.Cancel,
        };
    }

    public void ShowError(string message) =>
        MessageBox.Show(_owner, message, "PurePad", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public void ShowInfo(string message, string caption) =>
        MessageBox.Show(_owner, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
}
