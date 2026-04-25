using ScalanceLogs.Helpers;
using ScalanceLogs.Models;
using ScalanceLogs.Services;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace ScalanceLogs;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    private SingleInstance?  _singleInstance;
    private NotifyIcon?      _trayIcon;
    private SyslogCollector? _collector;
    private CancellationTokenSource _collectorCts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new SingleInstance("ScalanceLogs_Mutex_v1");
        if (!_singleInstance.IsOwner)
        {
            MessageBox.Show("ScalanceLogs already running.", "ScalanceLogs",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        Settings = SettingsService.Load();
        LogManager.Initialize();

        SetupTrayIcon();
        StartCollector();

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    // ── Collector lifecycle ──────────────────────────────────────
    public void StartCollector()
    {
        _collectorCts.Cancel();
        _collectorCts.Dispose();
        _collectorCts = new CancellationTokenSource();
        _collector    = new SyslogCollector();
        _collector.StartAsync(_collectorCts.Token);
    }

    // ── Tray icon ────────────────────────────────────────────────
    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon    = CreateTrayIcon(),
            Visible = true,
            Text    = "ScalanceLogs — Syslog Collector",
        };

        _trayIcon.DoubleClick += (_, _) => ShowMain();

        var menu = new ContextMenuStrip();
        menu.BackColor = System.Drawing.Color.FromArgb(22, 27, 38);
        menu.ForeColor = System.Drawing.Color.FromArgb(168, 180, 204);
        menu.Items.Add("Show",     null, (_, _) => ShowMain());
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",     null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;
    }

    public void ShowMain()
    {
        if (MainWindow is MainWindow mw)
        {
            mw.Show();
            mw.WindowState = WindowState.Normal;
            mw.Activate();
        }
    }

    public void OpenSettings()
    {
        var owner = MainWindow is { IsVisible: true } ? MainWindow : null;
        var win   = new SettingsWindow();
        if (owner != null) win.Owner = owner;
        win.ShowDialog();
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (!Settings.BalloonNotifications) return;
        _trayIcon?.ShowBalloonTip(4000, title, text, icon);
    }

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g   = Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.Transparent);
        // Accent-coloured square with inner grid pattern
        var accent = System.Drawing.Color.FromArgb(79, 168, 197);
        var dark   = System.Drawing.Color.FromArgb(45, 106, 130);
        g.FillRectangle(new SolidBrush(accent), 1, 1, 14, 14);
        g.FillRectangle(new SolidBrush(dark),   1, 1, 6,  6);
        g.FillRectangle(new SolidBrush(dark),   9, 9, 6,  6);
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void ExitApp()
    {
        _collectorCts.Cancel();
        _trayIcon?.Dispose();
        _singleInstance?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _collectorCts.Cancel();
        _collectorCts.Dispose();
        _trayIcon?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
