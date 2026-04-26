using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScalanceLogs.Helpers;

/// <summary>
/// Colors the Windows 11 DWM title bar (caption / text / border) to match
/// the active theme.  Silently does nothing on Windows 10 or older.
/// </summary>
public static class TitleBarHelper
{
    // DWM attribute IDs (Windows 11 21H2+)
    private const int DWMWA_BORDER_COLOR  = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR    = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    // WPF Color → COLORREF (0x00BBGGRR)
    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>Apply the current theme colours to a single window.</summary>
    public static void Apply(Window w)
    {
        try
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;

            var res = Application.Current.Resources;

            if (res["SurfaceBrush"] is SolidColorBrush surface)
                SetAttr(hwnd, DWMWA_CAPTION_COLOR, ToColorRef(surface.Color));

            if (res["TextBrush"] is SolidColorBrush text)
                SetAttr(hwnd, DWMWA_TEXT_COLOR, ToColorRef(text.Color));

            if (res["BorderBrush"] is SolidColorBrush border)
                SetAttr(hwnd, DWMWA_BORDER_COLOR, ToColorRef(border.Color));
        }
        catch (Exception ex)
        {
            // Non-fatal — older OS (Win10 ignores), sandbox, etc.
            System.Diagnostics.Debug.WriteLine($"[TitleBarHelper] {ex.Message}");
        }
    }

    /// <summary>Apply to every open window (called after a theme switch).</summary>
    public static void ApplyAll()
    {
        foreach (Window w in Application.Current.Windows)
            Apply(w);
    }

    private static void SetAttr(IntPtr hwnd, int attr, int value)
        => DwmSetWindowAttribute(hwnd, attr, ref value, sizeof(int));
}
