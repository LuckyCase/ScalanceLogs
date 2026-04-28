using ScalanceLogs.Models;
using ScalanceLogs.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private string   _activeQuickFilter = "";
    private bool     _autoScroll;

    private readonly DispatcherTimer _liveTimer   = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _lastUpdate      = DateTime.MinValue;
    private DateTime _lastTreeRefresh = DateTime.MinValue;

    private const int LiveMax      = 100;
    private const int LiveFadeSec  = 30;

    public MainWindow()
    {
        InitializeComponent();

        LogList.ItemsSource  = _logEntries;
        LiveFeed.ItemsSource = _liveEntries;
        SwitchList.ItemsSource = _activeSw;
        FileTree.ItemsSource = _fileGroups;

        _liveTimer.Tick   += LiveTimer_Tick;
        _statusTimer.Tick += StatusTimer_Tick;
        _liveTimer.Start();
        _statusTimer.Start();

        EventHub.MessageReceived += OnMessageReceived;

        Loaded  += (_, _) =>
        {
            RefreshFileTree();
            BuildQuickFilters();
            Helpers.TitleBarHelper.Apply(this);
        };
        Closing += OnClosing;
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
            // Named switch: {ip}_{name}_{date}.log
            var swM = Regex.Match(f, @"^((?:\d+_){3}\d+)_(.+)_(\d{4}-\d{2}-\d{2})\.log$");
            if (swM.Success)
            {
                var date = swM.Groups[3].Value;
                var name = swM.Groups[2].Value;
                if (!map.TryGetValue(date, out var g)) map[date] = g = new FileGroup { Date = date };
                g.Switches.Add(new FileItem { Display = name, FileName = f });
                continue;
            }
            // IP-only (no name configured): {ip}_{date}.log
            var ipM = Regex.Match(f, @"^((?:\d+_){3}\d+)_(\d{4}-\d{2}-\d{2})\.log$");
            if (ipM.Success)
            {
                var date = ipM.Groups[2].Value;
                var ip   = ipM.Groups[1].Value.Replace('_', '.');
                if (!map.TryGetValue(date, out var g)) map[date] = g = new FileGroup { Date = date };
                g.Switches.Add(new FileItem { Display = ip, FileName = f });
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
    public void ReloadLog() => LoadLog();

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

        // Reverse iteration without LINQ — avoids ambiguity with
        // MemoryExtensions.Reverse(Span<T>) (void) added in .NET 9.
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line  = lines[i];
            var entry = SyslogParser.BuildEntry(line);
            if (entry is null) continue;
            _logEntries.Add(entry);

            // Count by ORIGINAL severity (not ChipLabel — which can be overridden by MessageType)
            var sev = entry.Severity.ToLowerInvariant();
            if (sev is "error" or "crit" or "emerg" or "alert") err++;
            else if (sev == "warn") warn++;
            if (entry.IsLinkDown) down++;
            if (entry.IsLinkUp)   up++;
        }

        StatTotal.Text = lines.Length.ToString();
        StatErr.Text   = err.ToString();
        StatWarn.Text  = warn.ToString();
        StatDown.Text  = down.ToString();
        StatUp.Text    = up.ToString();

        _lastUpdate = DateTime.Now;

        if (_autoScroll)
            (LogList.Template.FindName("LogScroll", LogList) as ScrollViewer)?.ScrollToTop();
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

            if (!_activeSw.Contains(entry.SwitchName))
                _activeSw.Insert(0, entry.SwitchName);

            LiveBadge.Text = _liveEntries.Count.ToString();

            // ── Append directly to center (same moment as live panel) ──
            AppendToCenter(msg);

            // Refresh file tree at most once per 5 s (picks up new host files)
            if ((DateTime.Now - _lastTreeRefresh).TotalSeconds >= 5)
            {
                RefreshFileTree();
                _lastTreeRefresh = DateTime.Now;
            }

            FlashLiveStrip();

            if (App.Settings.BalloonNotifications)
            {
                var labels = App.Settings.BalloonLabels;
                if (labels.Count == 0 ||
                    labels.Contains(entry.ChipLabel, StringComparer.OrdinalIgnoreCase))
                    ((App)Application.Current).ShowBalloon(entry.SwitchName, entry.Message,
                        System.Windows.Forms.ToolTipIcon.Info);
            }
        });
    }

    // ── Append incoming message directly to center panel ─────────
    private void AppendToCenter(RawSyslogMessage msg)
    {
        if (_currentFile is null) return;

        var filename = System.IO.Path.GetFileName(_currentFile);

        // Events file — skip if message doesn't pass event filter
        if (filename.StartsWith("events_"))
        {
            var patterns = App.Settings.EventPatterns;
            if (patterns.Count > 0 &&
                !patterns.Any(p => Helpers.SafeRegex.IsMatch(msg.Message, p)))
                return;
        }
        else
        {
            // Switch-specific file — only messages from that IP
            var safeIp = msg.SrcIp.Replace('.', '_');
            if (!filename.StartsWith(safeIp)) return;
        }

        var entry = SyslogParser.BuildEntry(msg.Line);
        if (entry is null) return;

        _logEntries.Insert(0, entry);

        // Trim to current line limit
        var max = GetLineCount();
        while (_logEntries.Count > max)
            _logEntries.RemoveAt(_logEntries.Count - 1);

        // Update stats incrementally — by ORIGINAL severity, plus link state
        StatTotal.Text = _logEntries.Count.ToString();
        var sev = entry.Severity.ToLowerInvariant();
        if (sev is "error" or "crit" or "emerg" or "alert")
            StatErr.Text = (int.TryParse(StatErr.Text, out var e) ? e + 1 : 1).ToString();
        else if (sev == "warn")
            StatWarn.Text = (int.TryParse(StatWarn.Text, out var w) ? w + 1 : 1).ToString();
        if (entry.IsLinkDown)
            StatDown.Text = (int.TryParse(StatDown.Text, out var d) ? d + 1 : 1).ToString();
        if (entry.IsLinkUp)
            StatUp.Text = (int.TryParse(StatUp.Text, out var u) ? u + 1 : 1).ToString();

        _lastUpdate = DateTime.Now;

        if (_autoScroll)
            (LogList.Template.FindName("LogScroll", LogList) as ScrollViewer)?.ScrollToTop();
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

        // Drop switches with no remaining live entries (entries fade after LiveFadeSec)
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

    private void AutoScrollBtn_Click(object sender, RoutedEventArgs e)
    {
        _autoScroll = !_autoScroll;
        AutoScrollBtn.Style = _autoScroll
            ? (Style)FindResource("AccentBtn")
            : (Style)FindResource(typeof(Button));
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

    // ── Flash live strip when collapsed and message arrives ───────
    private ColorAnimation? _flashAnim;

    private void FlashLiveStrip()
    {
        if (LiveFull.Visibility != Visibility.Collapsed) return;

        // Fresh brush each time — avoids state conflicts when messages arrive rapidly
        var brush = new SolidColorBrush(Color.FromArgb(90, 79, 168, 197));
        LiveStrip.Background = brush;

        var anim = new ColorAnimation
        {
            To             = Color.FromRgb(0x16, 0x1b, 0x26),
            Duration       = TimeSpan.FromSeconds(1.2),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior   = FillBehavior.Stop,  // release after completion
        };

        // Only the last animation clears the background
        var thisAnim = anim;
        anim.Completed += (_, _) =>
        {
            if (ReferenceEquals(_flashAnim, thisAnim))
                LiveStrip.ClearValue(Border.BackgroundProperty);
        };
        _flashAnim = anim;
        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    // ── Close → minimize to tray (show hint once) ────────────────
    private bool _trayHintShown;

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        if (!_trayHintShown)
        {
            _trayHintShown = true;
            ((App)Application.Current).ShowBalloon(
                "Running in tray",
                "Use tray → Exit to quit.",
                System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    // ── Hyperlink: only open validated https://IP links ──────────
    private void Hyperlink_Navigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;

        var uri = e.Uri;
        if (uri.Scheme != Uri.UriSchemeHttps ||
            !System.Net.IPAddress.TryParse(uri.Host, out _))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            Helpers.AppLog.Info($"Opened browser → {uri.AbsoluteUri}");
        }
        catch (Exception ex)
        {
            // .NET 6 self-contained single-file occasionally fails to resolve the
            // default browser handler. Don't crash the app over a click.
            Helpers.AppLog.Error($"Process.Start failed for {uri.AbsoluteUri}", ex);
            MessageBox.Show(
                $"Could not open browser for {uri.AbsoluteUri}\n\n{ex.Message}",
                "SW-LOG", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private int GetLineCount()
    {
        var item = LineCountBox.SelectedItem as ComboBoxItem;
        return int.TryParse(item?.Tag?.ToString(), out var n) ? n : 200;
    }
}
