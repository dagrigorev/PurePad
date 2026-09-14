using System.Text;

namespace PurePad.Domain;

/// <summary>
/// Represents the file the editor is working on: its path, the encoding it was loaded
/// with / will be saved as, and whether it has unsaved changes. It raises
/// <see cref="Changed"/> whenever a property that affects the window title or menus
/// moves (Observer), so the UI can react without polling.
/// </summary>
public sealed class TextDocument
{
    private string? _filePath;
    private bool _isModified;
    private Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Raised when <see cref="FilePath"/> or <see cref="IsModified"/> changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Full path of the backing file, or null for a never-saved document.</summary>
    public string? FilePath
    {
        get => _filePath;
        private set
        {
            if (!string.Equals(_filePath, value, StringComparison.Ordinal))
            {
                _filePath = value;
                OnChanged();
            }
        }
    }

    /// <summary>Whether the buffer differs from the last saved/loaded state.</summary>
    public bool IsModified
    {
        get => _isModified;
        set
        {
            if (_isModified != value)
            {
                _isModified = value;
                OnChanged();
            }
        }
    }

    /// <summary>Encoding used to load the file and to save it again.</summary>
    public Encoding Encoding
    {
        get => _encoding;
        set => _encoding = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool HasPath => _filePath is not null;

    /// <summary>The file name for display, or "Untitled" when never saved.</summary>
    public string DisplayName =>
        _filePath is null ? "Untitled" : Path.GetFileName(_filePath);

    /// <summary>File extension (lower-case, with dot) or empty string.</summary>
    public string Extension =>
        _filePath is null ? string.Empty : Path.GetExtension(_filePath).ToLowerInvariant();

    /// <summary>Reset to a brand-new, unsaved, unmodified document.</summary>
    public void Reset()
    {
        _filePath = null;
        _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _isModified = false;
        OnChanged();
    }

    /// <summary>Record that the document is now associated with, and clean against, a file.</summary>
    public void MarkSaved(string filePath, Encoding encoding)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        _isModified = false;
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
