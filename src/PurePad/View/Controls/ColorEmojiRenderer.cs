using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PurePad.View.Controls;

/// <summary>
/// Rasterizes an emoji glyph to a color <see cref="Bitmap"/> using Direct2D + DirectWrite.
/// GDI+/<c>Graphics.DrawString</c> only draws the monochrome outline of a color (COLR) font
/// such as Segoe UI Emoji; Direct2D's <c>DrawText</c> with the color-font option renders the
/// real multi-layer glyph.
/// </summary>
/// <remarks>
/// The COM objects are driven by reading their vtable slots directly rather than through
/// <c>[ComImport]</c> interfaces: the CLR's COM marshaller mishandles the <c>in</c>-struct
/// parameters these methods take (color, rect, render-target properties), corrupting the stack.
/// Binding each method as an explicit stdcall delegate passes those by-reference structs
/// correctly. If any factory is unavailable the renderer reports <see cref="IsAvailable"/> =
/// false so callers can fall back to plain text.
/// </remarks>
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

    // Vtable slot indices (IUnknown occupies 0-2).
    private const int WicCreateBitmapSlot = 17;
    private const int D2DCreateWicBitmapRenderTargetSlot = 13;
    private const int DWriteCreateTextFormatSlot = 15;
    private const int FormatSetTextAlignmentSlot = 3;
    private const int FormatSetParagraphAlignmentSlot = 4;
    private const int RtCreateSolidColorBrushSlot = 8;
    private const int RtDrawTextSlot = 27;
    private const int RtClearSlot = 47;
    private const int RtBeginDrawSlot = 48;
    private const int RtEndDrawSlot = 49;
    private const int BitmapSourceCopyPixelsSlot = 7;

    // ---- interop structs (blittable; passed by reference) ---------------

    [StructLayout(LayoutKind.Sequential)]
    private struct RenderTargetProperties
    {
        public int Type;
        public int PixelFormat;
        public int AlphaMode;
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

    // ---- vtable method delegates ---------------------------------------

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateBitmapDelegate(IntPtr self, int width, int height, in Guid pixelFormat, int cacheOption, out IntPtr bitmap);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateWicRenderTargetDelegate(IntPtr self, IntPtr target, in RenderTargetProperties props, out IntPtr renderTarget);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateTextFormatDelegate(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string fontFamily, IntPtr collection,
        int weight, int style, int stretch, float fontSize, [MarshalAs(UnmanagedType.LPWStr)] string localeName, out IntPtr textFormat);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetInt32Delegate(IntPtr self, int value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateSolidColorBrushDelegate(IntPtr self, in ColorF color, IntPtr brushProps, out IntPtr brush);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void BeginDrawDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ClearDelegate(IntPtr self, IntPtr color);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void DrawTextDelegate(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string text, int length,
        IntPtr textFormat, in RectF layoutRect, IntPtr brush, int options, int measuringMode);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EndDrawDelegate(IntPtr self, IntPtr tag1, IntPtr tag2);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CopyPixelsDelegate(IntPtr self, IntPtr rect, int stride, int bufferSize, byte[] buffer);

    // ---- instance state -------------------------------------------------

    private readonly IntPtr _d2d;
    private readonly IntPtr _dwrite;
    private readonly IntPtr _wic;
    private readonly Dictionary<int, IntPtr> _formatsBySize = new(); // px -> IDWriteTextFormat*

    public ColorEmojiRenderer()
    {
        try
        {
            if (D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, IID_ID2D1Factory, IntPtr.Zero, out _d2d) >= 0 &&
                DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, IID_IDWriteFactory, out _dwrite) >= 0 &&
                CoCreateInstance(CLSID_WICImagingFactory, IntPtr.Zero, CLSCTX_INPROC_SERVER, IID_IWICImagingFactory, out _wic) >= 0)
            {
                IsAvailable = _d2d != IntPtr.Zero && _dwrite != IntPtr.Zero && _wic != IntPtr.Zero;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            IsAvailable = false;
        }
    }

    /// <summary>True when the Direct2D stack initialized and glyphs can be rendered.</summary>
    public bool IsAvailable { get; }

    /// <summary>Bind vtable slot <paramref name="slot"/> of COM object <paramref name="obj"/> as a delegate.</summary>
    private static T Method<T>(IntPtr obj, int slot) where T : Delegate
    {
        IntPtr vtable = Marshal.ReadIntPtr(obj);
        IntPtr function = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(function);
    }

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
            if (Method<CreateBitmapDelegate>(_wic, WicCreateBitmapSlot)(_wic, size, size, GUID_WICPixelFormat32bppPBGRA, WICBitmapCacheOnLoad, out wicBitmap) < 0)
            {
                return null;
            }

            var props = new RenderTargetProperties
            {
                PixelFormat = DXGI_FORMAT_B8G8R8A8_UNORM,
                AlphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED,
                DpiX = 96,
                DpiY = 96,
            };

            if (Method<CreateWicRenderTargetDelegate>(_d2d, D2DCreateWicBitmapRenderTargetSlot)(_d2d, wicBitmap, props, out renderTarget) < 0)
            {
                return null;
            }

            IntPtr format = GetTextFormat(size);
            if (format == IntPtr.Zero)
            {
                return null;
            }

            var black = new ColorF { A = 1 };
            if (Method<CreateSolidColorBrushDelegate>(renderTarget, RtCreateSolidColorBrushSlot)(renderTarget, black, IntPtr.Zero, out brush) < 0)
            {
                return null;
            }

            var rect = new RectF { Right = size, Bottom = size };
            Method<BeginDrawDelegate>(renderTarget, RtBeginDrawSlot)(renderTarget);
            Method<ClearDelegate>(renderTarget, RtClearSlot)(renderTarget, IntPtr.Zero); // transparent
            Method<DrawTextDelegate>(renderTarget, RtDrawTextSlot)(renderTarget, emoji, emoji.Length, format, rect, brush,
                D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT, 0);
            if (Method<EndDrawDelegate>(renderTarget, RtEndDrawSlot)(renderTarget, IntPtr.Zero, IntPtr.Zero) < 0)
            {
                return null;
            }

            return CopyToBitmap(wicBitmap, size);
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

        // Glyph slightly smaller than the cell so its side bearings aren't clipped.
        if (Method<CreateTextFormatDelegate>(_dwrite, DWriteCreateTextFormatSlot)(_dwrite, "Segoe UI Emoji", IntPtr.Zero,
            400, 0, 5, size * 0.82f, "", out IntPtr format) < 0)
        {
            return _formatsBySize[size] = IntPtr.Zero;
        }

        Method<SetInt32Delegate>(format, FormatSetTextAlignmentSlot)(format, DWRITE_TEXT_ALIGNMENT_CENTER);
        Method<SetInt32Delegate>(format, FormatSetParagraphAlignmentSlot)(format, DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
        return _formatsBySize[size] = format;
    }

    private static Bitmap? CopyToBitmap(IntPtr wicBitmap, int size)
    {
        int stride = size * 4;
        var pixels = new byte[stride * size];
        if (Method<CopyPixelsDelegate>(wicBitmap, BitmapSourceCopyPixelsSlot)(wicBitmap, IntPtr.Zero, stride, pixels.Length, pixels) < 0)
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

    public void Dispose()
    {
        foreach (IntPtr format in _formatsBySize.Values)
        {
            if (format != IntPtr.Zero) Marshal.Release(format);
        }

        _formatsBySize.Clear();
        if (_wic != IntPtr.Zero) Marshal.Release(_wic);
        if (_dwrite != IntPtr.Zero) Marshal.Release(_dwrite);
        if (_d2d != IntPtr.Zero) Marshal.Release(_d2d);
    }
}
