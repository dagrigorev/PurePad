using PurePad.Diagnostics;
using PurePad.Domain;
using PurePad.Editor;
using PurePad.Formatting;
using PurePad.Services;
using PurePad.Theming;
using PurePad.Tools;

namespace PurePad.App;

/// <summary>
/// Mediator between the view, the document model, the file/dialog services and the
/// formatting subsystem. It holds all file-lifecycle and colourising logic so the
/// <c>MainForm</c> stays a thin view and so the behaviour is exercised through a small,
/// well-defined surface (Single Responsibility, Dependency Inversion).
/// </summary>
public sealed class EditorController
{
    private readonly ITextEditor _editor;
    private readonly TextDocument _document;
    private readonly IFileService _files;
    private readonly IDialogService _dialogs;
    private readonly ILanguageCatalog _catalog;
    private readonly ExternalToolCatalog _tools;

    private LanguageDefinition _forcedLanguage;
    private CancellationTokenSource? _loadCts;
    private bool _loadedTruncated;

    public EditorController(
        ITextEditor editor,
        TextDocument document,
        IFileService files,
        IDialogService dialogs,
        ILanguageCatalog catalog,
        ExternalToolCatalog tools)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _forcedLanguage = catalog.PlainText;
    }

    /// <summary>Raised when colouring should be refreshed (the view owns the actual rendering now).</summary>
    public event EventHandler? HighlightRequested;

    /// <summary>Raised when the syntax mode/language changes, so the view can refresh its menu.</summary>
    public event EventHandler? SyntaxStateChanged;

    /// <summary>Raised after a syntax check, carrying the diagnostics to display.</summary>
    public event EventHandler<IReadOnlyList<Diagnostic>>? DiagnosticsProduced;

    /// <summary>Raised when an async file load begins.</summary>
    public event EventHandler? LoadStarted;

    /// <summary>Raised (0..1) as an async file load progresses.</summary>
    public event EventHandler<double>? LoadProgressChanged;

    /// <summary>Raised when an async file load finishes (success, error or cancel).</summary>
    public event EventHandler? LoadCompleted;

    /// <summary>Raised with the full path when a file is opened or saved (for the recent-files list).</summary>
    public event EventHandler<string>? DocumentOpened;

    /// <summary>Files at/above this size load in a reduced-feature mode to stay responsive.</summary>
    public const long LargeDocumentBytes = 20L * 1024 * 1024;

    /// <summary>At/above this size a file opens in the read-only virtualized viewer instead of the editor.</summary>
    public const long ViewerThresholdBytes = 32L * 1024 * 1024;

    /// <summary>Raised when a file is too large to edit and should open in the virtualized viewer.</summary>
    public event EventHandler<string>? LargeFileRequested;

    /// <summary>True while a read-only large-file view is active; blocks saving.</summary>
    public bool ReadOnlyView { get; set; }

    /// <summary>When true, the document is reformatted automatically before each save.</summary>
    public bool AutoFormatOnSave { get; set; }

    /// <summary>When true, the "save changes?" prompt is skipped and unsaved changes are discarded (headless/test mode).</summary>
    public bool SuppressSavePrompts { get; set; }

    public TextDocument Document => _document;

    public ILanguageCatalog Catalog => _catalog;

    public SyntaxMode SyntaxMode { get; private set; } = SyntaxMode.Auto;

    /// <summary>The language actually in effect given the current mode and file extension.</summary>
    public LanguageDefinition EffectiveLanguage => SyntaxMode switch
    {
        SyntaxMode.Off => _catalog.PlainText,
        SyntaxMode.Forced => _forcedLanguage,
        _ => _catalog.ResolveByExtension(_document.Extension),
    };

    /// <summary>The language forced by the user (meaningful only when mode is Forced).</summary>
    public LanguageDefinition ForcedLanguage => _forcedLanguage;

    /// <summary>Formatter in effect: the external tool when configured, otherwise the built-in one.</summary>
    public ITextFormatter EffectiveFormatter =>
        _tools.TryGetFormatter(EffectiveLanguage.Id, out var external) ? external : EffectiveLanguage.Formatter;

    /// <summary>Checker in effect: the external tool when configured, otherwise the built-in one.</summary>
    public ISyntaxChecker EffectiveChecker =>
        _tools.TryGetChecker(EffectiveLanguage.Id, out var external) ? external : EffectiveLanguage.Checker;

    public bool CanReformat => EffectiveFormatter.CanFormat;

    public bool CanCheck => EffectiveChecker.CanCheck;


    // ----- File lifecycle -------------------------------------------------

    /// <summary>Start a new, empty document after offering to save any pending changes.</summary>
    public void NewDocument()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        _editor.Text = string.Empty;
        _editor.Modified = false;
        _loadedTruncated = false;
        _document.Reset();
        ApplyHighlight();
    }

    /// <summary>Prompt for a file and open it asynchronously.</summary>
    public async Task OpenWithDialogAsync()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        string? path = _dialogs.PromptOpenFile();
        if (path is not null && !RouteIfTooLargeToEdit(path))
        {
            await LoadFileAsync(path).ConfigureAwait(true);
        }
    }

    /// <summary>Open <paramref name="path"/> directly (e.g. from a command-line argument).</summary>
    public async Task OpenPathAsync(string path)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        if (!RouteIfTooLargeToEdit(path))
        {
            await LoadFileAsync(path).ConfigureAwait(true);
        }
    }

    /// <summary>Hand very large files to the read-only viewer; returns true when it did so.</summary>
    private bool RouteIfTooLargeToEdit(string path)
    {
        try
        {
            if (new FileInfo(path).Length >= ViewerThresholdBytes)
            {
                LargeFileRequested?.Invoke(this, path);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall through to the normal loader, which will surface the error.
        }

        return false;
    }

    /// <summary>Cancel an in-flight load, if any.</summary>
    public void CancelLoad() => _loadCts?.Cancel();

    /// <summary>Whether the current document is a truncated large-file preview (saving disabled).</summary>
    public bool IsTruncatedView => _loadedTruncated;

    private bool BlockedByTruncation()
    {
        if (ReadOnlyView)
        {
            _dialogs.ShowInfo(
                "This file is open in read-only large-file view and cannot be saved from here.",
                "PurePad — Large File");
            return true;
        }

        if (_loadedTruncated)
        {
            _dialogs.ShowInfo(
                "Saving is disabled for a partially loaded large file, to avoid overwriting it " +
                "with only the part that is shown.",
                "PurePad — Large File");
            return true;
        }

        return false;
    }

    /// <summary>Save to the current file, falling back to Save As when there is no path.</summary>
    public bool Save()
    {
        if (BlockedByTruncation())
        {
            return false;
        }

        if (!_document.HasPath)
        {
            return SaveAs();
        }

        return WriteTo(_document.FilePath!, _document.Encoding);
    }

    /// <summary>Prompt for a path and save there.</summary>
    public bool SaveAs()
    {
        if (BlockedByTruncation())
        {
            return false;
        }

        string? path = _dialogs.PromptSaveFile(_document.DisplayName);
        if (path is null)
        {
            return false;
        }

        return WriteTo(path, _document.Encoding);
    }

    /// <summary>
    /// Offer to save unsaved changes. Returns true when it is safe to proceed (the caller
    /// may discard the buffer) and false when the user cancelled.
    /// </summary>
    public bool ConfirmDiscardChanges()
    {
        if (!_editor.Modified || SuppressSavePrompts)
        {
            return true;
        }

        return _dialogs.ConfirmSaveChanges(_document.DisplayName) switch
        {
            SaveChangesResponse.Save => Save(),
            SaveChangesResponse.Discard => true,
            _ => false,
        };
    }

    private async Task LoadFileAsync(string path)
    {
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var progress = new Progress<double>(p => LoadProgressChanged?.Invoke(this, p));

        LoadStarted?.Invoke(this, EventArgs.Empty);
        try
        {
            // ReadAsync decodes off the UI thread; ConfigureAwait(true) returns here to touch the editor.
            FileContent content = await _files.ReadAsync(path, progress, _loadCts.Token).ConfigureAwait(true);
            ApplyLoadedContent(path, content);
        }
        catch (OperationCanceledException)
        {
            // User cancelled: leave the current document untouched.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Cannot open '{Path.GetFileName(path)}'.\n\n{ex.Message}");
        }
        finally
        {
            LoadCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyLoadedContent(string path, FileContent content)
    {
        _editor.LoadText(content.Text);
        _document.MarkSaved(path, content.Encoding);
        _loadedTruncated = content.Truncated;
        DocumentOpened?.Invoke(this, path);
        ApplyHighlight();

        if (content.Truncated && !SuppressSavePrompts)
        {
            _dialogs.ShowInfo(
                "This file is too large to open fully. Only the first part is shown, and saving " +
                "is disabled to protect the original file.",
                "PurePad — Large File");
        }
    }

    private bool WriteTo(string path, System.Text.Encoding encoding)
    {
        try
        {
            if (AutoFormatOnSave)
            {
                TryAutoFormat();
            }

            _files.Write(path, _editor.Text, encoding);
            _editor.Modified = false;
            _document.MarkSaved(path, encoding);
            DocumentOpened?.Invoke(this, path);
            ApplyHighlight(); // extension may have changed which language applies
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Cannot save '{Path.GetFileName(path)}'.\n\n{ex.Message}");
            return false;
        }
    }

    // ----- Colourising & formatting --------------------------------------

    /// <summary>Recolour the document using the currently effective highlighter.</summary>
    public void ApplyHighlight() => HighlightRequested?.Invoke(this, EventArgs.Empty);

    public void SetSyntaxAuto()
    {
        SyntaxMode = SyntaxMode.Auto;
        RaiseSyntaxChanged();
    }

    public void SetSyntaxOff()
    {
        SyntaxMode = SyntaxMode.Off;
        RaiseSyntaxChanged();
    }

    public void ForceLanguage(LanguageDefinition language)
    {
        _forcedLanguage = language ?? throw new ArgumentNullException(nameof(language));
        SyntaxMode = SyntaxMode.Forced;
        RaiseSyntaxChanged();
    }

    /// <summary>Reformat (pretty-print) the whole document with the effective formatter.</summary>
    public void ReformatDocument()
    {
        ITextFormatter formatter = EffectiveFormatter;
        if (!formatter.CanFormat)
        {
            _dialogs.ShowInfo(
                "There is no reformatter available for this file type.",
                "PurePad");
            return;
        }

        try
        {
            ApplyFormatter(formatter);
            ApplyHighlight();
        }
        catch (TextFormatException ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    /// <summary>Run the effective checker and publish its diagnostics.</summary>
    public IReadOnlyList<Diagnostic> CheckDocument()
    {
        IReadOnlyList<Diagnostic> diagnostics = EffectiveChecker.Check(_editor.Text);
        DiagnosticsProduced?.Invoke(this, diagnostics);
        return diagnostics;
    }

    /// <summary>Format silently before a save; formatting failures are swallowed so the save still proceeds.</summary>
    private void TryAutoFormat()
    {
        ITextFormatter formatter = EffectiveFormatter;
        if (!formatter.CanFormat)
        {
            return;
        }

        try
        {
            ApplyFormatter(formatter);
        }
        catch (TextFormatException)
        {
            // Invalid content: save it as-is rather than blocking the save.
        }
    }

    private void ApplyFormatter(ITextFormatter formatter)
    {
        string formatted = formatter.Format(_editor.Text);
        if (!string.Equals(formatted, _editor.Text, StringComparison.Ordinal))
        {
            _editor.Text = formatted;
            _editor.Modified = true;
            _document.IsModified = true;
        }
    }

    private void RaiseSyntaxChanged()
    {
        ApplyHighlight();
        SyntaxStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
