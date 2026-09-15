using System.Drawing;
using System.Windows.Forms;
using PurePad.App;
using PurePad.Commands;
using PurePad.Domain;
using PurePad.Editor;
using PurePad.Formatting;
using PurePad.Configuration;
using PurePad.Lsp;
using PurePad.Diagnostics;
using PurePad.Search;
using PurePad.Services;
using PurePad.Theming;
using PurePad.Tools;
using PurePad.View.Animation;
using PurePad.View.Controls;
using PurePad.View.Dialogs;
using PurePad.View.Printing;

namespace PurePad.View;

/// <summary>
/// The Vista-Notepad-style main window. It is deliberately thin: it builds the UI and
/// forwards user gestures to the <see cref="EditorController"/>, the <see cref="SearchService"/>
/// and the dialogs. All file, colourising and formatting logic lives in those collaborators,
/// keeping the view focused on presentation (Single Responsibility).
/// </summary>
public sealed partial class MainForm : Form
{
    private static readonly Font DefaultEditorFont = new("Lucida Console", 10f);

    private readonly LargeFileViewer _largeViewer;
    private readonly ITextEditor _editor;
    private readonly IDialogService _dialogs;
    private readonly ILanguageCatalog _catalog;
    private readonly IThemeCatalog _themes;
    private readonly ISettingsStore _settingsStore;
    private readonly EditorController _controller;
    private readonly TextDocument _document;
    private readonly SearchService _search;
    private readonly DocumentPrinter _printer;
    private readonly System.Windows.Forms.Timer _highlightTimer;
    private readonly System.Windows.Forms.Timer _checkTimer;
    private readonly Dictionary<ToolStripMenuItem, IApplicationCommand> _commandBindings = new();

    private MenuStrip _menu = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _caretLabel = null!;
    private ToolStripStatusLabel _loadLabel = null!;
    private ToolStripProgressBar _loadProgress = null!;
    private ToolStripStatusLabel _cancelLabel = null!;
    private bool _statusVisibleBeforeLoad;
    private DiagnosticsPanel _diagnostics = null!;
    private FolderTreeView _folderView = null!;
    private Splitter _folderSplitter = null!;
    private FindDialog? _findDialog;
    private ReplaceDialog? _replaceDialog;
    private SearchRequest? _lastSearch;
    private Theme _currentTheme;
    private bool _statusBarRequested;
    private bool _readyToPersist;
    private FormWindowState _lastWindowState = FormWindowState.Normal;
    private string? _startupPath;
    private string? _screenshotPath;
    private LargeFiles.LargeFileDocument? _largeDoc;
    private readonly List<string> _recentFiles = new();
    private const int MaxRecentFiles = 10;
    private readonly LanguageProfileStore _profileStore = new(LanguageProfileStore.DefaultPath);
    private Font _baseEditorFont = DefaultEditorFont;

    public MainForm(
        IFileService fileService,
        ILanguageCatalog catalog,
        IThemeCatalog themes,
        ExternalToolCatalog externalTools,
        ISettingsStore settingsStore,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(fileService);
        ArgumentNullException.ThrowIfNull(externalTools);
        ArgumentNullException.ThrowIfNull(settings);
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _currentTheme = themes.ResolveById(settings.ThemeId);

        _largeViewer = new LargeFileViewer { Dock = DockStyle.Fill, Font = DefaultEditorFont };
        _largeViewer.ZoomChanged += (s, e) => PersistSettings(); // remember zoom across sessions
        _largeViewer.IncludeOpenRequested += (s, name) => OpenIncludeFile(name);
        _editor = _largeViewer; // the viewer is the single editing surface
        _dialogs = new WinFormsDialogService(this);
        _document = new TextDocument();
        _printer = new DocumentPrinter();
        _search = new SearchService(_editor);

        _controller = new EditorController(_editor, _document, fileService, _dialogs, _catalog, externalTools);

        _highlightTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _highlightTimer.Tick += OnHighlightTimerTick;
        _checkTimer = new System.Windows.Forms.Timer { Interval = 600 };
        _checkTimer.Tick += OnCheckTimerTick;

        InitializeUi();
        WireEvents();
        ApplySettings(settings);
        _editor.LoadText(string.Empty); // start with an empty, editable buffer
        SyncViewerHighlighting();
        UpdateTitle();
        UpdateCaretPosition();

        _readyToPersist = true; // startup done: user-driven changes may now persist
    }

    /// <summary>Remember a file passed on the command line; it is opened once the window is shown.</summary>
    public void OpenOnStartup(string path) => _startupPath = path;

    /// <summary>Debug hook: scroll to this 1-based line before the screenshot capture.</summary>
    public int? DebugGotoLine { get; set; }

    /// <summary>Debug hook: caret column placed on the caret line for the capture.</summary>
    public int DebugCaretColumn { get; set; }

    /// <summary>Debug hook: 1-based caret line (defaults to the goto line).</summary>
    public int? DebugCaretLine { get; set; }

    /// <summary>Debug hook: 1-based block-head lines to collapse before the capture.</summary>
    public List<int> DebugFoldLines { get; } = new();

    /// <summary>Debug hook: zoom steps applied before the capture.</summary>
    public int DebugZoomSteps { get; set; }

    /// <summary>Debug hook: open a second file (via the folder-view path) before the capture.</summary>
    public string? DebugReopenPath { get; set; }

    /// <summary>Debug hook: open the completion popup at the caret before the capture.</summary>
    public bool DebugShowCompletion { get; set; }

    /// <summary>Debug hook: run the syntax checker and show the Problems panel before the capture.</summary>
    public bool DebugRunCheck { get; set; }

    /// <summary>Debug hook: Ctrl+click go-to-definition at the caret before the capture.</summary>
    public bool DebugGoToDefinition { get; set; }

    /// <summary>Enable the debug screenshot hook: capture to <paramref name="path"/> once shown, then close.</summary>
    public void ScheduleScreenshot(string path)
    {
        _screenshotPath = path;
        _controller.SuppressSavePrompts = true; // never block an automated capture with a prompt
    }

    /// <summary>Skip the "save changes?" prompt (discard unsaved changes). For headless/testing.</summary>
    public void SetSuppressSavePrompts(bool suppress) => _controller.SuppressSavePrompts = suppress;

    // ----- UI construction ------------------------------------------------

    /// <summary>Load the embedded multi-resolution icon so the taskbar picks a crisp size.</summary>
    private static Icon? LoadAppIcon()
    {
        using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream("PurePad.app.ico");
        return stream is null ? null : new Icon(stream);
    }

    private void InitializeUi()
    {
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(660, 480);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = false;
        Icon = LoadAppIcon();

        _menu = BuildMenus();
        MainMenuStrip = _menu;

        _editorContextMenu = BuildEditorContextMenu();
        _largeViewer.ContextMenuStrip = _editorContextMenu;

        _statusStrip = BuildStatusStrip();

        _diagnostics = new DiagnosticsPanel { Visible = false };
        _diagnostics.DiagnosticActivated += OnDiagnosticActivated;

        _folderView = new FolderTreeView { Dock = DockStyle.Left, Width = 220, Visible = false };
        _folderView.FileActivated += (s, path) => OpenFromFolderView(path);
        _folderSplitter = new Splitter { Dock = DockStyle.Left, Width = 4, Visible = false };

        Controls.Add(_largeViewer);
        Controls.Add(_folderSplitter);
        Controls.Add(_folderView);
        Controls.Add(_diagnostics);
        Controls.Add(_statusStrip);
        Controls.Add(_menu);

        ResumeLayout(performLayout: true);
    }

    private StatusStrip BuildStatusStrip()
    {
        _caretLabel = new ToolStripStatusLabel
        {
            Text = "Ln 1, Col 1",
            Spring = true,
            TextAlign = ContentAlignment.MiddleRight,
        };

        _loadLabel = new ToolStripStatusLabel { Text = "Loading…", Visible = false };
        _loadProgress = new ToolStripProgressBar
        {
            Visible = false,
            Minimum = 0,
            Maximum = 100,
            Width = 160,
            Style = ProgressBarStyle.Continuous,
        };
        _cancelLabel = new ToolStripStatusLabel
        {
            Text = "Cancel",
            Visible = false,
            IsLink = true,
            ForeColor = Color.FromArgb(0, 102, 204),
        };
        _cancelLabel.Click += (s, e) => _controller.CancelLoad();

        return new StatusStrip
        {
            RenderMode = ToolStripRenderMode.System,
            SizingGrip = true,
            Visible = false, // Vista Notepad hides the status bar by default
            Items = { _loadLabel, _loadProgress, _cancelLabel, _caretLabel },
        };
    }

    // ----- Event wiring ---------------------------------------------------

    private void WireEvents()
    {
        _document.Changed += (s, e) => UpdateTitle();
        _editor.TextChanged += OnEditorTextChanged;
        _editor.SelectionChanged += (s, e) => UpdateCaretPosition();
        _controller.SyntaxStateChanged += OnSyntaxStateChanged;
        _controller.HighlightRequested += (s, e) => SyncViewerHighlighting();
        _controller.DiagnosticsProduced += (s, diagnostics) => _diagnostics.SetDiagnostics(diagnostics);
        _controller.LoadStarted += (s, e) => OnLoadStarted();
        _controller.LoadProgressChanged += (s, fraction) => OnLoadProgress(fraction);
        _controller.LoadCompleted += (s, e) => OnLoadCompleted();
        _controller.LargeFileRequested += (s, path) => _ = OpenLargeFileAsync(path);
        _controller.DocumentOpened += (s, path) => { AddRecentFile(path); SyncViewerHighlighting(); SyncLsp(); };
        _largeViewer.TextChanged += (s, e) => _lspDirty = true;
        ResizeEnd += (s, e) => PersistSettings(); // window moved/resized by the user
        SizeChanged += OnSizeChanged;              // catch maximize/restore, which ResizeEnd misses
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (WindowState != _lastWindowState)
        {
            _lastWindowState = WindowState;
            PersistSettings();
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        _document.IsModified = _editor.Modified;
        ScheduleHighlight();
        ScheduleCheck();
    }

    private void OnSyntaxStateChanged(object? sender, EventArgs e)
    {
        SyncViewerHighlighting();
        UpdateCaretPosition();
        ScheduleCheck();
        PersistSettings();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        InitLsp(); // the WinForms sync context is live now, so diagnostics marshal to the UI thread

        if (_startupPath is not null)
        {
            _ = _controller.OpenPathAsync(_startupPath);
            _startupPath = null;
        }

        UpdateJumpList(); // publish the persisted recent list to the taskbar

        // Title bar follows the theme now that the handle exists.
        WindowChrome.Apply(Handle, _currentTheme.Palette);
        NativeDarkMode.EnableApplicationDarkMode(_currentTheme.Palette.IsDark);

        // Smooth fade-in on launch.
        Opacity = 0;
        Animator.Animate(200, t => Opacity = t, () => Opacity = 1);

        if (_screenshotPath is not null)
        {
            CaptureScreenshotThenClose(_screenshotPath);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_screenshotPath is not null)
        {
            _lsp?.Dispose();
            _largeDoc?.Dispose();
            base.OnFormClosing(e); // debug capture run: exit without prompts or persistence
            return;
        }

        if (!ConfirmDiscard())
        {
            e.Cancel = true;
        }
        else
        {
            _settingsStore.Save(CaptureSettings());
            _lsp?.Dispose();
            _largeDoc?.Dispose();
        }

        base.OnFormClosing(e);
    }

    /// <summary>Wait for layout/colourising to settle, image the window, then exit.</summary>
    private void CaptureScreenshotThenClose(string path)
    {
        var timer = new System.Windows.Forms.Timer { Interval = 900 };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            timer.Dispose();
            try
            {
                if (DebugReopenPath is { } reopen)
                {
                    OpenFromFolderView(reopen);
                    for (int i = 0; i < 40; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
                }

                if (DebugZoomSteps != 0)
                {
                    _largeViewer.Zoom(DebugZoomSteps);
                }

                foreach (int foldLine in DebugFoldLines)
                {
                    _largeViewer.DebugToggleFold(foldLine);
                }

                if (DebugGotoLine is { } gotoLine)
                {
                    _largeViewer.GoToLine(gotoLine);
                    _largeViewer.DebugCaretAt((DebugCaretLine ?? gotoLine) - 1, DebugCaretColumn);
                    Application.DoEvents();
                }

                if (DebugRunCheck)
                {
                    _diagnostics.Visible = true;
                    _diagnostics.Height = 170;
                    _controller.CheckDocument();
                    Application.DoEvents();
                }

                if (DebugShowCompletion)
                {
                    _largeViewer.DebugShowCompletion();
                    Application.DoEvents();
                }

                if (DebugGoToDefinition)
                {
                    _largeViewer.DebugGoToDefinition();
                    Application.DoEvents();
                }

                ScreenCapture.Capture(this, path);
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(path + ".err.txt", ex.ToString()); } catch { /* ignore */ }
            }
            finally
            {
                Close();
            }
        };
        timer.Start();
    }

    // ----- Colourising ----------------------------------------------------

    private const int MaxCheckChars = 2_000_000; // skip live syntax checking above this size

    private void ScheduleHighlight()
    {
        if (ReferenceEquals(_controller.EffectiveLanguage, _catalog.PlainText))
        {
            return; // nothing to colour: keep plain-text typing fast
        }

        _highlightTimer.Stop();
        _highlightTimer.Start();
    }

    private void OnHighlightTimerTick(object? sender, EventArgs e)
    {
        _highlightTimer.Stop();
        _controller.ApplyHighlight();
    }

    // ----- Syntax checking ------------------------------------------------

    private void ScheduleCheck()
    {
        if (LspOwnsDiagnostics)
        {
            _checkTimer.Stop(); // the timer pushes a didChange; the server pushes diagnostics back
            _checkTimer.Start();
            return;
        }

        if (!_controller.CanCheck || _editor.TextLength > MaxCheckChars)
        {
            _diagnostics.SetDiagnostics(Array.Empty<Diagnostic>());
            return;
        }

        _checkTimer.Stop();
        _checkTimer.Start();
    }

    // ----- Async file loading --------------------------------------------

    private void OnLoadStarted()
    {
        ReleaseLargeBacking();
        BeginProgress("Loading…");
    }

    /// <summary>Release any memory-mapped backing (before loading a different document).</summary>
    private void ReleaseLargeBacking()
    {
        _largeDoc?.Dispose();
        _largeDoc = null;
        _controller.ReadOnlyView = false;
    }

    /// <summary>Show the status-bar progress UI without switching editor/viewer mode.</summary>
    private void BeginProgress(string label)
    {
        _statusVisibleBeforeLoad = _statusStrip.Visible;
        _statusStrip.Visible = true;
        _loadLabel.Visible = true;
        _loadProgress.Visible = true;
        _cancelLabel.Visible = true;
        _loadProgress.Value = 0;
        _loadLabel.Text = label;
        UseWaitCursor = true;
    }

    private void OnLoadProgress(double fraction)
    {
        int percent = Math.Clamp((int)(fraction * 100), 0, 100);
        _loadProgress.Value = percent;
        _loadLabel.Text = $"Loading… {percent}%";
    }

    private void SaveProgress(double fraction)
    {
        int percent = Math.Clamp((int)(fraction * 100), 0, 100);
        _loadProgress.Value = percent;
        _loadLabel.Text = $"Saving… {percent}%";
    }

    private void OnLoadCompleted()
    {
        _loadLabel.Visible = false;
        _loadProgress.Visible = false;
        _cancelLabel.Visible = false;
        _statusStrip.Visible = _statusVisibleBeforeLoad;
        UseWaitCursor = false;
    }

    /// <summary>Open a very large file into the read-only virtualized viewer.</summary>
    private async Task OpenLargeFileAsync(string path)
    {
        var cts = new System.Threading.CancellationTokenSource();
        var progress = new Progress<double>(OnLoadProgress);
        OnLoadStarted();
        _cancelLabel.Click += CancelLargeLoad;

        void CancelLargeLoad(object? s, EventArgs e) => cts.Cancel();

        try
        {
            LargeFiles.LargeFileDocument document =
                await LargeFiles.LargeFileDocument.OpenAsync(path, progress, cts.Token).ConfigureAwait(true);
            SwitchToViewer(document, path);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Cannot open '{Path.GetFileName(path)}'.\n\n{ex.Message}");
        }
        finally
        {
            _cancelLabel.Click -= CancelLargeLoad;
            cts.Dispose();
            OnLoadCompleted();
        }
    }

    private void SwitchToViewer(LargeFiles.LargeFileDocument document, string path)
    {
        _largeDoc?.Dispose();
        _largeDoc = document;

        bool editable = document.EditingSupported;
        if (editable)
        {
            var original = new LargeFiles.MappedOriginalText(document);
            var table = new LargeFiles.PieceTable(original, document.CharLineStarts());
            _largeViewer.SetEditable(document, table);
            _controller.ReadOnlyView = false;
        }
        else
        {
            _largeViewer.SetDocument(document);
            _controller.ReadOnlyView = true;
        }

        _document.MarkSaved(path, document.Encoding);
        SyncViewerHighlighting();
        _largeViewer.Focus();

        if (!editable)
        {
            Text = $"{Path.GetFileName(path)} (read-only) - Notepad";
        }

        _diagnostics.SetDiagnostics(Array.Empty<Diagnostic>());
        AddRecentFile(path);
    }

    // ----- Folder view ----------------------------------------------------

    /// <summary>Open a file chosen in the folder tree, routing large files to the mmap path.</summary>
    private void OpenFromFolderView(string path)
    {
        if (ConfirmDiscard())
        {
            _ = _controller.OpenPathAsync(path);
        }
    }

    /// <summary>Resolve an <c>#include</c> target near the open file and open it (Ctrl+click).</summary>
    private void OpenIncludeFile(string include)
    {
        string name = include.Replace('/', Path.DirectorySeparatorChar);
        string? dir = ActiveFileDirectory();

        var roots = new List<string>();
        for (string? d = dir; d is not null; d = Path.GetDirectoryName(d))
        {
            roots.Add(d);
            roots.Add(Path.Combine(d, "include"));
            roots.Add(Path.Combine(d, "inc"));
        }

        foreach (string root in roots)
        {
            string candidate = Path.Combine(root, name);
            if (File.Exists(candidate))
            {
                OpenFromFolderView(candidate);
                return;
            }
        }

        _dialogs.ShowInfo($"Could not find include \"{include}\" near the current file.", "PurePad");
    }

    /// <summary>Prompt for a folder and show it in the sidebar.</summary>
    private void OpenFolder()
    {
        string? folder = _dialogs.PromptOpenFolder();
        if (folder is null)
        {
            return;
        }

        _folderView.SetRoot(folder);
        SetFolderViewVisible(true);
        PersistSettings();
    }

    private void ToggleFolderView()
    {
        SetFolderViewVisible(!_folderView.Visible);
        if (_folderView.Visible && _folderView.RootPath is null)
        {
            // Default to the folder holding the open file; only prompt if there's no saved file.
            if (ActiveFileDirectory() is { } dir)
            {
                _folderView.SetRoot(dir);
            }
            else
            {
                OpenFolder();
            }
        }

        PersistSettings();
    }

    /// <summary>The path of the currently open file (large or small), or null when unsaved.</summary>
    private string? ActiveFilePath() => _largeDoc is not null
        ? _largeViewer.FilePath
        : _controller.Document.HasPath ? _controller.Document.FilePath : null;

    /// <summary>The directory of the currently open file (large or small), or null when unsaved.</summary>
    private string? ActiveFileDirectory()
    {
        if (ActiveFilePath() is not { } path)
        {
            return null;
        }

        string? dir = Path.GetDirectoryName(path);
        return dir is not null && Directory.Exists(dir) ? dir : null;
    }

    // ----- Language server (LSP) ------------------------------------------

    private LspManager? _lsp;
    private string? _lspActivePath;
    private bool _lspDirty;

    private void InitLsp()
    {
        _lsp = new LspManager(SynchronizationContext.Current);
        _lsp.DiagnosticsReported += (path, diagnostics) =>
        {
            if (string.Equals(path, _lspActivePath, StringComparison.OrdinalIgnoreCase))
            {
                _diagnostics.SetDiagnostics(diagnostics);
            }
        };
        _lsp.ServerMissing += command =>
        {
            if (!_controller.SuppressSavePrompts)
            {
                _dialogs.ShowInfo($"Language server '{command}' was not found on PATH; using built-in checking.", "PurePad — LSP");
            }
        };
    }

    /// <summary>Whether a language server currently owns diagnostics for the open document.</summary>
    private bool LspOwnsDiagnostics => _lspActivePath is { } p && _lsp?.IsActive(p) == true;

    /// <summary>Start/stop the language server as the active document or its language changes.</summary>
    private void SyncLsp()
    {
        if (_lsp is null)
        {
            return;
        }

        string? path = ActiveFilePath();

        // Close a server document we've navigated away from.
        if (_lspActivePath is { } prev && !string.Equals(prev, path, StringComparison.OrdinalIgnoreCase))
        {
            _lsp.CloseDocument(prev);
            _lspActivePath = null;
        }

        LspSettings? settings = _controller.EffectiveLanguage.Lsp;
        if (path is null || settings is null || !_largeViewer.IsEditable)
        {
            return; // no path, no server for this type, or a huge (read-only) file
        }

        if (!_lsp.IsActive(path))
        {
            _lsp.OpenDocument(path, settings, _editor.Text);
            _lspActivePath = path;
            _lspDirty = false;
        }
    }

    private void ToggleStickyScroll()
    {
        _largeViewer.StickyScrollEnabled = !_largeViewer.StickyScrollEnabled;
        PersistSettings();
    }

    private void ToggleAntialias()
    {
        _largeViewer.TextAntialiasing = !_largeViewer.TextAntialiasing;
        PersistSettings();
    }

    private void SetFolderViewVisible(bool visible)
    {
        _folderView.Visible = visible;
        _folderSplitter.Visible = visible;
    }

    /// <summary>Point the viewer at the effective language's highlighter, theme (with profile colour
    /// overrides) and — for a file type with a profile font — that font.</summary>
    private void SyncViewerHighlighting()
    {
        LanguageDefinition lang = _controller.EffectiveLanguage;
        SyntaxTheme theme = _currentTheme.Syntax.WithOverrides(lang.ColorOverrides);
        _largeViewer.SetHighlighting(lang.Highlighter, theme);
        _largeViewer.SetCompletionWords(lang.CompletionWords);
        ApplyLanguageFont(lang);
    }

    private string? _fontLanguageId;
    private bool _hasLanguageFont;

    /// <summary>Open the user's languages.json for editing (creating the documented default if needed).</summary>
    private void OpenLanguageProfiles()
    {
        _profileStore.EnsureDefaultFile();
        OpenFromFolderView(_profileStore.Path);
    }

    /// <summary>Re-read languages.json and re-apply syntax/colours/font to the current document.</summary>
    private void ReloadLanguageProfiles()
    {
        if (_catalog is LanguageCatalog catalog)
        {
            catalog.ReloadProfiles(_profileStore.Load());
            _fontLanguageId = null; // force font/colour re-application even if the id is unchanged
            SyncViewerHighlighting();
            _dialogs.ShowInfo("Language profiles reloaded.", "PurePad");
        }
    }

    /// <summary>Apply a profile-defined font when a file of that type is shown; restore the global font otherwise.</summary>
    private void ApplyLanguageFont(LanguageDefinition lang)
    {
        if (lang.Id == _fontLanguageId)
        {
            return; // only act when the language actually changes
        }

        _fontLanguageId = lang.Id;

        if (lang.Font is { } f && IsFontInstalled(f.Family))
        {
            _largeViewer.ApplyFont(new Font(f.Family, f.Size, f.Style));
            _hasLanguageFont = true;
        }
        else if (_hasLanguageFont)
        {
            _largeViewer.ApplyFont(_baseEditorFont); // restore the global font when leaving a profile font
            _hasLanguageFont = false;
        }
    }

    private static bool IsFontInstalled(string family)
    {
        try
        {
            using var probe = new FontFamily(family);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // ----- Recent files ---------------------------------------------------

    /// <summary>Move <paramref name="path"/> to the top of the recent list and persist.</summary>
    private void AddRecentFile(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return;
        }

        _recentFiles.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
        _recentFiles.Insert(0, full);
        if (_recentFiles.Count > MaxRecentFiles)
        {
            _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
        }

        PersistSettings();
        UpdateJumpList();
    }

    /// <summary>Rebuild the taskbar Jump List's Recent category from the current list.</summary>
    private void UpdateJumpList()
    {
        string? exe = Environment.ProcessPath;
        if (exe is not null)
        {
            TaskbarJumpList.Update(_recentFiles, exe);
        }
    }

    /// <summary>Open a file chosen from the recent list, routing large files to the viewer.</summary>
    private void OpenRecentFile(string path)
    {
        if (!File.Exists(path))
        {
            _recentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            PersistSettings();
            UpdateJumpList();
            _dialogs.ShowError($"The file no longer exists:\n{path}");
            return;
        }

        if (ConfirmDiscard())
        {
            _ = _controller.OpenPathAsync(path);
        }
    }

    /// <summary>Rebuild the Recent Files submenu items from the current list.</summary>
    private void RebuildRecentMenu()
    {
        _recentMenu.DropDownItems.Clear();

        if (_recentFiles.Count == 0)
        {
            _recentMenu.DropDownItems.Add(new ToolStripMenuItem("(none)") { Enabled = false });
            return;
        }

        int index = 1;
        foreach (string path in _recentFiles)
        {
            string captured = path;
            string label = $"&{index} {Path.GetFileName(path)}";
            var item = new ToolStripMenuItem(label, null, (s, e) => OpenRecentFile(captured))
            {
                ToolTipText = path,
            };
            _recentMenu.DropDownItems.Add(item);
            index++;
        }

        _recentMenu.DropDownItems.Add(new ToolStripSeparator());
        _recentMenu.DropDownItems.Add(new ToolStripMenuItem("&Clear Recent Files", null, (s, e) =>
        {
            _recentFiles.Clear();
            PersistSettings();
            TaskbarJumpList.Clear();
        }));
    }

    /// <summary>Prompt to save unsaved changes before discarding the document. Returns false if cancelled.</summary>
    private bool ConfirmDiscard()
    {
        if (!_editor.Modified || _controller.SuppressSavePrompts)
        {
            return true;
        }

        return _dialogs.ConfirmSaveChanges(_document.DisplayName) switch
        {
            SaveChangesResponse.Save => SaveActiveDocument(),
            SaveChangesResponse.Discard => true,
            _ => false,
        };
    }

    /// <summary>
    /// Save the edited large file: stream the piece table to a temp file, release the memory
    /// map, atomically replace the original, then re-open it fresh for continued editing.
    /// </summary>
    private bool SaveLargeFile()
    {
        if (_largeDoc is null || _largeViewer.FilePath is not { } path)
        {
            return false;
        }

        string temp = path + ".purepad.tmp";
        System.Text.Encoding encoding = _largeDoc.Encoding;

        // BeginProgress (not OnLoadStarted) so we do NOT switch out of viewer mode / dispose the
        // memory map that WriteTo is about to read from.
        BeginProgress("Saving…");
        _cancelLabel.Visible = false;
        try
        {
            using var writer = new StreamWriter(temp, append: false, encoding);
            _largeViewer.WriteTo(writer, new Progress<double>(SaveProgress));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UseWaitCursor = false;
            OnLoadCompleted();
            _dialogs.ShowError($"Cannot save '{Path.GetFileName(path)}'.\n\n{ex.Message}");
            TryDelete(temp);
            return false;
        }

        // Release the memory map so the original file can be replaced.
        _largeViewer.Clear();
        _largeDoc.Dispose();
        _largeDoc = null;

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temp, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UseWaitCursor = false;
            OnLoadCompleted();
            _dialogs.ShowError($"Cannot replace '{Path.GetFileName(path)}'.\n\n{ex.Message}");
            return false;
        }

        UseWaitCursor = false;
        OnLoadCompleted();
        _ = OpenLargeFileAsync(path); // reopen fresh (clean, re-indexed)
        return true;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }

    /// <summary>Save the current document: a memory-mapped file streams; an in-memory one writes directly.</summary>
    private bool SaveActiveDocument()
    {
        if (_largeDoc is not null)
        {
            if (!_largeViewer.IsEditable)
            {
                _dialogs.ShowInfo("This file is open read-only and cannot be saved.", "PurePad");
                return false;
            }

            return SaveLargeFile();
        }

        return _controller.Save();
    }

    private void OnCheckTimerTick(object? sender, EventArgs e)
    {
        _checkTimer.Stop();

        if (LspOwnsDiagnostics)
        {
            if (_lspDirty && _lspActivePath is { } path)
            {
                _lsp!.ChangeDocument(path, _editor.Text); // server re-publishes diagnostics
                _lspDirty = false;
            }

            return;
        }

        _controller.CheckDocument();
    }

    private void OnDiagnosticActivated(object? sender, Diagnostic diagnostic)
    {
        int line = Math.Min(diagnostic.Line, _editor.LineCount);
        int charIndex = _editor.GetFirstCharIndexOfLine(line - 1);
        if (charIndex >= 0)
        {
            _editor.Select(charIndex + Math.Max(0, diagnostic.Column - 1), 0);
            _editor.ScrollToCaret();
            _largeViewer.Focus();
        }
    }

    // ----- Theming --------------------------------------------------------

    private void ApplyTheme(Theme theme)
    {
        _currentTheme = theme;
        ThemePalette palette = theme.Palette;

        BackColor = palette.ChromeBackground;
        _diagnostics.ApplyTheme(palette);
        _folderView.ApplyTheme(palette);
        _largeViewer.ApplyColors(palette.EditorBackground, palette.EditorForeground, palette.GutterBackground, palette.GutterForeground, palette.Accent);
        _largeViewer.UpdateSyntaxTheme(theme.Syntax.WithOverrides(_controller?.EffectiveLanguage.ColorOverrides));

        ApplyChromeRenderer(_menu, palette);
        ApplyChromeRenderer(_statusStrip, palette);
        ApplyChromeRenderer(_editorContextMenu, palette);
        _menu.BackColor = palette.ChromeBackground;
        _menu.ForeColor = palette.ChromeForeground;
        _statusStrip.BackColor = palette.ChromeBackground;
        _statusStrip.ForeColor = palette.ChromeForeground;

        NativeDarkMode.EnableApplicationDarkMode(palette.IsDark);

        if (IsHandleCreated)
        {
            WindowChrome.Apply(Handle, palette);
        }

        ReThemeOpenDialogs(palette);
    }

    /// <summary>Re-skin any modeless dialogs that are currently open when the theme changes.</summary>
    private void ReThemeOpenDialogs(ThemePalette palette)
    {
        if (_findDialog is { Visible: true })
        {
            DialogThemer.Apply(_findDialog, palette);
        }

        if (_replaceDialog is { Visible: true })
        {
            DialogThemer.Apply(_replaceDialog, palette);
        }
    }

    private static void ApplyChromeRenderer(ToolStrip strip, ThemePalette palette)
    {
        if (palette.IsDark)
        {
            strip.RenderMode = ToolStripRenderMode.Professional;
            strip.Renderer = new ThemedToolStripRenderer(palette);
        }
        else
        {
            // Light theme keeps the authentic Vista system renderer.
            strip.RenderMode = ToolStripRenderMode.System;
        }
    }

    // ----- Settings persistence -------------------------------------------

    /// <summary>Apply saved preferences on startup.</summary>
    private void ApplySettings(AppSettings settings)
    {
        try
        {
            _baseEditorFont = new Font(settings.FontFamily, settings.FontSize, settings.FontStyle);
            _largeViewer.ApplyFont(_baseEditorFont);
        }
        catch (ArgumentException)
        {
            // Saved font is unavailable on this machine; keep the default.
        }

        _largeViewer.Zoom(settings.EditorZoomSteps); // restore saved zoom on top of the base font

        _statusBarRequested = settings.StatusBarVisible;
        _controller.AutoFormatOnSave = settings.AutoFormatOnSave;
        _largeViewer.StickyScrollEnabled = settings.StickyScrollVisible;
        _largeViewer.TextAntialiasing = settings.EditorAntialias;

        _recentFiles.Clear();
        _recentFiles.AddRange(settings.RecentFiles.Take(MaxRecentFiles));

        if (settings.FolderViewVisible && settings.FolderPath is { } folder && Directory.Exists(folder))
        {
            _folderView.SetRoot(folder);
            SetFolderViewVisible(true);
        }

        _diagnostics.Visible = settings.ProblemsPanelVisible;
        if (settings.ProblemsPanelVisible)
        {
            _diagnostics.Height = 140;
        }

        ApplyTheme(_currentTheme);
        ApplySyntaxModeSetting(settings);
        ApplyWindowBounds(settings);
    }

    private void ApplySyntaxModeSetting(AppSettings settings)
    {
        switch (settings.SyntaxMode)
        {
            case "off":
                _controller.SetSyntaxOff();
                break;
            case "forced":
                LanguageDefinition language = _catalog.ResolveById(settings.ForcedLanguageId);
                if (!ReferenceEquals(language, _catalog.PlainText))
                {
                    _controller.ForceLanguage(language);
                }

                break;
            // "auto" is the controller's default.
        }
    }

    private void ApplyWindowBounds(AppSettings settings)
    {
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Size = new Size(settings.WindowWidth, settings.WindowHeight);

            var location = new Point(settings.WindowX, settings.WindowY);
            if (settings.WindowX >= 0 && settings.WindowY >= 0 && IsOnScreen(location))
            {
                StartPosition = FormStartPosition.Manual;
                Location = location;
            }
        }

        if (settings.Maximized)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private static bool IsOnScreen(Point location) =>
        Screen.AllScreens.Any(screen => screen.WorkingArea.Contains(location));

    /// <summary>Persist the current state immediately (after a user-driven change).</summary>
    private void PersistSettings()
    {
        if (_readyToPersist)
        {
            _settingsStore.Save(CaptureSettings());
        }
    }

    /// <summary>Snapshot the current UI state for persistence.</summary>
    private AppSettings CaptureSettings()
    {
        bool maximized = WindowState == FormWindowState.Maximized;
        Rectangle bounds = maximized || WindowState == FormWindowState.Minimized ? RestoreBounds : Bounds;

        return new AppSettings
        {
            ThemeId = _currentTheme.Id,
            FontFamily = _largeViewer.Font.FontFamily.Name,
            FontSize = _largeViewer.BaseFontSize, // base font, without the zoom offset
            FontStyle = _largeViewer.Font.Style,
            EditorZoomSteps = _largeViewer.ZoomSteps,
            StatusBarVisible = _statusBarRequested,
            FolderViewVisible = _folderView.Visible,
            FolderPath = _folderView.RootPath,
            RecentFiles = new List<string>(_recentFiles),
            ProblemsPanelVisible = _diagnostics.Visible,
            StickyScrollVisible = _largeViewer.StickyScrollEnabled,
            EditorAntialias = _largeViewer.TextAntialiasing,
            AutoFormatOnSave = _controller.AutoFormatOnSave,
            SyntaxMode = _controller.SyntaxMode switch
            {
                App.SyntaxMode.Off => "off",
                App.SyntaxMode.Forced => "forced",
                _ => "auto",
            },
            ForcedLanguageId = _controller.SyntaxMode == App.SyntaxMode.Forced ? _controller.ForcedLanguage.Id : null,
            WindowX = bounds.X,
            WindowY = bounds.Y,
            WindowWidth = bounds.Width,
            WindowHeight = bounds.Height,
            Maximized = maximized,
        };
    }

    // ----- Title & status -------------------------------------------------

    private void UpdateTitle()
    {
        Text = $"{_document.DisplayName} - Notepad";
    }

    private void UpdateCaretPosition()
    {
        CaretPosition position = _editor.GetCaretPosition();
        _caretLabel.Text = $"Ln {position.Line}, Col {position.Column}";
    }
}
