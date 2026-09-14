using System.Windows.Forms;
using PurePad.Configuration;
using PurePad.Formatting;
using PurePad.Services;
using PurePad.Theming;
using PurePad.Tools;
using PurePad.View;

namespace PurePad;

/// <summary>
/// Application entry point and composition root. It enables Vista visual styles, wires the
/// framework-agnostic services (file access, language catalogue) and hands them to the main
/// window, which assembles the UI-bound collaborators. Keeping construction in one place
/// makes the dependency graph explicit and swappable (Dependency Inversion).
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Stable identity so the taskbar button and its Jump List group under one app.
        TrySetAppUserModelId("PurePad.Editor");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // Log otherwise-unhandled exceptions so failures are diagnosable.
        Application.ThreadException += (s, e) => LogUnhandled(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (s, e) => LogUnhandled(e.ExceptionObject as Exception);

        IFileService fileService = new FileService();
        ILanguageCatalog catalog = new LanguageCatalog();
        IThemeCatalog themes = new ThemeCatalog();

        ToolConfiguration toolConfig = ToolConfiguration.LoadOrEmpty(ToolConfiguration.DefaultPath);
        var externalTools = new ExternalToolCatalog(toolConfig, new ExternalToolRunner());

        ISettingsStore settingsStore = new JsonSettingsStore(JsonSettingsStore.DefaultPath);
        AppSettings settings = settingsStore.Load();

        var form = new MainForm(fileService, catalog, themes, externalTools, settingsStore, settings);

        (string? openPath, string? screenshotPath, bool noPrompts) = ParseArgs(args);
        if (openPath is not null)
        {
            form.OpenOnStartup(openPath);
        }

        if (noPrompts)
        {
            form.SetSuppressSavePrompts(true);
        }

        if (screenshotPath is not null)
        {
            form.ScheduleScreenshot(screenshotPath);
        }

        foreach (string arg in args)
        {
            if (arg.StartsWith("--goto=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg["--goto=".Length..], out int line))
            {
                form.DebugGotoLine = line;
            }
            else if (arg.StartsWith("--caret-col=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg["--caret-col=".Length..], out int col))
            {
                form.DebugCaretColumn = col;
            }
            else if (arg.StartsWith("--caret-line=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg["--caret-line=".Length..], out int cline))
            {
                form.DebugCaretLine = cline;
            }
            else if (arg.StartsWith("--zoom=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg["--zoom=".Length..], out int zoom))
            {
                form.DebugZoomSteps = zoom;
            }
            else if (arg.StartsWith("--fold=", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string part in arg["--fold=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part, out int fl))
                    {
                        form.DebugFoldLines.Add(fl);
                    }
                }
            }
        }

        Application.Run(form);
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string appId);

    private static void TrySetAppUserModelId(string appId)
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(appId);
        }
        catch
        {
            // Non-critical: only affects taskbar grouping / Jump List.
        }
    }

    private static void LogUnhandled(Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

        try
        {
            string path = Path.Combine(Path.GetTempPath(), "purepad-error.log");
            File.AppendAllText(path, $"{DateTime.Now:O}\n{ex}\n\n");
        }
        catch
        {
            // Never let logging throw.
        }
    }

    /// <summary>
    /// Parse the command line: a positional path opens a file; <c>--screenshot[=path]</c>
    /// enables the debug capture hook (defaulting to purepad-screenshot.png in the cwd).
    /// </summary>
    private static (string? OpenPath, string? ScreenshotPath, bool NoPrompts) ParseArgs(string[] args)
    {
        string? openPath = null;
        string? screenshotPath = null;
        bool screenshot = false;
        bool noPrompts = false;

        foreach (string arg in args)
        {
            if (arg.Equals("--screenshot", StringComparison.OrdinalIgnoreCase))
            {
                screenshot = true;
            }
            else if (arg.StartsWith("--screenshot=", StringComparison.OrdinalIgnoreCase))
            {
                screenshot = true;
                screenshotPath = arg["--screenshot=".Length..].Trim('"');
            }
            else if (arg.Equals("--no-prompts", StringComparison.OrdinalIgnoreCase))
            {
                noPrompts = true;
            }
            else if (!arg.StartsWith("--", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(arg))
            {
                openPath = arg;
            }
        }

        if (screenshot && screenshotPath is null)
        {
            screenshotPath = Path.Combine(Environment.CurrentDirectory, "purepad-screenshot.png");
        }

        return (openPath, screenshotPath, noPrompts);
    }
}
