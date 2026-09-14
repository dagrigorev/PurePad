using System.Runtime.InteropServices;

namespace PurePad.View;

// Native shell COM types needed to publish a taskbar Jump List. Kept in one file so the
// interop surface is contained; only the members PurePad actually calls are declared.

[ComImport]
[Guid("77f10cf0-3db5-4966-b520-b7c54fd35ed6")]
internal class DestinationList
{
}

[ComImport]
[Guid("2d3468c1-36a7-43b6-ac24-d3f02fd9607a")]
internal class EnumerableObjectCollection
{
}

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLink
{
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("6332debf-87b5-4670-90c0-5e57b408a49e")]
internal interface ICustomDestinationList
{
    void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);

    [PreserveSig]
    int BeginList(out uint cMaxSlots, in Guid riid, out IntPtr ppv);

    [PreserveSig]
    int AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory, [MarshalAs(UnmanagedType.Interface)] IObjectArray poa);

    void AppendKnownCategory(int category);

    [PreserveSig]
    int AddUserTasks([MarshalAs(UnmanagedType.Interface)] IObjectArray poa);

    void CommitList();

    void GetRemovedDestinations(in Guid riid, out IntPtr ppv);

    void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string? pszAppID);

    void AbortList();
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9")]
internal interface IObjectArray
{
    uint GetCount();

    [return: MarshalAs(UnmanagedType.IUnknown)]
    object GetAt(uint index, in Guid riid);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("5632b1a4-e38a-400a-928a-d4cd63230295")]
internal interface IObjectCollection
{
    // IObjectArray
    uint GetCount();

    [return: MarshalAs(UnmanagedType.IUnknown)]
    object GetAt(uint index, in Guid riid);

    // IObjectCollection
    void AddObject([MarshalAs(UnmanagedType.IUnknown)] object pvObject);

    void AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray poaSource);

    void RemoveObjectAt(uint index);

    void Clear();
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLinkW
{
    void GetPath([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
    void Resolve(IntPtr hwnd, uint fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
internal interface IPropertyStore
{
    uint GetCount();
    void GetAt(uint iProp, out PropertyKey pkey);
    void GetValue(ref PropertyKey key, out PropVariant pv);
    void SetValue(ref PropertyKey key, ref PropVariant pv);
    void Commit();
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;

    public PropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    private ushort _vt;
    private readonly ushort _reserved1;
    private readonly ushort _reserved2;
    private readonly ushort _reserved3;
    private IntPtr _pointer;
    private readonly IntPtr _pointer2;

    private const ushort VT_LPWSTR = 31;

    public static PropVariant FromString(string value) => new()
    {
        _vt = VT_LPWSTR,
        _pointer = Marshal.StringToCoTaskMemUni(value),
    };

    /// <summary>Free the unmanaged string this variant owns.</summary>
    public void Clear()
    {
        if (_pointer != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(_pointer);
            _pointer = IntPtr.Zero;
            _vt = 0;
        }
    }
}
