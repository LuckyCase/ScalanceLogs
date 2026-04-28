using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyslogViewer.Models;

public class AppSettings
{
    public int    UdpPort           { get; set; } = 514;
    public int    LogRetentionDays  { get; set; } = 30;
    public string LogPath           { get; set; } = "logs";
    public bool   AutoStart         { get; set; } = false;
    public string Theme             { get; set; } = "Cyber";
    public bool   BalloonNotifications { get; set; } = true;
    // Strict mode: if true, packets from IPs not in SwitchNames are diverted to
    // unknown_sources_YYYY-MM-DD.log and excluded from the live feed / per-host files.
    public bool   StrictMode        { get; set; } = false;
    // Labels that trigger balloon. Empty = ALL messages.
    public List<string> BalloonLabels { get; set; } = ["EMERG", "ALERT", "CRIT", "ERROR",
                                                        "LINK DOWN", "LINK UP"];

    // Per-theme color overrides (key = theme name, value = {brushKey → hex})
    public Dictionary<string, Dictionary<string, string>> ThemeOverrides { get; set; } = new();

    public List<SwitchNameEntry>   SwitchNames   { get; set; } = [];
    public List<string>            EventPatterns { get; set; } = DefaultEventPatterns();
    public List<MessageTypeEntry>  MessageTypes  { get; set; } = DefaultMessageTypes();
    public List<QuickFilterEntry>  QuickFilters  { get; set; } = DefaultQuickFilters();

    public static List<string> DefaultEventPatterns() =>
    [
        @"link\s+(up|down)",
        @"port.*(up|down)",
        @"(fault|error|fail)",
    ];

    public static List<MessageTypeEntry> DefaultMessageTypes() =>
    [
        new() { Pattern = @"link\s+down",           Label = "LINK DOWN", Color = "#ff6b6b", Bg = "rgba(255,107,107,0.15)" },
        new() { Pattern = @"link\s+up",             Label = "LINK UP",   Color = "#4a9e72", Bg = "rgba(74,158,114,0.12)"  },
        new() { Pattern = @"time synchronized",     Label = "NTP SYNC",  Color = "#4fa8c5", Bg = "rgba(79,168,197,0.10)"  },
        new() { Pattern = @"time not synchronized", Label = "NTP LOST",  Color = "#b8913a", Bg = "rgba(184,145,58,0.12)"  },
        new() { Pattern = @"configuration changed", Label = "CFG CHG",   Color = "#b8913a", Bg = "rgba(184,145,58,0.10)"  },
        new() { Pattern = @"logged in",             Label = "LOGIN",     Color = "#a8b4cc", Bg = "rgba(168,180,204,0.08)" },
        new() { Pattern = @"logged out|inactivity", Label = "LOGOUT",    Color = "#556070", Bg = "rgba(85,96,112,0.10)"   },
        new() { Pattern = @"log file cleared",      Label = "LOG CLR",   Color = "#556070", Bg = "rgba(85,96,112,0.08)"   },
    ];

    public static List<QuickFilterEntry> DefaultQuickFilters() =>
    [
        new() { Label = "Link",   Query = "link"          },
        new() { Label = "NTP",    Query = "ntp"           },
        new() { Label = "Admin",  Query = "admin"         },
        new() { Label = "Config", Query = "configuration" },
    ];
}

public class SwitchNameEntry
{
    public string Ip   { get; set; } = "";
    public string Name { get; set; } = "";
}

public class MessageTypeEntry : INotifyPropertyChanged
{
    private string _pattern = "", _label = "",
                   _color = "#a8b4cc", _bg = "rgba(168,180,204,0.08)";

    public string Pattern { get => _pattern; set { _pattern = value; OnPC(); } }
    public string Label   { get => _label;   set { _label   = value; OnPC(); } }
    public string Color   { get => _color;   set { _color   = value; OnPC(); } }
    public string Bg      { get => _bg;      set { _bg      = value; OnPC(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPC([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class QuickFilterEntry
{
    public string Label { get; set; } = "";
    public string Query { get; set; } = "";
}
