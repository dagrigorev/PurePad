using System.Runtime.InteropServices;

namespace PurePad.View;

/// <summary>
/// Publishes a "Recent" category to the application's Windows taskbar Jump List, where each
/// entry re-launches PurePad with that file. Uses the native ICustomDestinationList /
/// IShellLink shell API (WinForms has no managed equivalent). All calls are best-effort:
/// on any COM failure the jump list is simply left unchanged.
/// </summary>
internal static class TaskbarJumpList
{
    /// <summary>Rebuild the Recent category from <paramref name="recentFiles"/> (newest first).</summary>
    public static void Update(IReadOnlyList<string> recentFiles, string executablePath)
    {
        try
        {
            var list = (ICustomDestinationList)new DestinationList();
            list.BeginList(out _, typeof(IObjectArray).GUID, out _);

            var collection = (IObjectCollection)new EnumerableObjectCollection();
            foreach (string path in recentFiles)
            {
                if (File.Exists(path))
                {
                    collection.AddObject(CreateShellLink(path, executablePath));
                }
            }

            if (collection.GetCount() > 0)
            {
                list.AppendCategory("Recent", (IObjectArray)collection);
            }

            list.CommitList();
        }
        catch
        {
            // Jump lists are a convenience; never let a shell/COM error surface.
        }
    }

    /// <summary>Clear the app's custom Jump List categories.</summary>
    public static void Clear()
    {
        try
        {
            var list = (ICustomDestinationList)new DestinationList();
            list.DeleteList(null);
        }
        catch
        {
        }
    }

    private static IShellLinkW CreateShellLink(string filePath, string executablePath)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(executablePath);
        link.SetArguments($"\"{filePath}\"");
        link.SetIconLocation(executablePath, 0);
        link.SetDescription(filePath);

        // The text shown in the Jump List comes from System.Title (PKEY_Title).
        var store = (IPropertyStore)link;
        var titleKey = new PropertyKey(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);
        var value = PropVariant.FromString(Path.GetFileName(filePath));
        try
        {
            store.SetValue(ref titleKey, ref value);
            store.Commit();
        }
        finally
        {
            value.Clear();
        }

        return link;
    }
}
