using ScalanceLogs.Helpers;
using System.Windows;
using System.Windows.Media;

namespace ScalanceLogs.Services;

public static class ThemeManager
{
    public static readonly string[] Available = ["Cyber", "Light", "Midnight", "SlateGlass"];

    // ── Default palettes ────────────────────────────────────────────
    public static readonly Dictionary<string, Dictionary<string, string>> Defaults = new()
    {
        ["Cyber"] = new()
        {
            ["BgBrush"]      = "#10121a",
            ["SurfaceBrush"] = "#161b26",
            ["SubtleBrush"]  = "#1a1c24",
            ["BorderBrush"]  = "#222840",
            ["AccentBrush"]  = "#4fa8c5",
            ["Accent2Brush"] = "#2d6a82",
            ["TextBrush"]    = "#a8b4cc",
            ["TextDimBrush"] = "#556070",
            ["MutedBrush"]   = "#3d4a60",
            ["GreenBrush"]   = "#4a9e72",
            ["RedBrush"]     = "#b85555",
            ["YellowBrush"]  = "#b8913a",
            ["TransBrush"]   = "Transparent",
            ["HoverBg"]      = "#0a1520",
            ["SelectBg"]     = "#0d2030",
            ["PressedBg"]    = "#1a2a35",
            ["RowErrorBg"]   = "#1a0808",
            ["RowWarnBg"]    = "#1a1608",
        },
        ["Light"] = new()
        {
            ["BgBrush"]      = "#F8FAFC",
            ["SurfaceBrush"] = "#FFFFFF",
            ["SubtleBrush"]  = "#F1F5F9",
            ["BorderBrush"]  = "#E2E8F0",
            ["AccentBrush"]  = "#3B82F6",
            ["Accent2Brush"] = "#2563EB",
            ["TextBrush"]    = "#0F172A",
            ["TextDimBrush"] = "#94A3B8",
            ["MutedBrush"]   = "#CBD5E1",
            ["GreenBrush"]   = "#10B981",
            ["RedBrush"]     = "#EF4444",
            ["YellowBrush"]  = "#F59E0B",
            ["TransBrush"]   = "Transparent",
            ["HoverBg"]      = "#F1F5F9",
            ["SelectBg"]     = "#EFF6FF",
            ["PressedBg"]    = "#DBEAFE",
            ["RowErrorBg"]   = "#FEF2F2",
            ["RowWarnBg"]    = "#FFFBEB",
        },
        ["Midnight"] = new()
        {
            ["BgBrush"]      = "#0C0E14",
            ["SurfaceBrush"] = "#11141D",
            ["SubtleBrush"]  = "#151829",
            ["BorderBrush"]  = "#1E2235",
            ["AccentBrush"]  = "#7C6DF8",
            ["Accent2Brush"] = "#6D5EE8",
            ["TextBrush"]    = "#E2E8F0",
            ["TextDimBrush"] = "#4A5180",
            ["MutedBrush"]   = "#2A2E4A",
            ["GreenBrush"]   = "#34D399",
            ["RedBrush"]     = "#F87171",
            ["YellowBrush"]  = "#FBBF24",
            ["TransBrush"]   = "Transparent",
            ["HoverBg"]      = "#181C28",
            ["SelectBg"]     = "#1A1830",
            ["PressedBg"]    = "#1A1830",
            ["RowErrorBg"]   = "#1C1010",
            ["RowWarnBg"]    = "#1C1A0C",
        },
        ["SlateGlass"] = new()
        {
            ["BgBrush"]      = "#1C1C1E",
            ["SurfaceBrush"] = "#2C2C30",
            ["SubtleBrush"]  = "#141416",
            ["BorderBrush"]  = "#3A3A3E",
            ["AccentBrush"]  = "#2DD4BF",
            ["Accent2Brush"] = "#0D9488",
            ["TextBrush"]    = "#F5F5F7",
            ["TextDimBrush"] = "#8E8E93",
            ["MutedBrush"]   = "#48484A",
            ["GreenBrush"]   = "#30D158",
            ["RedBrush"]     = "#FF453A",
            ["YellowBrush"]  = "#FFD60A",
            ["TransBrush"]   = "Transparent",
            ["HoverBg"]      = "#3A3A3E",
            ["SelectBg"]     = "#333337",
            ["PressedBg"]    = "#404044",
            ["RowErrorBg"]   = "#2A1212",
            ["RowWarnBg"]    = "#2A2408",
        },
    };

    // ── Apply ────────────────────────────────────────────────────────
    /// <summary>Builds a programmatic ResourceDictionary from defaults + overrides and
    /// swaps it into Application.Current.Resources.MergedDictionaries.</summary>
    public static void Apply(string name)
    {
        if (!Available.Contains(name)) name = "Cyber";

        // Start from defaults, then layer user overrides
        var colors = new Dictionary<string, string>(Defaults[name]);
        if (App.Settings.ThemeOverrides.TryGetValue(name, out var ov))
            foreach (var kv in ov) colors[kv.Key] = kv.Value;

        // Build programmatic dict (no Source URI, so WPF won't reload from disk)
        var dict = new ResourceDictionary();
        foreach (var (key, hex) in colors)
        {
            Brush brush = hex.Equals("Transparent", StringComparison.OrdinalIgnoreCase)
                ? Brushes.Transparent
                : new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            dict[key] = brush;
        }

        // Replace existing theme dict (identified by containing "BgBrush")
        var merged = Application.Current.Resources.MergedDictionaries;
        var old    = merged.FirstOrDefault(d => d.Contains("BgBrush"));
        if (old != null) merged.Remove(old);
        merged.Add(dict);

        // Refresh cached brushes + repaint every open window's title bar
        ThemeBrushes.Refresh();
        TitleBarHelper.ApplyAll();
    }

    /// <summary>Effective colors for a theme: defaults merged with user overrides.</summary>
    public static Dictionary<string, string> GetEffectiveColors(string name)
    {
        var colors = new Dictionary<string, string>(Defaults[name]);
        if (App.Settings.ThemeOverrides.TryGetValue(name, out var ov))
            foreach (var kv in ov) colors[kv.Key] = kv.Value;
        return colors;
    }
}
