using ScalanceLogs.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace ScalanceLogs.Services;

public class SyslogCollector
{
    // Refuse oversized datagrams (jumbo or fragmentation amplification)
    private const int MaxPacketBytes = 8 * 1024;

    // Bounded queue: under flood we drop new packets instead of OOM/blocking the receive loop.
    private const int QueueCapacity = 4096;

    private Task? _ingestTask;

    public void StartAsync(CancellationToken ct)
    {
        var channel = Channel.CreateBounded<(string raw, string ip)>(
            new BoundedChannelOptions(QueueCapacity)
            {
                FullMode    = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = true,
            });

        _ = Task.Run(() => RunUdp(channel.Writer, ct), ct);
        _ingestTask = Task.Run(() => RunIngest(channel.Reader, ct), ct);
    }

    private async Task RunUdp(ChannelWriter<(string raw, string ip)> writer, CancellationToken ct)
    {
        var port = App.Settings.UdpPort;

        if (AdminHelper.NeedsAdmin(port) && !AdminHelper.IsAdmin())
        {
            AppLog.Warn($"Port {port} requires admin — collector not started.");
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                System.Windows.MessageBox.Show(
                    $"Port {port} requires administrator rights.\n\nRun as Administrator or change port to 5140 in Settings.",
                    "ScalanceLogs — Permission Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning));
            writer.Complete();
            return;
        }

        try
        {
            var ep  = new IPEndPoint(IPAddress.Any, port);
            using var udp = new UdpClient(ep);
            AppLog.Info($"UDP listener bound to 0.0.0.0:{port}");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    if (result.Buffer.Length == 0 || result.Buffer.Length > MaxPacketBytes)
                        continue;

                    var raw = Encoding.UTF8.GetString(result.Buffer).Trim();
                    if (string.IsNullOrEmpty(raw)) continue;

                    var ip  = result.RemoteEndPoint.Address.ToString();
                    writer.TryWrite((raw, ip)); // drops on full queue (DropWrite)
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { AppLog.Warn("Malformed packet ignored", ex); }
            }
            AppLog.Info("UDP listener stopped.");
        }
        catch (SocketException ex)
        {
            AppLog.Error($"Cannot bind UDP port {port}", ex);
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                System.Windows.MessageBox.Show(
                    $"Cannot bind UDP port {port}:\n{ex.Message}",
                    "ScalanceLogs — Socket Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error));
        }
        finally
        {
            writer.TryComplete();
        }
    }

    // Off the receive thread — file I/O can be slow under flood.
    private static async Task RunIngest(ChannelReader<(string raw, string ip)> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var (raw, ip) in reader.ReadAllAsync(ct))
                Handle(raw, ip);
        }
        catch (OperationCanceledException) { }
    }

    private static void Handle(string raw, string srcIp)
    {
        // payload = clean human MSG (header + [SD] stripped) — used for event filters & UI.
        // raw     = full original packet — written to the file so the operator can inspect
        //           it via the expand view (timestamp, app-name, sysUpTime, etc.)
        var (severity, _, payload) = SyslogParser.ParseRaw(raw, srcIp);
        var sevLabel = SyslogParser.SeverityLabel(severity);

        var entry = App.Settings.SwitchNames.FirstOrDefault(s => s.Ip == srcIp);
        var isRegistered = entry != null;
        var name  = entry?.Name ?? srcIp;
        var label = isRegistered ? $"{srcIp} ({name})" : srcIp;
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var ts    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var line  = $"{ts} [{sevLabel,-6}] {label} | {raw}";

        // ── Strict mode: divert unregistered sources to a separate audit log ──────
        if (App.Settings.StrictMode && !isRegistered)
        {
            LogManager.Write($"unknown_{today}", $"unknown_sources_{today}.log", line);
            return;  // do NOT publish to UI, do NOT touch per-host or events files
        }

        var safeIp   = srcIp.Replace('.', '_');
        var fileBase = isRegistered ? $"{safeIp}_{name}" : safeIp;
        LogManager.Write($"host_{fileBase}_{today}", $"{fileBase}_{today}.log", line);

        var msg = new RawSyslogMessage(ts, srcIp, label, severity, payload, line);
        EventHub.Publish(msg);

        if (IsEvent(payload))
            LogManager.Write($"events_{today}", $"events_{today}.log", line);
    }

    private static bool IsEvent(string message)
    {
        var patterns = App.Settings.EventPatterns;
        if (patterns.Count == 0) return true;
        return patterns.Any(p => SafeRegex.IsMatch(message, p));
    }
}
