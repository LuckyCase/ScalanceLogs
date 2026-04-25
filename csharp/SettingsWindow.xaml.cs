using ScalanceLogs.Helpers;
using ScalanceLogs.Models;
using ScalanceLogs.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace ScalanceLogs;

public partial class SettingsWindow : Window
{
    private readonly ObservableCollection<SwitchNameEntry>  _switches    = [];
    private readonly ObservableCollection<MessageTypeEntry> _msgTypes    = [];
    private readonly ObservableCollection<QuickFilterEntry> _quickFilters= [];
    private readonly ObservableCollection<string>           _patterns    = [];

    public SettingsWindow()
    {
        InitializeComponent();

        SwitchGrid.ItemsSource  = _switches;
        MsgTypeGrid.ItemsSource = _msgTypes;
        QFGrid.ItemsSource      = _quickFilters;
        PatternList.ItemsSource = _patterns;

        LoadFromSettings(App.Settings);
    }

    private void LoadFromSettings(AppSettings s)
    {
        PortBox.Text      = s.UdpPort.ToString();
        RetentionBox.Text = s.LogRetentionDays.ToString();
        LogPathBox.Text   = s.LogPath;
        AutoStartCheck.IsChecked = s.AutoStart;
        BalloonCheck.IsChecked   = s.BalloonNotifications;

        _switches.Clear();
        foreach (var x in s.SwitchNames)
            _switches.Add(new SwitchNameEntry { Ip = x.Ip, Name = x.Name });

        _patterns.Clear();
        foreach (var p in s.EventPatterns) _patterns.Add(p);

        _msgTypes.Clear();
        foreach (var m in s.MessageTypes)
            _msgTypes.Add(new MessageTypeEntry { Pattern = m.Pattern, Label = m.Label, Color = m.Color, Bg = m.Bg });

        _quickFilters.Clear();
        foreach (var q in s.QuickFilters)
            _quickFilters.Add(new QuickFilterEntry { Label = q.Label, Query = q.Query });
    }

    // ── OK / Cancel ────────────────────────────��─────────────────
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Invalid port number (1–65535).", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(RetentionBox.Text, out var days) || days < 1)
        {
            MessageBox.Show("Retention must be at least 1 day.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Commit DataGrid edits in progress
        SwitchGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        MsgTypeGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        QFGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        var s = App.Settings;
        var portChanged    = s.UdpPort != port;
        var logPathChanged = s.LogPath != LogPathBox.Text.Trim();

        s.UdpPort              = port;
        s.LogRetentionDays     = days;
        s.LogPath              = LogPathBox.Text.Trim();
        s.AutoStart            = AutoStartCheck.IsChecked == true;
        s.BalloonNotifications = BalloonCheck.IsChecked   == true;

        s.SwitchNames   = [.. _switches];
        s.EventPatterns = [.. _patterns];
        s.MessageTypes  = [.. _msgTypes];
        s.QuickFilters  = [.. _quickFilters];

        SettingsService.Save(s);
        AutostartHelper.Set(s.AutoStart);

        if (logPathChanged) LogManager.Reinitialize();
        if (portChanged)    ((App)Application.Current).StartCollector();

        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.BuildQuickFilters();
            mw.RefreshFileTree();
        }

        // Warn about admin rights if needed
        if (AdminHelper.NeedsAdmin(port) && !AdminHelper.IsAdmin())
        {
            MessageBox.Show(
                $"Port {port} requires administrator rights.\n" +
                "Run as Administrator, or use port 5140.",
                "Admin Required", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // ── Browse log folder ──────────────────────────────���─────────
    private void BrowseLog_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description         = "Select log folder",
            SelectedPath        = LogPathBox.Text,
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            LogPathBox.Text = dlg.SelectedPath;
    }

    // ── Switches ─────────────────��───────────────────────────────
    private void AddSwitch_Click(object sender, RoutedEventArgs e) =>
        _switches.Add(new SwitchNameEntry { Ip = "0.0.0.0", Name = "Switch" });

    private void RemoveSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (SwitchGrid.SelectedItem is SwitchNameEntry s) _switches.Remove(s);
    }

    // ── Event patterns ───────────────────────────────────────────
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

    // ── Message types ───────────────────────────���─────────────────
    private void AddMsgType_Click(object sender, RoutedEventArgs e) =>
        _msgTypes.Add(new MessageTypeEntry { Pattern = "pattern", Label = "LABEL",
                                             Color = "#a8b4cc", Bg = "rgba(168,180,204,0.08)" });

    private void RemoveMsgType_Click(object sender, RoutedEventArgs e)
    {
        if (MsgTypeGrid.SelectedItem is MessageTypeEntry m) _msgTypes.Remove(m);
    }

    // ── Quick filters ─────────────────────────────────────────────
    private void AddQF_Click(object sender, RoutedEventArgs e) =>
        _quickFilters.Add(new QuickFilterEntry { Label = "Label", Query = "query" });

    private void RemoveQF_Click(object sender, RoutedEventArgs e)
    {
        if (QFGrid.SelectedItem is QuickFilterEntry q) _quickFilters.Remove(q);
    }
}
