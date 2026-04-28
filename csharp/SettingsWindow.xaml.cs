using ScalanceLogs.Helpers;
using ScalanceLogs.Models;
using ScalanceLogs.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace ScalanceLogs;

// ── Theme card view-model ─────────────────────────────────────────────

public class ThemeCardItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string         Name        { get; set; } = "";
    public SolidColorBrush AccentColor { get; set; } = new();
    public SolidColorBrush BgColor     { get; set; } = new();
    public SolidColorBrush TextColor   { get; set; } = new();
    public SolidColorBrush GreenColor  { get; set; } = new();
    public SolidColorBrush RedColor    { get; set; } = new();

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPC(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPC([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ── Window ────────────────────────────────────────────────────────────

public class BalloonLabelItem
{
    public string Label     { get; set; } = "";
    public bool   IsChecked { get; set; }
}

public partial class SettingsWindow : Window
{
    private static readonly string[] StandardLabels =
        ["EMERG", "ALERT", "CRIT", "ERROR", "WARN", "NOTICE", "INFO", "DEBUG"];

    private readonly ObservableCollection<SwitchNameEntry>  _switches     = [];
    private readonly ObservableCollection<MessageTypeEntry> _msgTypes     = [];
    private readonly ObservableCollection<QuickFilterEntry> _quickFilters = [];
    private readonly ObservableCollection<string>           _patterns     = [];
    private readonly ObservableCollection<BalloonLabelItem> _balloonItems = [];
    private readonly ObservableCollection<ThemeCardItem>    _themeCards   = [];

    public SettingsWindow()
    {
        InitializeComponent();

        SwitchGrid.ItemsSource       = _switches;
        MsgTypeGrid.ItemsSource      = _msgTypes;
        QFGrid.ItemsSource           = _quickFilters;
        PatternList.ItemsSource      = _patterns;
        BalloonLabelList.ItemsSource = _balloonItems;
        ThemeCards.ItemsSource       = _themeCards;

        LoadFromSettings(App.Settings);

        Icon    = Helpers.IconHelper.CreateBrandBitmapSource(32);
        Loaded += (_, _) => Helpers.TitleBarHelper.Apply(this);
    }

    private void LoadFromSettings(AppSettings s)
    {
        PortBox.Text      = s.UdpPort.ToString();
        RetentionBox.Text = s.LogRetentionDays.ToString();
        LogPathBox.Text   = s.LogPath;
        AutoStartCheck.IsChecked  = s.AutoStart;
        BalloonCheck.IsChecked    = s.BalloonNotifications;
        StrictModeCheck.IsChecked = s.StrictMode;

        // Theme cards
        RebuildThemeCards(s.Theme);

        // Balloon labels
        var customLabels = s.MessageTypes
            .Select(m => m.Label)
            .Where(l => !string.IsNullOrWhiteSpace(l) &&
                        !StandardLabels.Contains(l, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        _balloonItems.Clear();
        foreach (var lbl in StandardLabels.Concat(customLabels))
            _balloonItems.Add(new BalloonLabelItem
            {
                Label     = lbl,
                IsChecked = s.BalloonLabels.Count == 0 ||
                            s.BalloonLabels.Contains(lbl, StringComparer.OrdinalIgnoreCase)
            });

        var allSelected = s.BalloonLabels.Count == 0;
        BalloonAllCheck.IsChecked         = allSelected;
        BalloonLabelList.Opacity          = allSelected ? 0.35 : 1.0;
        BalloonLabelList.IsHitTestVisible = !allSelected;
        BalloonTriggers.Visibility        = s.BalloonNotifications
                                            ? Visibility.Visible : Visibility.Collapsed;

        _switches.Clear();
        foreach (var x in s.SwitchNames)
            _switches.Add(new SwitchNameEntry { Ip = x.Ip, Name = x.Name });

        _patterns.Clear();
        foreach (var p in s.EventPatterns) _patterns.Add(p);

        _msgTypes.Clear();
        foreach (var m in s.MessageTypes)
            _msgTypes.Add(new MessageTypeEntry
                { Pattern = m.Pattern, Label = m.Label, Color = m.Color, Bg = m.Bg });

        _quickFilters.Clear();
        foreach (var q in s.QuickFilters)
            _quickFilters.Add(new QuickFilterEntry { Label = q.Label, Query = q.Query });
    }

    // ── Theme cards ───────────────────────────────────────────────────
    private void RebuildThemeCards(string selectedTheme)
    {
        _themeCards.Clear();
        foreach (var name in ThemeManager.Available)
        {
            var c = ThemeManager.GetEffectiveColors(name);
            _themeCards.Add(new ThemeCardItem
            {
                Name        = name,
                AccentColor = ColorHelper.ParseBrush(c["AccentBrush"]),
                BgColor     = ColorHelper.ParseBrush(c["BgBrush"]),
                TextColor   = ColorHelper.ParseBrush(c["TextBrush"]),
                GreenColor  = ColorHelper.ParseBrush(c["GreenBrush"]),
                RedColor    = ColorHelper.ParseBrush(c["RedBrush"]),
                IsSelected  = name == selectedTheme,
            });
        }
    }

    private void EditTheme_Click(object sender, RoutedEventArgs e)
    {
        var themeName = (string)((System.Windows.Controls.Button)sender).Tag;
        var editor    = new ThemeEditorWindow(themeName) { Owner = this };
        if (editor.ShowDialog() == true)
            RebuildThemeCards(_themeCards.FirstOrDefault(c => c.IsSelected)?.Name
                              ?? App.Settings.Theme);
    }

    // ── OK / Apply / Cancel ───────────────────────────────────────────
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ApplySettings()) { DialogResult = true; Close(); }
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => ApplySettings();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private bool ApplySettings()
    {
        if (!int.TryParse(PortBox.Text, out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Invalid port number (1–65535).", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!int.TryParse(RetentionBox.Text, out var days) || days < 1)
        {
            MessageBox.Show("Retention must be at least 1 day.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        SwitchGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        MsgTypeGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        QFGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        var s = App.Settings;
        var portChanged    = s.UdpPort != port;
        var logPathChanged = s.LogPath != LogPathBox.Text.Trim();

        var selectedTheme = _themeCards.FirstOrDefault(c => c.IsSelected)?.Name ?? "Cyber";
        var themeChanged  = s.Theme != selectedTheme;
        s.Theme = selectedTheme;

        s.UdpPort              = port;
        s.LogRetentionDays     = days;
        s.LogPath              = LogPathBox.Text.Trim();
        s.AutoStart            = AutoStartCheck.IsChecked  == true;
        s.BalloonNotifications = BalloonCheck.IsChecked    == true;
        s.StrictMode           = StrictModeCheck.IsChecked == true;
        s.BalloonLabels        = BalloonAllCheck.IsChecked == true
                                 ? []
                                 : [.. _balloonItems.Where(b => b.IsChecked).Select(b => b.Label)];

        s.SwitchNames   = [.. _switches];
        s.EventPatterns = [.. _patterns];
        s.MessageTypes  = [.. _msgTypes];
        s.QuickFilters  = [.. _quickFilters];

        SettingsService.Save(s);
        AutostartHelper.Set(s.AutoStart);

        if (themeChanged)   ThemeManager.Apply(s.Theme);
        if (logPathChanged) LogManager.Reinitialize();
        if (portChanged)    ((App)Application.Current).StartCollector();

        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.BuildQuickFilters();
            mw.RefreshFileTree();
            if (themeChanged) mw.ReloadLog();
        }

        if (AdminHelper.NeedsAdmin(port) && !AdminHelper.IsAdmin())
        {
            MessageBox.Show(
                $"Port {port} requires administrator rights.\n" +
                "Run as Administrator, or use port 5140.",
                "Admin Required", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        return true;
    }

    // ── Message Types — color pickers ─────────────────────────────────
    private void PickChipColor_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not MessageTypeEntry item) return;
        var picked = PickHexColor(item.Color);
        if (picked != null) item.Color = picked;
    }

    private void PickChipBg_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not MessageTypeEntry item) return;
        var picked = PickBgColor(item.Bg);
        if (picked != null) item.Bg = picked;
    }

    private static string? PickHexColor(string current)
    {
        var dc = ToDrawingColor(current);
        using var dlg = new System.Windows.Forms.ColorDialog
            { Color = dc, FullOpen = true, AnyColor = true };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return null;
        return $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
    }

    private static string? PickBgColor(string current)
    {
        // Extract existing alpha from rgba(...) if present
        double alpha = 0.12;
        var m = Regex.Match(current,
            @"rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)");
        string hexForPicker = current;
        if (m.Success)
        {
            if (m.Groups[4].Success)
                alpha = double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
            hexForPicker = $"#{int.Parse(m.Groups[1].Value):X2}" +
                           $"{int.Parse(m.Groups[2].Value):X2}" +
                           $"{int.Parse(m.Groups[3].Value):X2}";
        }

        var dc = ToDrawingColor(hexForPicker);
        using var dlg = new System.Windows.Forms.ColorDialog
            { Color = dc, FullOpen = true, AnyColor = true };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return null;

        var c = dlg.Color;
        return $"rgba({c.R},{c.G},{c.B},{alpha.ToString("0.##", CultureInfo.InvariantCulture)})";
    }

    private static System.Drawing.Color ToDrawingColor(string css)
    {
        try
        {
            var brush = ColorHelper.ParseBrush(css);
            var c = brush.Color;
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch { return System.Drawing.Color.FromArgb(0xa8, 0xb4, 0xcc); }
    }

    // ── Test packet ───────────────────────────────────────────────────
    private async void TestPacket_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, out var port) || port < 1 || port > 65535)
        {
            TestStatus.Text       = "Invalid port";
            TestStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
            return;
        }

        var ts   = DateTime.Now.ToString("MMM dd HH:mm:ss", CultureInfo.InvariantCulture);
        var raw  = $"<133>{ts} 127.0.0.1 ScalanceLogs: test packet from Settings (port {port})";
        var data = Encoding.UTF8.GetBytes(raw);

        try
        {
            using var udp = new UdpClient();
            await udp.SendAsync(data, data.Length, "127.0.0.1", port);
            TestStatus.Text       = $"✓ Sent to 127.0.0.1:{port}";
            TestStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x5f, 0xb8, 0x78));

            await Task.Delay(500);
            if (Application.Current.MainWindow is MainWindow mw)
                mw.RefreshFileTree();
        }
        catch (Exception ex)
        {
            TestStatus.Text       = $"✗ {ex.Message}";
            TestStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xe0, 0x6c, 0x75));
        }
    }

    // ── Balloon triggers ──────────────────────────────────────────────
    private void BalloonCheck_Changed(object sender, RoutedEventArgs e) =>
        BalloonTriggers.Visibility = BalloonCheck.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;

    private void BalloonAllCheck_Changed(object sender, RoutedEventArgs e)
    {
        var all = BalloonAllCheck.IsChecked == true;
        BalloonLabelList.Opacity          = all ? 0.35 : 1.0;
        BalloonLabelList.IsHitTestVisible = !all;
    }

    // ── Browse log folder ─────────────────────────────────────────────
    private void BrowseLog_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description            = "Select log folder",
            SelectedPath           = LogPathBox.Text,
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            LogPathBox.Text = dlg.SelectedPath;
    }

    // ── Switches ──────────────────────────────────────────────────────
    private void AddSwitch_Click(object sender, RoutedEventArgs e) =>
        _switches.Add(new SwitchNameEntry { Ip = "0.0.0.0", Name = "Switch" });

    private void RemoveSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (SwitchGrid.SelectedItem is SwitchNameEntry s) _switches.Remove(s);
    }

    // ── Event patterns ────────────────────────────────────────────────
    private void AddPattern_Click(object sender, RoutedEventArgs e) => AddPattern();
    private void NewPattern_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddPattern();
    }
    private void AddPattern()
    {
        var p = NewPatternBox.Text.Trim();
        if (string.IsNullOrEmpty(p)) return;
        _patterns.Add(p);
        NewPatternBox.Clear();
    }
    private void RemovePattern_Click(object sender, RoutedEventArgs e)
    {
        if (PatternList.SelectedItem is string p) _patterns.Remove(p);
    }

    // ── Message types ─────────────────────────────────────────────────
    private void AddMsgType_Click(object sender, RoutedEventArgs e) =>
        _msgTypes.Add(new MessageTypeEntry
            { Pattern = "pattern", Label = "LABEL",
              Color = "#a8b4cc", Bg = "rgba(168,180,204,0.08)" });

    private void RemoveMsgType_Click(object sender, RoutedEventArgs e)
    {
        if (MsgTypeGrid.SelectedItem is MessageTypeEntry m) _msgTypes.Remove(m);
    }

    // ── Quick filters ─────────────────────────────────────────────────
    private void AddQF_Click(object sender, RoutedEventArgs e) =>
        _quickFilters.Add(new QuickFilterEntry { Label = "Label", Query = "query" });

    private void RemoveQF_Click(object sender, RoutedEventArgs e)
    {
        if (QFGrid.SelectedItem is QuickFilterEntry q) _quickFilters.Remove(q);
    }
}
