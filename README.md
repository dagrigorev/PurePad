# PurePad

A faithful, from-scratch clone of **Windows Vista Notepad**, written in C# / WinForms,
plus a **file-format colourising and reformatting** feature layered cleanly on top.

The interface mirrors Vista Notepad exactly — the same five menus (File, Edit, Format,
View, Help), the same items, shortcuts, status bar, word-wrap behaviour and the classic
system-rendered grey menus — while a small **Format → Syntax Highlighting** submenu and a
**Reformat Document** command add the new capability without disturbing the original layout.

## Requirements

- Windows
- .NET SDK 8.0 or later (the repo targets `net8.0-windows`)

## Build & run

```powershell
# from the repository root
dotnet build PurePad.sln -c Release

# run it (optionally pass a file to open)
dotnet run --project src/PurePad/PurePad.csproj -- samples/sample.json
```

The compiled executable is `src/PurePad/bin/<config>/net8.0-windows/PurePad.exe`.
You can also open the solution in Visual Studio 2022 and press F5.

## The colourising / formatting feature

- **Format → Syntax Highlighting** chooses how the document is coloured:
  - **Auto-detect** — picks the language from the file extension (default).
  - A specific language — **JSON**, **XML / HTML**, **Markdown** or **Source Code**.
  - **Off (Plain Text)** — no colouring.
- **Format → Reformat Document** (`Ctrl+Shift+F`) pretty-prints the current document.
  JSON and XML are re-indented; invalid input is reported rather than corrupted.

Supported out of the box: `.json`; `.xml/.html/.htm/.xaml/.csproj/.config/.svg/.xsd/.resx`;
`.md/.markdown`; and C-family source (`.cs/.js/.ts/.java/.c/.cpp/.css/.py/.go/.rs/...`).

## Line numbers

**View → Line Numbers** toggles a gutter down the left edge that numbers each logical line.
It stays aligned to the text and in sync with typing, scrolling (scrollbar, wheel or
keyboard), font changes and word wrap (wrapped continuation lines share their line's number).

## Architecture

The code is organised by responsibility and follows OOP / SOLID with several design patterns.

| Area | Type(s) | Responsibility |
|------|---------|----------------|
| Entry point | `Program` | Composition root: wires services and starts the app. |
| Domain | `Domain/TextDocument` | File path, encoding and dirty state; raises change events. |
| Editor abstraction | `Editor/ITextEditor`, `RichTextBoxEditor` | Framework-agnostic editing surface (Adapter over `RichTextBox`). |
| Application logic | `App/EditorController` | Mediator for file lifecycle, colourising and formatting. |
| Commands | `Commands/IApplicationCommand`, `RelayCommand` | Menu actions with an availability rule. |
| Services | `Services/IFileService`, `IDialogService` (+ impls) | File I/O with encoding detection; modal dialogs. |
| Search | `Search/SearchService`, `SearchRequest` | Find / Replace / Replace-All logic. |
| Formatting | `Formatting/*` | Highlighters, formatters, theme and language registry. |
| View | `View/MainForm.*`, `View/Dialogs/*`, `View/Printing/*`, `View/Rendering/*` | The window, dialogs, printing and colour rendering. |

### Design patterns used

- **Command** — every menu action is an `IApplicationCommand`; `CanExecute` drives the
  enabled/greyed state of Undo, Cut, Copy, Paste, etc.
- **Strategy** — `ISyntaxHighlighter` and `ITextFormatter` have one implementation per
  language, selected at runtime.
- **Factory / Registry** — `LanguageCatalog` resolves a `LanguageDefinition` (highlighter +
  formatter) by file extension. Adding a language is a single registration (Open/Closed).
- **Null Object** — `PlainTextHighlighter` and `NullFormatter` remove null checks and
  special cases.
- **Observer** — `TextDocument.Changed` lets the window update its title/menus without polling.
- **Adapter** — `RichTextBoxEditor` adapts the WinForms control to `ITextEditor`.
- **Mediator** — `EditorController` coordinates the view, model, services and formatting.
- **Dependency Inversion** — collaborators depend on interfaces; `Program` (and the window
  for UI-bound pieces) compose the concrete graph.

### Why WinForms + RichTextBox

Vista Notepad is a Win32 app built on a plain EDIT control. WinForms with the
`ToolStripSystemRenderer` reproduces the exact system menu/look, and a `RichTextBox` (needed
because a plain textbox cannot show colour) provides the per-range colouring the new feature
requires. Colouring is debounced, painting is suspended during recolour, and the caret/scroll
position is preserved so it is invisible to the user.

## Known limitations

- Save As does not expose the ANSI/Unicode/UTF-8 encoding drop-down that Vista's dialog has;
  instead the file's original encoding (detected from its BOM) is preserved on save, and new
  files are written as UTF-8 without a BOM.
- Colourising is skipped for documents larger than ~300,000 characters to keep typing responsive.
