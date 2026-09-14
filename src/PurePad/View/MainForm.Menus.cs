using System.Windows.Forms;
using PurePad.Commands;
using PurePad.Formatting;
using PurePad.Theming;

namespace PurePad.View;

/// <summary>
/// Menu construction and the handlers behind each item. The menu items are bound to
/// <see cref="IApplicationCommand"/> objects so their availability (enabled/greyed) can be
/// refreshed from a single place when a menu opens, exactly as Notepad greys out Undo,
/// Cut, Copy and so on.
/// </summary>
public sealed partial class MainForm
{
    private ToolStripMenuItem _statusBarItem = null!;
    private ToolStripMenuItem _diagnosticsItem = null!;
    private ToolStripMenuItem _folderViewItem = null!;
    private ToolStripMenuItem _autoFormatItem = null!;
    private ContextMenuStrip _editorContextMenu = null!;
    private ToolStripMenuItem _recentMenu = null!;
    private ToolStripMenuItem _syntaxAutoItem = null!;
    private ToolStripMenuItem _syntaxOffItem = null!;
    private readonly List<(ToolStripMenuItem Item, LanguageDefinition Language)> _syntaxLanguageItems = new();
    private readonly List<(ToolStripMenuItem Item, Theme Theme)> _themeItems = new();

    private MenuStrip BuildMenus()
    {
        var menu = new MenuStrip
        {
            RenderMode = ToolStripRenderMode.System, // classic Vista system menu look
            Dock = DockStyle.Top,
        };

        menu.Items.Add(BuildFileMenu());
        menu.Items.Add(BuildEditMenu());
        menu.Items.Add(BuildFormatMenu());
        menu.Items.Add(BuildViewMenu());
        menu.Items.Add(BuildHelpMenu());

        foreach (ToolStripMenuItem top in menu.Items.OfType<ToolStripMenuItem>())
        {
            top.DropDownOpening += (s, e) => RefreshMenuState(top);
        }

        return menu;
    }

    private ToolStripMenuItem BuildFileMenu()
    {
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(Bound("&New", new RelayCommand(() => { if (ConfirmDiscard()) { ReleaseLargeBacking(); _controller.NewDocument(); } }), Keys.Control | Keys.N));
        file.DropDownItems.Add(Bound("&Open...", new RelayCommand(() => { if (ConfirmDiscard()) _ = _controller.OpenWithDialogAsync(); }), Keys.Control | Keys.O));
        file.DropDownItems.Add(Bound("Open Fol&der...", new RelayCommand(OpenFolder), Keys.Control | Keys.K));
        file.DropDownItems.Add(Bound("&Save", new RelayCommand(() => SaveActiveDocument()), Keys.Control | Keys.S));
        file.DropDownItems.Add(Bound("Save &As...", new RelayCommand(() => { if (_largeDoc is not null) SaveLargeFile(); else _controller.SaveAs(); }), Keys.Control | Keys.Shift | Keys.S));
        file.DropDownItems.Add(new ToolStripSeparator());
        _recentMenu = new ToolStripMenuItem("Recent Fil&es");
        _recentMenu.DropDownOpening += (s, e) => RebuildRecentMenu();
        file.DropDownItems.Add(_recentMenu);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Bound("Page Set&up...", new RelayCommand(() => _printer.ShowPageSetup(this))));
        file.DropDownItems.Add(Bound("&Print...", new RelayCommand(PrintDocument), Keys.Control | Keys.P));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Bound("E&xit", new RelayCommand(Close)));
        return file;
    }

    private ToolStripMenuItem BuildEditMenu()
    {
        var edit = new ToolStripMenuItem("&Edit");
        edit.DropDownItems.Add(Bound("&Undo", new RelayCommand(_editor.Undo, () => _editor.CanUndo), Keys.Control | Keys.Z));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Bound("Cu&t", new RelayCommand(_editor.Cut, () => _editor.HasSelection), Keys.Control | Keys.X));
        edit.DropDownItems.Add(Bound("&Copy", new RelayCommand(_editor.Copy, () => _editor.HasSelection), Keys.Control | Keys.C));
        edit.DropDownItems.Add(Bound("&Paste", new RelayCommand(_editor.Paste, () => _editor.CanPaste), Keys.Control | Keys.V));
        edit.DropDownItems.Add(Bound("De&lete", new RelayCommand(_editor.DeleteSelection, () => _editor.HasSelection), Keys.Delete));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Bound("&Find...", new RelayCommand(ShowFindDialog, () => _editor.TextLength > 0), Keys.Control | Keys.F));
        edit.DropDownItems.Add(Bound("Find &Next", new RelayCommand(FindNextRepeat, () => _editor.TextLength > 0), Keys.F3));
        edit.DropDownItems.Add(Bound("&Replace...", new RelayCommand(ShowReplaceDialog, () => _editor.TextLength > 0), Keys.Control | Keys.H));
        edit.DropDownItems.Add(Bound("&Go To...", new RelayCommand(ShowGoToDialog), Keys.Control | Keys.G));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Bound("Select &All", new RelayCommand(_editor.SelectAll), Keys.Control | Keys.A));
        edit.DropDownItems.Add(Bound("Time/&Date", new RelayCommand(InsertTimeDate), Keys.F5));
        return edit;
    }

    private ToolStripMenuItem BuildFormatMenu()
    {
        var format = new ToolStripMenuItem("F&ormat");

        format.DropDownItems.Add(Bound("&Font...", new RelayCommand(ShowFontDialog)));
        format.DropDownItems.Add(new ToolStripSeparator());
        format.DropDownItems.Add(BuildSyntaxMenu());
        format.DropDownItems.Add(Bound("&Reformat Document", new RelayCommand(_controller.ReformatDocument, () => _controller.CanReformat), Keys.Control | Keys.Shift | Keys.F));
        format.DropDownItems.Add(Bound("&Check Syntax", new RelayCommand(CheckSyntaxNow, () => _controller.CanCheck), Keys.Control | Keys.Shift | Keys.C));
        _autoFormatItem = new ToolStripMenuItem("Auto-format on &Save", null, (s, e) => ToggleAutoFormat());
        format.DropDownItems.Add(_autoFormatItem);
        format.DropDownItems.Add(new ToolStripSeparator());
        format.DropDownItems.Add(Bound("External &Tools...", new RelayCommand(ShowExternalToolsInfo)));

        return format;
    }

    private ToolStripMenuItem BuildSyntaxMenu()
    {
        var syntax = new ToolStripMenuItem("&Syntax Highlighting");

        _syntaxAutoItem = new ToolStripMenuItem("&Auto-detect", null, (s, e) => _controller.SetSyntaxAuto());
        syntax.DropDownItems.Add(_syntaxAutoItem);
        syntax.DropDownItems.Add(new ToolStripSeparator());

        foreach (LanguageDefinition language in _catalog.Languages)
        {
            LanguageDefinition captured = language;
            var item = new ToolStripMenuItem(language.DisplayName, null, (s, e) => _controller.ForceLanguage(captured));
            _syntaxLanguageItems.Add((item, captured));
            syntax.DropDownItems.Add(item);
        }

        syntax.DropDownItems.Add(new ToolStripSeparator());
        _syntaxOffItem = new ToolStripMenuItem("&Off (Plain Text)", null, (s, e) => _controller.SetSyntaxOff());
        syntax.DropDownItems.Add(_syntaxOffItem);

        syntax.DropDownOpening += (s, e) => RefreshSyntaxChecks();
        return syntax;
    }

    private ToolStripMenuItem BuildViewMenu()
    {
        var view = new ToolStripMenuItem("&View");
        _statusBarItem = new ToolStripMenuItem("&Status Bar", null, (s, e) => ToggleStatusBar());
        _diagnosticsItem = new ToolStripMenuItem("&Problems Panel", null, (s, e) => ToggleDiagnostics());
        _folderViewItem = new ToolStripMenuItem("&Folder View", null, (s, e) => ToggleFolderView());
        view.DropDownItems.Add(_folderViewItem);
        view.DropDownItems.Add(_statusBarItem);
        view.DropDownItems.Add(_diagnosticsItem);
        view.DropDownItems.Add(new ToolStripSeparator());
        view.DropDownItems.Add(BuildThemeMenu());
        return view;
    }

    private ToolStripMenuItem BuildThemeMenu()
    {
        var themeMenu = new ToolStripMenuItem("&Theme");
        foreach (Theme theme in _themes.Themes)
        {
            Theme captured = theme;
            var item = new ToolStripMenuItem(theme.DisplayName, null, (s, e) => SelectTheme(captured));
            _themeItems.Add((item, captured));
            themeMenu.DropDownItems.Add(item);
        }

        themeMenu.DropDownOpening += (s, e) => RefreshThemeChecks();
        return themeMenu;
    }

    private ToolStripMenuItem BuildHelpMenu()
    {
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(Bound("View &Help", new RelayCommand(ShowHelp)));
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add(Bound("&About Notepad", new RelayCommand(ShowAbout)));
        return help;
    }

    /// <summary>Right-click menu for the editor, reusing the same commands as the main menu.</summary>
    private ContextMenuStrip BuildEditorContextMenu()
    {
        var menu = new ContextMenuStrip { RenderMode = ToolStripRenderMode.System };

        var undo = new ToolStripMenuItem("&Undo", null, (s, e) => _editor.Undo());
        var cut = new ToolStripMenuItem("Cu&t", null, (s, e) => _editor.Cut());
        var copy = new ToolStripMenuItem("&Copy", null, (s, e) => _editor.Copy());
        var paste = new ToolStripMenuItem("&Paste", null, (s, e) => _editor.Paste());
        var delete = new ToolStripMenuItem("&Delete", null, (s, e) => _editor.DeleteSelection());
        var selectAll = new ToolStripMenuItem("Select &All", null, (s, e) => _editor.SelectAll());
        var reformat = new ToolStripMenuItem("&Reformat Document", null, (s, e) => _controller.ReformatDocument());
        var check = new ToolStripMenuItem("Check &Syntax", null, (s, e) => CheckSyntaxNow());

        menu.Items.AddRange(new ToolStripItem[]
        {
            undo, new ToolStripSeparator(),
            cut, copy, paste, delete, new ToolStripSeparator(),
            selectAll, new ToolStripSeparator(),
            reformat, check,
        });

        menu.Opening += (s, e) =>
        {
            undo.Enabled = _editor.CanUndo;
            cut.Enabled = _editor.HasSelection;
            copy.Enabled = _editor.HasSelection;
            paste.Enabled = _editor.CanPaste;
            delete.Enabled = _editor.HasSelection;
            reformat.Enabled = _controller.CanReformat;
            check.Enabled = _controller.CanCheck;
        };

        return menu;
    }

    // ----- Command binding helpers ---------------------------------------

    private ToolStripMenuItem Bound(string text, IApplicationCommand command, Keys shortcut = Keys.None)
    {
        var item = new ToolStripMenuItem(text, null, (s, e) => command.Execute());
        if (shortcut != Keys.None)
        {
            item.ShortcutKeys = shortcut;
        }

        _commandBindings[item] = command;
        return item;
    }

    /// <summary>Refresh enabled state and checkmarks for one top-level menu as it opens.</summary>
    private void RefreshMenuState(ToolStripMenuItem topLevel)
    {
        foreach (ToolStripItem item in topLevel.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem && _commandBindings.TryGetValue(menuItem, out var command))
            {
                menuItem.Enabled = command.CanExecute();
            }
        }

        _statusBarItem.Checked = _statusStrip.Visible;
        _folderViewItem.Checked = _folderView.Visible;
        _recentMenu.Enabled = _recentFiles.Count > 0;
        _diagnosticsItem.Checked = _diagnostics.Visible;
        _autoFormatItem.Checked = _controller.AutoFormatOnSave;
    }

    private void RefreshThemeChecks()
    {
        foreach ((ToolStripMenuItem item, Theme theme) in _themeItems)
        {
            item.Checked = ReferenceEquals(_currentTheme, theme);
        }
    }

    private void RefreshSyntaxChecks()
    {
        _syntaxAutoItem.Checked = _controller.SyntaxMode == App.SyntaxMode.Auto;
        _syntaxOffItem.Checked = _controller.SyntaxMode == App.SyntaxMode.Off;

        foreach ((ToolStripMenuItem item, LanguageDefinition language) in _syntaxLanguageItems)
        {
            item.Checked = _controller.SyntaxMode == App.SyntaxMode.Forced &&
                           ReferenceEquals(_controller.ForcedLanguage, language);
        }
    }
}
