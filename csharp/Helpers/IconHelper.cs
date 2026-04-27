using DrawColor = System.Drawing.Color;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ScalanceLogs.Helpers;

/// <summary>
/// Renders the SW-LOG brand icon (rounded square + 3 log bars + 3 dots)
/// and converts it to both a GDI Icon (tray) and a WPF BitmapSource (window).
/// </summary>
public static class IconHelper
{
    // ── Brand colours (fixed – not theme-dependent) ─────────────────────────────
    private static readonly DrawColor BrandBg  = DrawColor.FromArgb(0xFF, 0x16, 0x1b, 0x26);
    private static readonly DrawColor BarFull  = DrawColor.FromArgb(0xFF, 0x3b, 0x82, 0xf6);
    private static readonly DrawColor BarMid   = DrawColor.FromArgb(0x88, 0x3b, 0x82, 0xf6);
    private static readonly DrawColor BarLight = DrawColor.FromArgb(0x44, 0x3b, 0x82, 0xf6);
    private static readonly DrawColor DotColor = DrawColor.FromArgb(0x99, 0x3b, 0x82, 0xf6);

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>GDI Icon – use for tray / WinForms.</summary>
    public static Icon CreateBrandIcon(int size = 32)
    {
        using var bmp = DrawBrand(size);
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>WPF BitmapSource – use for Window.Icon.</summary>
    public static System.Windows.Media.Imaging.BitmapSource CreateBrandBitmapSource(int size = 32)
    {
        using var bmp  = DrawBrand(size);
        var       hbmp = bmp.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hbmp, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hbmp);
        }
    }

    // ── Drawing ───────────────────────────────────────────────────────────────────

    private static Bitmap DrawBrand(int size)
    {
        // All geometry is defined in a virtual 80×80 canvas then scaled.
        float s = size / 80f;

        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.CompositingMode    = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.Clear(DrawColor.Transparent);

        // ── Background ────────────────────────────────────────────────────────
        float radius = Math.Max(1f, 18f * s);
        FillRoundedRect(g, BrandBg, 0, 0, size, size, radius);

        // ── Log bars  (x=14, h=8, spaced 14px apart starting at y=18) ─────────
        float bx = 14 * s, bh = 8 * s, br = 2 * s;
        FillRoundedRect(g, BarFull,  bx, 18 * s, 52 * s, bh, br);
        FillRoundedRect(g, BarMid,   bx, 32 * s, 38 * s, bh, br);
        FillRoundedRect(g, BarLight, bx, 46 * s, 44 * s, bh, br);

        // ── Dots (3 circles, r=4, y=62, spaced 17px apart) ────────────────────
        float dr = 4 * s, dy = 62 * s;
        float[] dotX = new float[] { 14 * s, 31 * s, 48 * s };
        using var dotBrush = new SolidBrush(DotColor);
        foreach (var dx in dotX)
            g.FillEllipse(dotBrush, dx, dy, dr * 2, dr * 2);

        return bmp;
    }

    private static void FillRoundedRect(Graphics g, DrawColor color,
                                        float x, float y, float w, float h, float r)
    {
        r = Math.Min(r, Math.Min(w, h) / 2f);
        if (r <= 0)
        {
            using var fb = new SolidBrush(color);
            g.FillRectangle(fb, x, y, w, h);
            return;
        }

        float d = r * 2;
        using var path = new GraphicsPath();
        path.AddArc(x,         y,         d, d, 180, 90);
        path.AddArc(x + w - d, y,         d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d,   0, 90);
        path.AddArc(x,         y + h - d, d, d,  90, 90);
        path.CloseFigure();

        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
