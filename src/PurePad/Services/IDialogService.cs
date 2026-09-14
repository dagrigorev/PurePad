using System.Drawing;

namespace PurePad.Services;

/// <summary>User's answer to the "save changes?" prompt shown before discarding a buffer.</summary>
public enum SaveChangesResponse
{
    Save,
    Discard,
    Cancel,
}

/// <summary>
/// Abstraction over the modal dialogs the editor needs (open/save/font pickers, message
/// boxes and the save-changes prompt). Commands depend on this interface, never on
/// WinForms dialog types directly, so their logic stays UI-framework agnostic and testable.
/// </summary>
public interface IDialogService
{
    /// <summary>Show the Open dialog. Returns the chosen path, or null if cancelled.</summary>
    string? PromptOpenFile();

    /// <summary>Show the Save As dialog seeded with <paramref name="suggestedFileName"/>. Returns the path, or null if cancelled.</summary>
    string? PromptSaveFile(string suggestedFileName);

    /// <summary>Show the folder picker. Returns the chosen folder path, or null if cancelled.</summary>
    string? PromptOpenFolder();

    /// <summary>Show the Font dialog seeded with <paramref name="current"/>. Returns the chosen font, or null if cancelled.</summary>
    Font? PromptFont(Font current);

    /// <summary>Show the three-way "save changes to X?" prompt.</summary>
    SaveChangesResponse ConfirmSaveChanges(string documentName);

    /// <summary>Show an error message box.</summary>
    void ShowError(string message);

    /// <summary>Show an informational message box.</summary>
    void ShowInfo(string message, string caption);
}
