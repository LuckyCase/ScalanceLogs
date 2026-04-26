using System.Windows;

namespace ScalanceLogs.Helpers;

/// <summary>
/// Snapshot of theme brushes used by the syslog parser hot-path.
/// Refreshed by ThemeManager.Apply() so we don't hit Application.Resources
/// once per log line (~7 lookups × 1000 lines per render).
/// </summary>
public static class ThemeBrushes
{
    public static Brush Red    { get; private set; } = ColorHelper.ParseBrush("#b85555");
    public static Brush Yellow { get; private set; } = ColorHelper.ParseBrush("#b8913a");
    public static Brush Accent { get; private set; } = ColorHelper.ParseBrush("#4fa8c5");
    public static Brush Green  { get; private set; } = ColorHelper.ParseBrush("#4a9e72");
    public static Brush Muted  { get; private set; } = ColorHelper.ParseBrush("#3d4a60");
    public static Brush RowErr { get; private set; } = ColorHelper.ParseBrush("#1a0808");
    public static Brush RowWrn { get; private set; } = ColorHelper.ParseBrush("#1a1608");

    public static void Refresh()
    {
        if (Application.Current is null) return;
        var r = Application.Current.Resources;
        Red    = r["RedBrush"]    as Brush ?? Red;
        Yellow = r["YellowBrush"] as Brush ?? Yellow;
        Accent = r["AccentBrush"] as Brush ?? Accent;
        Green  = r["GreenBrush"]  as Brush ?? Green;
        Muted  = r["MutedBrush"]  as Brush ?? Muted;
        RowErr = r["RowErrorBg"]  as Brush ?? RowErr;
        RowWrn = r["RowWarnBg"]   as Brush ?? RowWrn;
    }
}
