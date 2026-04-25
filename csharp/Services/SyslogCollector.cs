using ScalanceLogs.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace ScalanceLogs.Services;

public class SyslogCollector
{
    private static readonly string[] SevLabels =
        ["EMERG", "ALERT", "CRIT", "ERROR", "WARN", "NOTICE", "INFO", "DEBUG"];

    public void StartAsync(CancellationToken ct)
    {
        _ = Task.Run(() => RunUdp(ct), ct); // fire-and-forget intentional
    }

    private async Task RunUdp(CancellationToken ct)
    {
        var port = App.Settings.UdpPort;

        if (AdminHelper.NeedsAdmin(port) && !AdminHelper.IsAdmin())
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                System.Windows.MessageBox.Show(
                    $"Port {port} requires administrator rights.\n\nRun as Administrator or change port to 5140 in Settings.",
                    "ScalanceLogs — Permission Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning));
            return;
        }

        try
        {
            var ep  = new IPEndPoint(IPAddress.Any, port);
            using var udp = new UdpClient(ep);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    var raw    = Encoding.UTF8.GetString(result.Buffer).Trim();
                    var srcIp  = result.RemoteEndPoint.Address.ToString();
                    Handle(raw, srcIp);
                }
                catch (OperationCanceledException) { break; }
                catch { /* ignore individual message errors */ }
            }
        }
        catch (SocketException ex)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                System.Windows.MessageBox.Show(
                    $"Cannot bind UDP port {port}:\n{ex.Message}",
                    "ScalanceLogs — Socket Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error));
        }
    }

    private static void Handle(string raw, string srcIp)
    {
        var (severity, _, message) = SyslogParser.ParseRaw(raw, srcIp);
        var sevLabel = SyslogParser.SeverityLabel(severity);

        var name  = App.Settings.SwitchNames.FirstOrDefault(s => s.Ip == srcIp)?.Name ?? srcIp;
        var label = (name != srcIp) ? $"{srcIp} ({name})" : srcIp;
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var ts    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var line  = $"{ts} [{sevLabel,-6}] {label} | {message}";

        var safeIp   = srcIp.Replace('.', '_');
        var fileBase = (name != srcIp) ? $"{safeIp}_{name}" : safeIp;
        LogManager.Write($"host_{fileBase}_{today}", $"{fileBase}_{today}.log", line);

        if (IsEvent(message))
        {
            LogManager.Write($"events_{today}", $"events_{today}.log", line);
            var msg = new RawSyslogMessage(ts, srcIp, label, severity, message, line);
            EventHub.Publish(msg);
        }
    }

    private static bool IsEvent(string message)
    {
        var patterns = App.Settings.EventPatterns;
        if (patterns.Count == 0) return true;
        return patterns.Any(p => Regex.IsMatch(message, p, RegexOptions.IgnoreCase));
    }
}
