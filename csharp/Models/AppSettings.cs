namespace ScalanceLogs.Models;

public class AppSettings
{
    public int    UdpPort           { get; set; } = 514;
    public int    LogRetentionDays  { get; set; } = 30;
    public string LogPath           { get; set; } = "logs";
    public bool   AutoStart         { get; set; } = false;
    public bool   BalloonNotifications { get; set; } = true;

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

public class MessageTypeEntry
{
    public string Pattern { get; set; } = "";
    public string Label   { get; set; } = "";
    public string Color   { get; set; } = "#a8b4cc";
    public string Bg      { get; set; } = "rgba(168,180,204,0.08)";
}

public class QuickFilterEntry
{
    public string Label { get; set; } = "";
    public string Query { get; set; } = "";
}
