using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Generates PurePad's application icon: a material tile with a document card and
// syntax-coloured lines, rendered crisply at each native size and packed into a
// multi-resolution .ico plus a 256px PNG preview.

string outIco = args.Length > 0 ? args[0] : "app.ico";
int[] sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };

var pngs = new Dictionary<int, byte[]>();
foreach (int size in sizes)
{
    pngs[size] = RenderPng(size);
}

WriteIco(outIco, sizes, pngs);
File.WriteAllBytes(Path.ChangeExtension(outIco, null) + "-256.png", pngs[256]);
Console.WriteLine($"Wrote {outIco} ({new FileInfo(outIco).Length} bytes) and preview PNG.");

static byte[] RenderPng(int size)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float s = size;
        float margin = s * 0.055f;
        var tile = new RectangleF(margin, margin, s - 2 * margin, s - 2 * margin);
        float tileRad = s * 0.20f;

        // Material tile: indigo -> blue diagonal gradient with a soft top highlight.
        using (var tilePath = Round(tile, tileRad))
        {
            using var grad = new LinearGradientBrush(
                new RectangleF(tile.X, tile.Y, tile.Width, tile.Height),
                Color.FromArgb(63, 81, 181), Color.FromArgb(33, 150, 243), 55f);
            g.FillPath(grad, tilePath);

            var clip = g.Clip;
            g.SetClip(tilePath);
            using var gloss = new LinearGradientBrush(
                new RectangleF(tile.X, tile.Y - 1, tile.Width, tile.Height * 0.55f + 1),
                Color.FromArgb(48, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f);
            g.FillRectangle(gloss, tile.X, tile.Y, tile.Width, tile.Height * 0.55f);
            g.Clip = clip;
        }

        // Paper card.
        float pw = tile.Width * 0.52f;
        float ph = tile.Height * 0.60f;
        float px = tile.X + (tile.Width - pw) / 2f;
        float py = tile.Y + (tile.Height - ph) / 2f;
        var paper = new RectangleF(px, py, pw, ph);
        float paperRad = Math.Max(1f, s * 0.045f);
        float fold = pw * 0.30f;

        if (size >= 24)
        {
            using var shadow = Round(new RectangleF(px, py + s * 0.022f, pw, ph), paperRad);
            using var sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
            g.FillPath(sb, shadow);
        }

        using (var white = new SolidBrush(Color.White))
        {
            if (size >= 32)
            {
                float r = paperRad;
                using var body = new GraphicsPath();
                body.AddArc(px, py, r * 2, r * 2, 180, 90);
                body.AddLine(px + fold, py, paper.Right, py + fold);
                body.AddArc(paper.Right - r * 2, paper.Bottom - r * 2, r * 2, r * 2, 0, 90);
                body.AddArc(px, paper.Bottom - r * 2, r * 2, r * 2, 90, 90);
                body.CloseFigure();
                g.FillPath(white, body);

                using var foldBrush = new SolidBrush(Color.FromArgb(206, 216, 224));
                using var foldPath = new GraphicsPath();
                foldPath.AddLine(px + fold, py, paper.Right, py + fold);
                foldPath.AddLine(px + fold, py + fold, px + fold, py);
                foldPath.CloseFigure();
                g.FillPath(foldBrush, foldPath);
            }
            else
            {
                using var rp = Round(paper, paperRad);
                g.FillPath(white, rp);
            }
        }

        // Syntax-coloured lines: the editor's colourising, in miniature.
        Color blue = Color.FromArgb(30, 136, 229);
        Color green = Color.FromArgb(67, 160, 71);
        Color amber = Color.FromArgb(251, 140, 0);
        Color grey = Color.FromArgb(120, 144, 156);

        float lx = px + pw * 0.16f;
        float top = py + ph * (size >= 32 ? 0.34f : 0.26f);
        float lh = ph * 0.085f;
        float gap = lh * 1.35f;
        float maxw = pw * 0.68f;

        Bar(g, lx, top + gap * 0, maxw * 0.85f, lh, blue);
        Bar(g, lx, top + gap * 1, maxw * 0.55f, lh, green);
        Bar(g, lx, top + gap * 2, maxw * 1.00f, lh, amber);
        if (size >= 48)
        {
            Bar(g, lx, top + gap * 3, maxw * 0.45f, lh, grey);
            Bar(g, lx, top + gap * 4, maxw * 0.70f, lh, blue);
        }
    }

    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

static void Bar(Graphics g, float x, float y, float w, float h, Color c)
{
    using var path = Round(new RectangleF(x, y, w, h), h / 2f);
    using var b = new SolidBrush(c);
    g.FillPath(b, path);
}

static GraphicsPath Round(RectangleF r, float rad)
{
    var p = new GraphicsPath();
    if (rad <= 0f)
    {
        p.AddRectangle(r);
        p.CloseFigure();
        return p;
    }

    float d = rad * 2f;
    p.AddArc(r.X, r.Y, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    p.CloseFigure();
    return p;
}

static void WriteIco(string path, int[] sizes, Dictionary<int, byte[]> pngs)
{
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);

    bw.Write((ushort)0);            // reserved
    bw.Write((ushort)1);            // type: icon
    bw.Write((ushort)sizes.Length); // image count

    int offset = 6 + 16 * sizes.Length;
    foreach (int size in sizes)
    {
        byte[] data = pngs[size];
        bw.Write((byte)(size >= 256 ? 0 : size)); // width (0 => 256)
        bw.Write((byte)(size >= 256 ? 0 : size)); // height
        bw.Write((byte)0);   // palette
        bw.Write((byte)0);   // reserved
        bw.Write((ushort)1); // colour planes
        bw.Write((ushort)32);// bits per pixel
        bw.Write((uint)data.Length);
        bw.Write((uint)offset);
        offset += data.Length;
    }

    foreach (int size in sizes)
    {
        bw.Write(pngs[size]);
    }
}
