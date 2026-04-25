using ScalanceLogs.Models;
using ScalanceLogs.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace ScalanceLogs;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<LogEntry>  _logEntries  = [];
    private readonly ObservableCollection<LiveEntry> _liveEntries = [];
    private readonly ObservableCollection<string>    _activeSw    = [];
    private readonly ObservableCollection<FileGroup> _fileGroups  = [];

    private string?  _currentFile;
    private bool     _autoRefresh;
    private string   _activeQuickFilter = "";

    private readonly DispatcherTimer _autoTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _liveTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _lastUpdate = DateTime.MinValue;

    private const int LiveMax      = 100;
    private const int LiveFadeSec  = 30;

    public MainWindow()
    {
        InitializeComponent();

        LogList.ItemsSource  = _logEntries;
        LiveFeed.ItemsSource = _liveEntries;
        SwitchList.ItemsSource = _activeSw;
        FileTree.ItemsSource = _fileGroups;

        _autoTimer.Tick  += (_, _) => LoadLog();
        _liveTimer.Tick  += LiveTimer_Tick;
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();

        EventHub.MessageReceived += OnMessageReceived;

        Loaded  += (_, _) => { RefreshFileTree(); BuildQuickFilters(); };
        Closing += (_, e) => { e.Cancel = true; Hide(); };
    }

    // ── File tree ────────────────────────────────────────────────
    public void RefreshFileTree()
    {
        var files  = LogManager.GetLogFiles();
        var groups = GroupFiles(files);

        _fileGroups.Clear();
        foreach (var g in groups) _fileGroups.Add(g);

        // Auto-select today's events file on first load
        if (_currentFile is null && _fileGroups.Count > 0)
        {
            var first = _fileGroups[0];
            SelectFile(first.EventsFile ?? first.Switches.FirstOrDefault()?.FileName);
        }
    }

    private static List<FileGroup> GroupFiles(string[] files)
    {
        var map = new SortedDictionary<string, FileGroup>(Comparer<string>.Create((a, b) => b.CompareTo(a)));
        foreach (var f in files)
        {
            var evM = Regex.Match(f, @"^events_(\d{4}-\d{2}-\d{2})\.log$");
            if (evM.Success)
            {
                var d = evM.Groups[1].Value;
                if (!map.TryGetValue(d, out var g)) map[d] = g = new FileGroup { Date = d };
                g.EventsFile = f;
                continue;
            }
            var swM = Regex.Match(f, @"^((?:\d+_){3}\d+)_(.+)_(\d{4}-\d{2}-\d{2})\.log$");
            if (swM.Success)
            {
                var date = swM.Groups[3].Value;
                var name = swM.Groups[2].Value;
                if (!map.TryGetValue(date, out var g)) map[date] = g = new FileGroup { Date = date };
                g.Switches.Add(new FileItem { Display = name, FileName = f });
            }
        }
        return [.. map.Values];
    }

    private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        switch (e.NewValue)
        {
            case FileGroup grp when grp.EventsFile is not null:
                SelectFile(grp.EventsFile);
                break;
            case FileItem item:
                SelectFile(item.FileName);
                break;
        }
    }

    private void SelectFile(string? filename)
    {
        if (filename is null) return;
        _currentFile         = filename;
        FooterFile.Text      = filename;
        LoadLog();
    }

    // ── Log loading ──────────────────────────────────────────────
    private void LoadLog()
    {
        if (_currentFile is null) return;

        var file   = _currentFile;
        var count  = GetLineCount();
        var search = SearchBox.Text.Trim();

        Task.Run(() => LogManager.ReadLines(file, count, search))
            .ContinueWith(t => Dispatcher.InvokeAsync(() => RenderLog(t.Result)));
    }

    private void RenderLog(string[] lines)
    {
        _logEntries.Clear();
        int err = 0, warn = 0, down = 0, up = 0;

        foreach (var line in lines.Reverse())
        {
            var entry = SyslogParser.BuildEntry(line);
            if (entry is null) continue;
            _logEntries.Add(entry);

            var sev = entry.SeverityText.ToLowerInvariant();
            if (sev is "error" or "crit" or "emerg" or "alert") err++;
            if (sev == "warn") warn++;
            if (Regex.IsMatch(line, @"link\s+down|port.*down", RegexOptions.IgnoreCase)) down++;
            if (Regex.IsMatch(line, @"link\s+up|port.*up",   RegexOptions.IgnoreCase)) up++;
        }

        StatTotal.Text = lines.Length.ToString();
        StatErr.Text   = err.ToString();
        StatWarn.Text  = warn.ToString();
        StatDown.Text  = down.ToString();
        StatUp.Text    = up.ToString();

        _lastUpdate = DateTime.Now;
    }

    // ── Live events ──────────────────────────────────────────────
    private void OnMessageReceived(RawSyslogMessage msg)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var entry = SyslogParser.BuildLive(msg.Line);
            if (entry is null) return;

            _liveEntries.Insert(0, entry);
            while (_liveEntries.Count > LiveMax)
                _liveEntries.RemoveAt(_liveEntries.Count - 1);

            // Switch activity list
            if (!_activeSw.Contains(entry.SwitchName))
                _activeSw.Insert(0, entry.SwitchName);

            LiveBadge.Text = _liveEntries.Count.ToString();

            if (App.Settings.BalloonNotifications)
            {
                var sev = msg.Severity;
                if (sev <= 3) // EMERG..ERROR
                    ((App)Application.Current).ShowBalloon(entry.SwitchName, entry.Message,
                        System.Windows.Forms.ToolTipIcon.Warning);
            }

            if (_autoRefresh && _currentFile is not null)
                LoadLog();
        });
    }

    private void LiveTimer_Tick(object? sender, EventArgs e)
    {
        for (int i = _liveEntries.Count - 1; i >= 0; i--)
        {
            var item = _liveEntries[i];
            var elapsed = (DateTime.Now - item.AddedAt).TotalSeconds;
            item.Opacity = Math.Max(0, 1.0 - elapsed / LiveFadeSec);
            if (item.Opacity <= 0) _liveEntries.RemoveAt(i);
        }
        LiveBadge.Text = _liveEntries.Count.ToString();

        // Clear stale switches after 60s
        for (int i = _activeSw.Count - 1; i >= 0; i--)
        {
            var sw = _activeSw[i];
            if (_liveEntries.All(e => e.SwitchName != sw))
                _activeSw.RemoveAt(i);
        }
    }

    // ── Status indicator ─────────────────────────────────────────
    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        if (_lastUpdate == DateTime.MinValue) return;
        var sec   = (DateTime.Now - _lastUpdate).TotalSeconds;
        var stale = sec >= 15;
        StatusDot.Fill        = stale ? (Brush)FindResource("YellowBrush") : (Brush)FindResource("GreenBrush");
        StatusLabel.Text      = sec < 60 ? $"Updated {(int)sec}s ago" : $"Updated {(int)(sec/60)}m ago";
        StatusLabel.Foreground= stale ? (Brush)FindResource("YellowBrush") : (Brush)FindResource("TextDimBrush");
    }

    // ── Quick filters ─────────────────────────────────────────────
    public void BuildQuickFilters()
    {
        QuickFiltersPanel.Children.Clear();
        foreach (var qf in App.Settings.QuickFilters)
        {
            var btn = new Button
            {
                Content = qf.Label,
                Margin  = new Thickness(0, 0, 4, 0),
                Tag     = qf.Query,
            };
            btn.Click += QuickFilterBtn_Click;
            QuickFiltersPanel.Children.Add(btn);
        }
    }

    private void QuickFilterBtn_Click(object sender, RoutedEventArgs e)
    {
        var btn   = (Button)sender;
        var query = btn.Tag?.ToString() ?? "";

        if (_activeQuickFilter == query)
        {
            _activeQuickFilter = "";
            SearchBox.Text     = "";
            btn.Style          = (Style)FindResource(typeof(Button));
        }
        else
        {
            _activeQuickFilter = query;
            SearchBox.Text     = query;
            foreach (Button b in QuickFiltersPanel.Children)
                b.Style = (Style)FindResource(typeof(Button));
            btn.Style = (Style)FindResource("ActiveBtn");
        }
        LoadLog();
    }

    // ── Toolbar handlers ─────────────────────────────────────────
    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        RefreshFileTree();
        LoadLog();
    }

    private void AutoBtn_Click(object sender, RoutedEventArgs e)
    {
        _autoRefresh = !_autoRefresh;
        AutoBtn.Style = _autoRefresh
            ? (Style)FindResource("ActiveBtn")
            : (Style)FindResource("AccentBtn");
        if (_autoRefresh) { _autoTimer.Start(); _liveTimer.Start(); }
        else              { _autoTimer.Stop();  _liveTimer.Stop();  }
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).OpenSettings();

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) LoadLog();
    }

    private void LineCountBox_Changed(object sender, SelectionChangedEventArgs e) => LoadLog();

    private void ExpandBtn_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is LogEntry entry)
            entry.IsExpanded = !entry.IsExpanded;
    }

    private void CollapseSidebar_Click(object sender, RoutedEventArgs e)
    {
        bool collapse = SidebarFull.Visibility == Visibility.Visible;
        SidebarFull.Visibility  = collapse ? Visibility.Collapsed : Visibility.Visible;
        SidebarStrip.Visibility = collapse ? Visibility.Visible   : Visibility.Collapsed;
        SidebarCol.Width        = collapse ? new GridLength(28)   : new GridLength(210);
    }

    private void CollapseLive_Click(object sender, RoutedEventArgs e)
    {
        bool collapse = LiveFull.Visibility == Visibility.Visible;
        LiveFull.Visibility  = collapse ? Visibility.Collapsed : Visibility.Visible;
        LiveStrip.Visibility = collapse ? Visibility.Visible   : Visibility.Collapsed;
        LiveCol.Width        = collapse ? new GridLength(28)   : new GridLength(270);
    }

    private void Hyperlink_Navigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private int GetLineCount()
    {
        var item = LineCountBox.SelectedItem as ComboBoxItem;
        return int.TryParse(item?.Tag?.ToString(), out var n) ? n : 200;
    }
}
