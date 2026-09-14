using System.Runtime.InteropServices;

namespace PurePad.Theming;

/// <summary>
/// Thin wrapper over the undocumented/uxtheme calls that make native Win32 controls
/// (scrollbars, list headers) render in dark mode. Every call is best-effort: on an OS
/// that lacks the export it silently does nothing, so the app still runs everywhere.
/// </summary>
internal static class NativeDarkMode
{
    private enum AppMode
    {
        Default = 0,
        AllowDark = 1,
        ForceDark = 2,
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? subAppName, string? subIdList);

    // Ordinal 135 in uxtheme.dll: SetPreferredAppMode (Windows 10 1903+).
    [DllImport("uxtheme.dll", EntryPoint = "#135", CharSet = CharSet.Unicode)]
    private static extern int SetPreferredAppMode(int mode);

    /// <summary>Let the process opt into dark theming of common controls.</summary>
    public static void EnableApplicationDarkMode(bool dark)
    {
        try
        {
            SetPreferredAppMode((int)(dark ? AppMode.AllowDark : AppMode.Default));
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }
    }

    /// <summary>Switch a control's scrollbars (and native header, where present) between light and dark.</summary>
    public static void UseExplorerTheme(IntPtr handle, bool dark)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            SetWindowTheme(handle, dark ? "DarkMode_Explorer" : "Explorer", null);
        }
        catch (DllNotFoundException)
        {
        }
    }
}
