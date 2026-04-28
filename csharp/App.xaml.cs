using SyslogViewer.Helpers;
using SyslogViewer.Models;
using SyslogViewer.Services;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace SyslogViewer;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    private SingleInstance?  _singleInstance;
    private NotifyIcon?      _trayIcon;
    private Icon?            _trayIconBitmap;   // owned HICON — must be destroyed
    private SyslogCollector? _collector;
    private CancellationTokenSource _collectorCts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        // Diagnostic log — created/cleared per session, lives next to the exe.
        AppLog.Initialize();
        AppLog.Info("App starting…");

        // Global safety net: catch anything we forgot to wrap so the app doesn't
        // disappear silently. Ctrl+C copies the message in the dialog.
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception", args.Exception);
            MessageBox.Show(
                $"Unhandled error:\n\n{args.Exception.GetType().Name}: {args.Exception.Message}\n\n" +
                $"Stack trace:\n{args.Exception.StackTrace}\n\n" +
                $"Full log: {AppLog.Path}",
                "SW-LOG — Unhandled error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;     // keep the app alive
        };

        // Background-task / non-UI thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled background exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        _singleInstance = new SingleInstance("SyslogViewer_Mutex_v1");
        if (!_singleInstance.IsOwner)
        {
            AppLog.Info("Another instance is already running — exiting.");
            MessageBox.Show("SW-LOG already running.", "SW-LOG",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        Settings = SettingsService.Load();
        AppLog.Info($"Settings loaded (theme={Settings.Theme}, port={Settings.UdpPort}, " +
                    $"strict={Settings.StrictMode}, switches={Settings.SwitchNames.Count}).");

        ThemeManager.Apply(Settings.Theme);
        LogManager.Initialize();
        AppLog.Info($"Log directory: {LogManager.LogDir}");

        SetupTrayIcon();
        StartCollector();

        var main = new MainWindow();
        MainWindow = main;
        main.Icon = IconHelper.CreateBrandBitmapSource(32);
        main.Show();
        AppLog.Info("MainWindow shown — startup complete.");
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
        _trayIconBitmap = CreateTrayIcon();
        _trayIcon = new NotifyIcon
        {
            Icon    = _trayIconBitmap,
            Visible = true,
            Text    = "SW-LOG — Syslog Viewer",
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

    // ── Custom WPF toast (replaces system balloon — no sound, no queue) ──
    private ToastWindow? _toast;

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (!Settings.BalloonNotifications) return;

        // Close any existing toast immediately — no queue
        _toast?.CloseToast();

        var isWarning = icon == ToolTipIcon.Warning || icon == ToolTipIcon.Error;
        _toast        = new ToastWindow(title, text, isWarning);
        _toast.Closed += (_, _) => { if (_toast is { } t && ReferenceEquals(t, _toast)) _toast = null; };
        _toast.Show();
    }

    private static Icon CreateTrayIcon() => IconHelper.CreateBrandIcon(16);

    private void ExitApp()
    {
        _collectorCts.Cancel();
        _trayIcon?.Dispose();
        _trayIconBitmap?.Dispose();   // releases native HICON
        _singleInstance?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info("App exiting…");
        try { _collectorCts.Cancel(); } catch { }
        _collectorCts.Dispose();
        _trayIcon?.Dispose();
        _trayIconBitmap?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
