using ScalanceLogs.Helpers;
using ScalanceLogs.Models;
using System.Text.RegularExpressions;

namespace ScalanceLogs.Services;

public static class SyslogParser
{
    // Parses RFC-3164 raw UDP datagram
    private static readonly Regex SyslogRe = new(
        @"^<(\d+)>(\w{3}\s+\d+\s+[\d:]+)\s+([\w.\-]+)\s+(.*)$",
        RegexOptions.Compiled);

    // Parses a formatted log line from file:
    // "2025-01-15 10:30:45 [INFO  ] 192.168.1.10 (SW-Panel-A) | message"
    private static readonly Regex LineRe = new(
        @"^(\d{4}-\d{2}-\d{2}\s[\d:]+)\s+\[([^\]]+)\]\s+([\d.]+(?:\s+\([^)]+\))?)[^|]*\|\s*(.*)$",
        RegexOptions.Compiled);

    // Extracts the useful part after syslog structured-data block [...]
    private static readonly Regex ExtractMsgRe = new(@"\[[^\]]*\]\s*(.+)$", RegexOptions.Compiled);

    private static readonly string[] SeverityLabels =
        ["EMERG", "ALERT", "CRIT", "ERROR", "WARN", "NOTICE", "INFO", "DEBUG"];

    // ── Parse raw syslog datagram ────────────────────────────────
    public static (int severity, string hostname, string message)
        ParseRaw(string raw, string srcIp)
    {
        var m = SyslogRe.Match(raw);
        if (m.Success)
        {
            var pri = int.Parse(m.Groups[1].Value);
            return (pri & 0x07, m.Groups[3].Value, m.Groups[4].Value);
        }
        return (6, srcIp, raw);
    }

    public static string SeverityLabel(int code) =>
        (code >= 0 && code < SeverityLabels.Length) ? SeverityLabels[code] : "INFO";

    public static string ExtractUsefulMessage(string raw)
    {
        var m = ExtractMsgRe.Match(raw);
        return m.Success ? m.Groups[1].Value.Trim() : raw.Trim();
    }

    // ── Build LogEntry from a formatted log file line ────────────
    public static LogEntry? BuildEntry(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        string ts = "", sevStr = "INFO", host = "", rawPart = line;
        var m = LineRe.Match(line);
        if (m.Success)
        {
            ts      = m.Groups[1].Value;
            sevStr  = m.Groups[2].Value.Trim();
            host    = m.Groups[3].Value.Trim();
            rawPart = m.Groups[4].Value;
        }

        var msg     = ExtractUsefulMessage(rawPart);
        var sevKey  = sevStr.ToLowerInvariant();
        var isDown  = Regex.IsMatch(line, @"link\s+down|port.*down", RegexOptions.IgnoreCase);
        var isUp    = Regex.IsMatch(line, @"link\s+up|port.*up",   RegexOptions.IgnoreCase);

        // Severity-based row & chip colours
        Brush rowBg     = Brushes.Transparent;
        Brush rowBorder = Brushes.Transparent;
        Brush chipFg    = Brushes.Gray;
        Brush chipBg    = Brushes.Transparent;
        string chipLbl  = sevStr;

        switch (sevKey)
        {
            case "error": case "crit": case "emerg": case "alert":
                rowBg    = ColorHelper.ParseBrush("rgba(255,61,61,0.05)");
                rowBorder= ColorHelper.ParseBrush("#b85555");
                chipFg   = ColorHelper.ParseBrush("#b85555");
                chipBg   = ColorHelper.ParseBrush("rgba(255,61,61,0.2)");
                break;
            case "warn":
                rowBg    = ColorHelper.ParseBrush("rgba(255,215,64,0.04)");
                rowBorder= ColorHelper.ParseBrush("#b8913a");
                chipFg   = ColorHelper.ParseBrush("#b8913a");
                chipBg   = ColorHelper.ParseBrush("rgba(255,215,64,0.15)");
                break;
            case "info": case "notice":
                chipFg   = ColorHelper.ParseBrush("#4fa8c5");
                chipBg   = ColorHelper.ParseBrush("rgba(0,200,255,0.1)");
                break;
            case "debug":
                chipFg   = ColorHelper.ParseBrush("#3d4a60");
                chipBg   = ColorHelper.ParseBrush("rgba(100,100,100,0.15)");
                break;
        }

        if (isDown) rowBorder = ColorHelper.ParseBrush("#b85555");
        if (isUp)   rowBorder = ColorHelper.ParseBrush("#4a9e72");

        // Message type override
        foreach (var mt in App.Settings.MessageTypes)
        {
            if (Regex.IsMatch(msg, mt.Pattern, RegexOptions.IgnoreCase))
            {
                chipLbl = mt.Label;
                chipFg  = ColorHelper.ParseBrush(mt.Color);
                chipBg  = ColorHelper.ParseBrush(mt.Bg);
                rowBg   = ColorHelper.ParseBrush(mt.Bg);
                if (!string.IsNullOrEmpty(mt.Color))
                    rowBorder = ColorHelper.ParseBrush(mt.Color);
                break;
            }
        }

        // Split host into IP + suffix; validate IP before making it a clickable link
        string ipPart = "", hostSuffix = "";
        var ipM = Regex.Match(host, @"^(\d+\.\d+\.\d+\.\d+)(.*)");
        if (ipM.Success && System.Net.IPAddress.TryParse(ipM.Groups[1].Value, out _))
        {
            ipPart     = ipM.Groups[1].Value;
            hostSuffix = ipM.Groups[2].Value;
        }
        else
        {
            // Not a valid IP — show as plain text in hostSuffix, no clickable link
            hostSuffix = host;
        }

        return new LogEntry
        {
            Raw          = rawPart,
            Timestamp    = ts,
            SeverityText = chipLbl,
            Host         = host,
            IpPart       = ipPart,
            HostSuffix   = hostSuffix,
            Message      = msg,
            ChipLabel    = chipLbl,
            ChipFg       = chipFg,
            ChipBg       = chipBg,
            RowBg        = rowBg,
            RowBorder    = rowBorder,
        };
    }

    // ── Build LiveEntry from a formatted log line ────────────────
    public static LiveEntry? BuildLive(string line)
    {
        var entry = BuildEntry(line);
        if (entry is null) return null;

        var swM = Regex.Match(entry.Host, @"\(([^)]+)\)");
        var sw  = swM.Success ? swM.Groups[1].Value : entry.IpPart;
        var timeM = Regex.Match(entry.Timestamp, @"\d{2}:\d{2}:\d{2}$");

        return new LiveEntry
        {
            TimeStr    = timeM.Success ? timeM.Value : entry.Timestamp,
            SwitchName = sw,
            Message    = entry.Message,
            ChipLabel  = entry.ChipLabel,
            ChipFg     = entry.ChipFg,
            ChipBg     = entry.ChipBg,
            RowBorder  = entry.RowBorder,
        };
    }
}
