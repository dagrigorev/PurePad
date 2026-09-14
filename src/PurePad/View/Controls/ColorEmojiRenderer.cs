using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PurePad.View.Controls;

/// <summary>
/// Rasterizes an emoji glyph to a color <see cref="Bitmap"/> using Direct2D + DirectWrite.
/// GDI+/<c>Graphics.DrawString</c> only draws the monochrome outline of a color (COLR) font
/// such as Segoe UI Emoji; Direct2D's <c>DrawText</c> with the color-font option renders the
/// real multi-layer glyph. The COM stack (D2D, DWrite, WIC factories) is created once and
/// reused. If any part is unavailable the renderer reports <see cref="IsAvailable"/> = false
/// so callers can fall back to plain text.
/// </summary>
internal sealed class ColorEmojiRenderer : IDisposable
{
    // ---- native factory entry points -----------------------------------

    [DllImport("d2d1.dll")]
    private static extern int D2D1CreateFactory(int factoryType, in Guid riid, IntPtr options, out IntPtr factory);

    [DllImport("dwrite.dll")]
    private static extern int DWriteCreateFactory(int factoryType, in Guid iid, out IntPtr factory);

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(in Guid clsid, IntPtr outer, uint clsContext, in Guid iid, out IntPtr instance);

    private static readonly Guid IID_ID2D1Factory = new("06152247-6f50-465a-9245-118bfd3b6007");
    private static readonly Guid IID_IDWriteFactory = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
    private static readonly Guid CLSID_WICImagingFactory = new("cacaf262-9370-4615-a13b-9f5539da4c0a");
    private static readonly Guid IID_IWICImagingFactory = new("ec5ec8a9-c395-4314-9c77-54d7a935ff70");
    private static readonly Guid GUID_WICPixelFormat32bppPBGRA = new("6fddc324-4e03-4bfe-b185-3d77768dc910");

    private const int D2D1_FACTORY_TYPE_SINGLE_THREADED = 0;
    private const int DWRITE_FACTORY_TYPE_SHARED = 0;
    private const uint CLSCTX_INPROC_SERVER = 1;
    private const int WICBitmapCacheOnLoad = 2;
    private const int DXGI_FORMAT_B8G8R8A8_UNORM = 87;
    private const int D2D1_ALPHA_MODE_PREMULTIPLIED = 1;
    private const int D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT = 4;
    private const int DWRITE_TEXT_ALIGNMENT_CENTER = 2;
    private const int DWRITE_PARAGRAPH_ALIGNMENT_CENTER = 2;

    // ---- interop structs -----------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct RenderTargetProperties
    {
        public int Type;
        public int PixelFormat;      // DXGI_FORMAT
        public int AlphaMode;        // D2D1_ALPHA_MODE
        public float DpiX;
        public float DpiY;
        public int Usage;
        public int MinLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ColorF
    {
        public float R, G, B, A;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectF
    {
        public float Left, Top, Right, Bottom;
    }

    // ---- COM interfaces (vtable order matters; unused slots are placeholders) ----

    [ComImport, Guid("06152247-6f50-465a-9245-118bfd3b6007"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID2D1Factory
    {
        void _0(); void _1(); void _2(); void _3(); void _4(); void _5();
        void _6(); void _7(); void _8(); void _9();
        [PreserveSig] int CreateWicBitmapRenderTarget(IntPtr target, in RenderTargetProperties props, out IntPtr renderTarget);
    }

    [ComImport, Guid("2cd90694-12e2-11dc-9fed-001143a055f9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID2D1RenderTarget
    {
        void _s3(); // GetFactory (ID2D1Resource)
        void _s4(); void _s5(); void _s6(); void _s7(); // CreateBitmap..CreateBitmapBrush
        [PreserveSig] int CreateSolidColorBrush(in ColorF color, IntPtr brushProps, out IntPtr brush); // slot 8
        // slots 9..26 (18 methods) up to DrawText
        void _s9(); void _s10(); void _s11(); void _s12(); void _s13(); void _s14();
        void _s15(); void _s16(); void _s17(); void _s18(); void _s19(); void _s20();
        void _s21(); void _s22(); void _s23(); void _s24(); void _s25(); void _s26();
        [PreserveSig] void DrawText([MarshalAs(UnmanagedType.LPWStr)] string text, int length, IntPtr textFormat,
            in RectF layoutRect, IntPtr brush, int options, int measuringMode); // slot 27
        // slots 28..46 (19 methods) up to Clear
        void _s28(); void _s29(); void _s30(); void _s31(); void _s32(); void _s33();
        void _s34(); void _s35(); void _s36(); void _s37(); void _s38(); void _s39();
        void _s40(); void _s41(); void _s42(); void _s43(); void _s44(); void _s45();
        void _s46();
        [PreserveSig] void Clear(IntPtr color);              // slot 47
        [PreserveSig] void BeginDraw();                      // slot 48
        [PreserveSig] int EndDraw(IntPtr tag1, IntPtr tag2); // slot 49
    }

    [ComImport, Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDWriteFactory
    {
        void _0(); void _1(); void _2(); void _3(); void _4(); void _5();
        void _6(); void _7(); void _8(); void _9(); void _10(); void _11();
        [PreserveSig] int CreateTextFormat([MarshalAs(UnmanagedType.LPWStr)] string fontFamily, IntPtr collection,
            int weight, int style, int stretch, float fontSize,
            [MarshalAs(UnmanagedType.LPWStr)] string localeName, out IntPtr textFormat); // slot 15
    }

    [ComImport, Guid("9c906818-31d7-4fd3-a151-7c5e225db55a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDWriteTextFormat
    {
        [PreserveSig] int SetTextAlignment(int alignment);      // slot 3
        [PreserveSig] int SetParagraphAlignment(int alignment); // slot 4
    }

    [ComImport, Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICImagingFactory
    {
        void _0(); void _1(); void _2(); void _3(); void _4(); void _5();
        void _6(); void _7(); void _8(); void _9(); void _10(); void _11();
        void _12(); void _13();
        [PreserveSig] int CreateBitmap(int width, int height, in Guid pixelFormat, int cacheOption, out IntPtr bitmap); // slot 17
    }

    [ComImport, Guid("00000121-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmap
    {
        void _0(); void _1(); void _2(); void _3(); // IWICBitmapSource: GetSize/GetPixelFormat/GetResolution/CopyPalette
        [PreserveSig] int CopyPixels(IntPtr rect, int stride, int bufferSize, byte[] buffer); // slot 7
    }

    // ---- instance state -------------------------------------------------

    private readonly ID2D1Factory? _d2d;
    private readonly IDWriteFactory? _dwrite;
    private readonly IWICImagingFactory? _wic;
    private readonly Dictionary<int, IntPtr> _formatsBySize = new(); // px -> IDWriteTextFormat*

    public ColorEmojiRenderer()
    {
        try
        {
            if (D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, IID_ID2D1Factory, IntPtr.Zero, out IntPtr d2d) >= 0 &&
                DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, IID_IDWriteFactory, out IntPtr dw) >= 0 &&
                CoCreateInstance(CLSID_WICImagingFactory, IntPtr.Zero, CLSCTX_INPROC_SERVER, IID_IWICImagingFactory, out IntPtr wic) >= 0)
            {
                _d2d = (ID2D1Factory)Marshal.GetTypedObjectForIUnknown(d2d, typeof(ID2D1Factory));
                _dwrite = (IDWriteFactory)Marshal.GetTypedObjectForIUnknown(dw, typeof(IDWriteFactory));
                _wic = (IWICImagingFactory)Marshal.GetTypedObjectForIUnknown(wic, typeof(IWICImagingFactory));
                Marshal.Release(d2d);
                Marshal.Release(dw);
                Marshal.Release(wic);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException or InvalidCastException)
        {
            _d2d = null;
        }
    }

    /// <summary>True when the Direct2D stack initialized and glyphs can be rendered.</summary>
    public bool IsAvailable => _d2d is not null && _dwrite is not null && _wic is not null;

    /// <summary>
    /// Render <paramref name="emoji"/> centered in a <paramref name="size"/>×<paramref name="size"/>
    /// transparent bitmap, or null if rendering fails.
    /// </summary>
    public Bitmap? Render(string emoji, int size)
    {
        if (!IsAvailable || size <= 0)
        {
            return null;
        }

        IntPtr wicBitmap = IntPtr.Zero, renderTarget = IntPtr.Zero, brush = IntPtr.Zero;
        try
        {
            if (_wic!.CreateBitmap(size, size, GUID_WICPixelFormat32bppPBGRA, WICBitmapCacheOnLoad, out wicBitmap) < 0)
            {
                return null;
            }

            var props = new RenderTargetProperties
            {
                Type = 0,
                PixelFormat = DXGI_FORMAT_B8G8R8A8_UNORM,
                AlphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED,
                DpiX = 96,
                DpiY = 96,
                Usage = 0,
                MinLevel = 0,
            };

            if (_d2d!.CreateWicBitmapRenderTarget(wicBitmap, props, out renderTarget) < 0)
            {
                return null;
            }

            var rt = (ID2D1RenderTarget)Marshal.GetTypedObjectForIUnknown(renderTarget, typeof(ID2D1RenderTarget));
            var black = new ColorF { R = 0, G = 0, B = 0, A = 1 };
            if (rt.CreateSolidColorBrush(black, IntPtr.Zero, out brush) < 0)
            {
                Marshal.ReleaseComObject(rt);
                return null;
            }

            IntPtr format = GetTextFormat(size);
            var rect = new RectF { Left = 0, Top = 0, Right = size, Bottom = size };

            rt.BeginDraw();
            rt.Clear(IntPtr.Zero); // transparent
            rt.DrawText(emoji, emoji.Length, format, rect, brush, D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT, 0);
            int hr = rt.EndDraw(IntPtr.Zero, IntPtr.Zero);
            Marshal.ReleaseComObject(rt);
            if (hr < 0)
            {
                return null;
            }

            return CopyToBitmap(wicBitmap, size);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (brush != IntPtr.Zero) Marshal.Release(brush);
            if (renderTarget != IntPtr.Zero) Marshal.Release(renderTarget);
            if (wicBitmap != IntPtr.Zero) Marshal.Release(wicBitmap);
        }
    }

    private IntPtr GetTextFormat(int size)
    {
        if (_formatsBySize.TryGetValue(size, out IntPtr cached))
        {
            return cached;
        }

        // Glyph slightly smaller than the cell so it isn't clipped by its side bearings.
        if (_dwrite!.CreateTextFormat("Segoe UI Emoji", IntPtr.Zero, 400, 0, 5, size * 0.82f, "", out IntPtr format) < 0)
        {
            _formatsBySize[size] = IntPtr.Zero;
            return IntPtr.Zero;
        }

        var fmt = (IDWriteTextFormat)Marshal.GetTypedObjectForIUnknown(format, typeof(IDWriteTextFormat));
        fmt.SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
        fmt.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
        Marshal.ReleaseComObject(fmt);

        _formatsBySize[size] = format;
        return format;
    }

    private static Bitmap? CopyToBitmap(IntPtr wicBitmapPtr, int size)
    {
        var source = (IWICBitmap)Marshal.GetTypedObjectForIUnknown(wicBitmapPtr, typeof(IWICBitmap));
        try
        {
            int stride = size * 4;
            var pixels = new byte[stride * size];
            if (source.CopyPixels(IntPtr.Zero, stride, pixels.Length, pixels) < 0)
            {
                return null;
            }

            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < size; y++)
                {
                    Marshal.Copy(pixels, y * stride, data.Scan0 + y * data.Stride, stride);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }
        finally
        {
            Marshal.ReleaseComObject(source);
        }
    }

    public void Dispose()
    {
        foreach (IntPtr format in _formatsBySize.Values)
        {
            if (format != IntPtr.Zero) Marshal.Release(format);
        }

        _formatsBySize.Clear();
        if (_d2d is not null) Marshal.ReleaseComObject(_d2d);
        if (_dwrite is not null) Marshal.ReleaseComObject(_dwrite);
        if (_wic is not null) Marshal.ReleaseComObject(_wic);
    }
}
