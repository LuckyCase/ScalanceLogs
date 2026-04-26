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
                catch { /* malformed packet — ignore */ }
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

        var msg = new RawSyslogMessage(ts, srcIp, label, severity, message, line);
        EventHub.Publish(msg);

        if (IsEvent(message))
            LogManager.Write($"events_{today}", $"events_{today}.log", line);
    }

    private static bool IsEvent(string message)
    {
        var patterns = App.Settings.EventPatterns;
        if (patterns.Count == 0) return true;
        return patterns.Any(p => SafeRegex.IsMatch(message, p));
    }
}
