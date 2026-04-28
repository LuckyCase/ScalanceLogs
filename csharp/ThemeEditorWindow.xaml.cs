using SyslogViewer.Helpers;
using SyslogViewer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace SyslogViewer;

// ── Row types for the ItemsControl ──────────────────────────────────

public class ThemeGroupRow
{
    public string Title { get; set; } = "";
}

public class ThemeColorRow : INotifyPropertyChanged
{
    public string Key   { get; set; } = "";
    public string Label { get; set; } = "";

    private string _hex = "#000000";
    public string Hex
    {
        get => _hex;
        set { _hex = value; OnPC(); OnPC(nameof(SwatchBrush)); }
    }

    public SolidColorBrush SwatchBrush
    {
        get
        {
            try { return ColorHelper.ParseBrush(Hex); }
            catch { return new SolidColorBrush(Colors.Gray); }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPC([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ── Window ───────────────────────────────────────────────────────────

public partial class ThemeEditorWindow : Window
{
    private readonly string _themeName;
    private readonly ObservableCollection<object> _rows = [];

    // Groups and their keys (TransBrush is always transparent — skip)
    private static readonly (string Group, string[] Keys)[] Layout =
    [
        ("BACKGROUNDS",          ["BgBrush", "SurfaceBrush", "SubtleBrush"]),
        ("ACCENT",               ["AccentBrush", "Accent2Brush"]),
        ("TEXT",                 ["TextBrush", "TextDimBrush", "MutedBrush"]),
        ("STATUS",               ["GreenBrush", "RedBrush", "YellowBrush"]),
        ("BORDERS & HOVER",      ["BorderBrush", "HoverBg", "SelectBg", "PressedBg"]),
        ("LOG ROW BACKGROUNDS",  ["RowErrorBg", "RowWarnBg"]),
    ];

    public ThemeEditorWindow(string themeName)
    {
        InitializeComponent();
        _themeName  = themeName;
        Title       = $"Edit Theme: {themeName}";
        ColorList.ItemsSource = _rows;
        BuildRows(ThemeManager.GetEffectiveColors(themeName));

        Icon    = Helpers.IconHelper.CreateBrandBitmapSource(32);
        Loaded += (_, _) => Helpers.TitleBarHelper.Apply(this);
    }

    // ── Build rows ───────────────────────────────────────────────────
    private void BuildRows(Dictionary<string, string> colors)
    {
        _rows.Clear();
        foreach (var (group, keys) in Layout)
        {
            _rows.Add(new ThemeGroupRow { Title = group });
            foreach (var key in keys)
            {
                var hex = colors.TryGetValue(key, out var h) ? h : "#808080";
                _rows.Add(new ThemeColorRow
                {
                    Key   = key,
                    Label = key.Replace("Brush", "").Replace("Bg", " Bg"),
                    Hex   = hex,
                });
            }
        }
    }

    private IEnumerable<ThemeColorRow> ColorRows =>
        _rows.OfType<ThemeColorRow>();

    // ── Color picker ─────────────────────────────────────────────────
    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not ThemeColorRow row) return;

        var initial = ToDrawingColor(row.Hex);
        using var dlg = new System.Windows.Forms.ColorDialog
        {
            Color       = initial,
            FullOpen    = true,
            AnyColor    = true,
            AllowFullOpen = true,
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        row.Hex = ToHex(dlg.Color);
    }

    // ── Buttons ──────────────────────────────────────────────────────
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                $"Reset \"{_themeName}\" to built-in defaults and save?",
                "Reset Theme", MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes) return;

        // Remove all overrides for this theme
        App.Settings.ThemeOverrides.Remove(_themeName);
        Services.SettingsService.Save(App.Settings);

        if (App.Settings.Theme == _themeName)
        {
            ThemeManager.Apply(_themeName);
            if (Application.Current.MainWindow is MainWindow mw)
                mw.ReloadLog();
        }

        DialogResult = true;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Collect changed values (only keys that differ from defaults)
        var defaults = ThemeManager.Defaults[_themeName];
        var overrides = new Dictionary<string, string>();
        foreach (var row in ColorRows)
        {
            if (!defaults.TryGetValue(row.Key, out var def) ||
                !string.Equals(def, row.Hex, StringComparison.OrdinalIgnoreCase))
            {
                overrides[row.Key] = row.Hex;
            }
        }

        // Save overrides to settings
        if (overrides.Count == 0)
            App.Settings.ThemeOverrides.Remove(_themeName);
        else
            App.Settings.ThemeOverrides[_themeName] = overrides;

        Services.SettingsService.Save(App.Settings);

        // Apply immediately if this is the active theme
        if (App.Settings.Theme == _themeName)
        {
            ThemeManager.Apply(_themeName);
            if (Application.Current.MainWindow is MainWindow mw)
                mw.ReloadLog();
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // ── Color helpers ────────────────────────────────────────────────
    private static string ToHex(System.Drawing.Color c)
        => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static System.Drawing.Color ToDrawingColor(string hex)
    {
        try
        {
            var brush = ColorHelper.ParseBrush(hex);
            var c = brush.Color;
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch { return System.Drawing.Color.FromArgb(0x80, 0x80, 0x80); }
    }
}
