using SyslogViewer.Helpers;
using SyslogViewer.Models;
using System.Text.RegularExpressions;

namespace SyslogViewer.Services;

public static class SyslogParser
{
    // RFC 5424 (modern Scalance, Cisco IOS-XE, …):
    //   <PRI>VERSION TIMESTAMP HOSTNAME APP-NAME PROCID MSGID [SD] MSG
    //   <134>1 2026-04-27T10:14:24+00:00 SW-01 6GK5216-4BS00-2AC2 69 - [meta …] Link down on P0.2.
    private static readonly Regex Rfc5424Re = new(
        @"^<(\d+)>\d+\s+\S+\s+(\S+)\s+\S+\s+\S+\s+\S+\s+(.*)$",
        RegexOptions.Compiled);

    // RFC 3164 (legacy BSD syslog):
    //   <PRI>Mmm DD HH:MM:SS HOSTNAME MSG
    private static readonly Regex Rfc3164Re = new(
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
        ["EMERG", "ALERT", "CRIT", "ERROR", "WARN", "NOTICE", "INFO", "DEBUG"];

    // ── Parse raw syslog datagram ────────────────────────────────
    /// <summary>
    /// Returns (severity, hostname, payload).  Payload is the human-readable MSG
    /// with header and structured-data stripped — used for event-filter matching.
    /// The full raw packet is preserved separately by the caller and written to
    /// the file as-is (so the user can see it via the row-expand view).
    /// </summary>
    public static (int severity, string hostname, string message)
        ParseRaw(string raw, string srcIp)
    {
        // RFC 5424 first (modern devices)
        var m5 = Rfc5424Re.Match(raw);
        if (m5.Success)
        {
            var pri  = int.Parse(m5.Groups[1].Value);
            var host = m5.Groups[2].Value;
            var msg  = StripStructuredData(m5.Groups[3].Value);
            return (pri & 0x07, host, msg);
        }

        // RFC 3164 fallback
        var m3 = Rfc3164Re.Match(raw);
        if (m3.Success)
        {
            var pri = int.Parse(m3.Groups[1].Value);
            return (pri & 0x07, m3.Groups[3].Value, m3.Groups[4].Value);
        }

        // Unknown format — keep severity unknown (default to INFO=6) and pass raw through
        return (6, srcIp, raw);
    }

    /// <summary>
    /// Removes RFC-5424 STRUCTURED-DATA: either nil ("-") or one or more
    /// "[ID key=value …]" blocks, returning just the human MSG.
    /// </summary>
    private static string StripStructuredData(string s)
    {
        s = s.TrimStart();
        if (s == "-")           return "";
        if (s.StartsWith("- ")) return s[2..].TrimStart();
        while (s.StartsWith('['))
        {
            var end = s.IndexOf(']');
            if (end < 0) break;
            s = s[(end + 1)..].TrimStart();
        }
        return s;
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

        // "Unregistered" = the source IP isn't in the user's SwitchNames list.
        // Non-IP host strings are treated as registered (no way to know).
        var isUnreg = !string.IsNullOrEmpty(ipPart) &&
                      !App.Settings.SwitchNames.Any(s => s.Ip == ipPart);

        return new LogEntry
        {
            Raw            = rawPart,
            Timestamp      = ts,
            Severity       = sevStr,        // ← original severity (used for stats)
            SeverityText   = sevStr,
            Host           = host,
            IpPart         = ipPart,
            HostSuffix     = hostSuffix,
            Message        = msg,
            ChipLabel      = chipLbl,       // ← may be overridden by MessageType
            ChipFg         = chipFg,
            ChipBg         = chipBg,
            RowBg          = rowBg,
            RowBorder      = rowBorder,
            IsLinkDown     = isDown,
            IsLinkUp       = isUp,
            IsUnregistered = isUnreg,
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
