using System.Windows.Forms;
using PurePad.App;
using PurePad.Search;
using PurePad.Theming;
using PurePad.Tools;
using PurePad.View.Animation;
using PurePad.View.Dialogs;

namespace PurePad.View;

/// <summary>
/// Handlers for the interactive gestures that need view-level state (modeless dialogs,
/// font, word wrap, printing). File and colourising logic is delegated to the controller;
/// this partial only bridges WinForms widgets to it.
/// </summary>
public sealed partial class MainForm
{
    // ----- Find / Replace / Go To ----------------------------------------

    private void ShowFindDialog()
    {
        _findDialog ??= CreateFindDialog();
        SeedQuery(_findDialog.Query.Length == 0 ? _editor.SelectedText : _findDialog.Query, q => _findDialog.Query = q);
        DialogThemer.Apply(_findDialog, _currentTheme.Palette);
        ShowModeless(_findDialog);
        _findDialog.FocusQuery();
    }

    private FindDialog CreateFindDialog()
    {
        var dialog = new FindDialog();
        dialog.FindNextRequested += (s, request) => ExecuteFind(request);
        return dialog;
    }

    private void ShowReplaceDialog()
    {
        _replaceDialog ??= CreateReplaceDialog();
        SeedQuery(_replaceDialog.Query.Length == 0 ? _editor.SelectedText : _replaceDialog.Query, q => _replaceDialog.Query = q);
        DialogThemer.Apply(_replaceDialog, _currentTheme.Palette);
        ShowModeless(_replaceDialog);
        _replaceDialog.FocusQuery();
    }

    private ReplaceDialog CreateReplaceDialog()
    {
        var dialog = new ReplaceDialog();
        dialog.FindNextRequested += (s, request) => ExecuteFind(request);
        dialog.ReplaceRequested += (s, request) =>
        {
            _lastSearch = request;
            _search.Replace(request);
        };
        dialog.ReplaceAllRequested += (s, request) =>
        {
            _lastSearch = request;
            int replaced = _search.ReplaceAll(request);
            _dialogs.ShowInfo($"Replaced {replaced} occurrence(s).", "PurePad");
        };
        return dialog;
    }

    private void ExecuteFind(SearchRequest request)
    {
        _lastSearch = request;
        if (!_search.FindNext(request))
        {
            _dialogs.ShowInfo($"Cannot find \"{request.Query}\".", "PurePad");
        }
    }

    private void FindNextRepeat()
    {
        if (_lastSearch is null)
        {
            ShowFindDialog();
            return;
        }

        ExecuteFind(_lastSearch);
    }

    private void ShowGoToDialog()
    {
        int currentLine = _editor.GetCaretPosition().Line;
        using var dialog = new GoToDialog(currentLine);
        DialogThemer.Apply(dialog, _currentTheme.Palette);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        int targetLine = Math.Min(dialog.LineNumber, _editor.LineCount);
        int charIndex = _editor.GetFirstCharIndexOfLine(targetLine - 1);
        if (charIndex >= 0)
        {
            _editor.Select(charIndex, 0);
            _editor.ScrollToCaret();
        }
    }

    private void SeedQuery(string value, Action<string> assign)
    {
        if (!value.Contains('\n') && !value.Contains('\r'))
        {
            assign(value);
        }
    }

    private void ShowModeless(Form dialog)
    {
        if (dialog.Visible)
        {
            dialog.Activate();
        }
        else
        {
            dialog.Show(this);
        }
    }

    // ----- Format / View --------------------------------------------------

    private void ToggleStatusBar()
    {
        _statusBarRequested = !_statusBarRequested;
        _statusStrip.Visible = _statusBarRequested;
        if (_statusStrip.Visible)
        {
            UpdateCaretPosition();
        }

        PersistSettings();
    }

    private void ToggleDiagnostics()
    {
        if (_diagnostics.Visible)
        {
            Animator.AnimateInt(_diagnostics.Height, 0, 150, h => _diagnostics.Height = h, () => _diagnostics.Visible = false);
        }
        else
        {
            _diagnostics.Height = 0;
            _diagnostics.Visible = true;
            Animator.AnimateInt(0, 140, 150, h => _diagnostics.Height = h);
            if (_controller.CanCheck)
            {
                _controller.CheckDocument();
            }
        }

        PersistSettings();
    }

    private void ToggleAutoFormat()
    {
        _controller.AutoFormatOnSave = !_controller.AutoFormatOnSave;
        PersistSettings();
    }

    private void CheckSyntaxNow()
    {
        if (!_diagnostics.Visible)
        {
            ToggleDiagnostics();
        }
        else
        {
            _controller.CheckDocument();
        }
    }

    private void SelectTheme(Theme theme)
    {
        ApplyTheme(theme);
        PersistSettings();

        // Gentle settle so the change does not feel abrupt.
        Opacity = 0.7;
        Animator.Animate(160, t => Opacity = 0.7 + 0.3 * t, () => Opacity = 1);
    }

    private void ShowExternalToolsInfo()
    {
        _dialogs.ShowInfo(
            "PurePad can use external formatters and linters (e.g. prettier, clang-format, jsonlint).\n\n" +
            "Configure them in:\n" + ToolConfiguration.DefaultPath + "\n\n" +
            "Each language maps to a \"format\" and/or \"check\" command; the document is piped to " +
            "the tool's standard input and its output is read back. When configured, an external " +
            "tool overrides the built-in one for that language.",
            "External Tools");
    }

    private void ShowFontDialog()
    {
        var chosen = _dialogs.PromptFont(_largeViewer.Font);
        if (chosen is not null)
        {
            _largeViewer.ApplyFont(chosen);
            PersistSettings();
        }
    }

    private void InsertTimeDate()
    {
        string stamp = $"{DateTime.Now:t} {DateTime.Now:d}";
        _editor.InsertText(stamp);
    }

    // ----- Printing & Help ------------------------------------------------

    private void PrintDocument()
    {
        _printer.Print(this, _editor.Text, _document.DisplayName, _largeViewer.Font);
    }

    private void ShowHelp()
    {
        _dialogs.ShowInfo(
            "PurePad is a Notepad-style text editor.\n\n" +
            "Use Format → Syntax Highlighting to colourise JSON, XML, Markdown and source code, " +
            "and Format → Reformat Document (Ctrl+Shift+F) to pretty-print JSON and XML.",
            "PurePad Help");
    }

    private void ShowAbout()
    {
        using var about = new AboutDialog();
        DialogThemer.Apply(about, _currentTheme.Palette);
        about.ShowDialog(this);
    }
}
