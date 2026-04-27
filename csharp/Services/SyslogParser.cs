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

    private static readonly Regex ExtractMsgRe = new(@"\[[^\]]*\]\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex IpHostRe     = new(@"^(\d+\.\d+\.\d+\.\d+)(.*)", RegexOptions.Compiled);
    private static readonly Regex TimeRe       = new(@"\d{2}:\d{2}:\d{2}$", RegexOptions.Compiled);
    private static readonly Regex SwitchTagRe  = new(@"\(([^)]+)\)", RegexOptions.Compiled);

    private static readonly string[] SeverityLabels =
        new string[] { "EMERG", "ALERT", "CRIT", "ERROR", "WARN", "NOTICE", "INFO", "DEBUG" };

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
        var isDown  = SafeRegex.IsMatch(line, @"link\s+down|port.*down");
        var isUp    = SafeRegex.IsMatch(line, @"link\s+up|port.*up");

        // Severity-based row & chip colours (cached brushes — no per-row Resources lookup)
        Brush rowBg     = Brushes.Transparent;
        Brush rowBorder = Brushes.Transparent;
        Brush chipFg    = Brushes.Gray;
        Brush chipBg    = Brushes.Transparent;
        string chipLbl  = sevStr;

        switch (sevKey)
        {
            case "error": case "crit": case "emerg": case "alert":
                rowBg     = ThemeBrushes.RowErr;
                rowBorder = ThemeBrushes.Red;
                chipFg    = ThemeBrushes.Red;
                chipBg    = ColorHelper.ParseBrush("rgba(255,61,61,0.2)");
                break;
            case "warn":
                rowBg     = ThemeBrushes.RowWrn;
                rowBorder = ThemeBrushes.Yellow;
                chipFg    = ThemeBrushes.Yellow;
                chipBg    = ColorHelper.ParseBrush("rgba(255,215,64,0.15)");
                break;
            case "info": case "notice":
                chipFg    = ThemeBrushes.Accent;
                chipBg    = ColorHelper.ParseBrush("rgba(0,200,255,0.1)");
                break;
            case "debug":
                chipFg    = ThemeBrushes.Muted;
                chipBg    = ColorHelper.ParseBrush("rgba(100,100,100,0.15)");
                break;
        }

        if (isDown) rowBorder = ThemeBrushes.Red;
        if (isUp)   rowBorder = ThemeBrushes.Green;

        // Message type override (uses SafeRegex — protects against ReDoS)
        foreach (var mt in App.Settings.MessageTypes)
        {
            if (SafeRegex.IsMatch(msg, mt.Pattern))
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
        var ipM = IpHostRe.Match(host);
        if (ipM.Success && System.Net.IPAddress.TryParse(ipM.Groups[1].Value, out _))
        {
            ipPart     = ipM.Groups[1].Value;
            hostSuffix = ipM.Groups[2].Value;
        }
        else
        {
            hostSuffix = host;
        }

        return new LogEntry
        {
            Raw          = rawPart,
            Timestamp    = ts,
            Severity     = sevStr,        // ← original severity (used for stats)
            SeverityText = sevStr,
            Host         = host,
            IpPart       = ipPart,
            HostSuffix   = hostSuffix,
            Message      = msg,
            ChipLabel    = chipLbl,       // ← may be overridden by MessageType
            ChipFg       = chipFg,
            ChipBg       = chipBg,
            RowBg        = rowBg,
            RowBorder    = rowBorder,
            IsLinkDown   = isDown,
            IsLinkUp     = isUp,
        };
    }

    // ── Build LiveEntry from a formatted log line ────────────────
    public static LiveEntry? BuildLive(string line)
    {
        var entry = BuildEntry(line);
        if (entry is null) return null;

        var swM = SwitchTagRe.Match(entry.Host);
        var sw  = swM.Success ? swM.Groups[1].Value : entry.IpPart;
        var timeM = TimeRe.Match(entry.Timestamp);

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
