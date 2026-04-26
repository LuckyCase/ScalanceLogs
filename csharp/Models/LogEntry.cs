using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScalanceLogs.Models;

public class LogEntry : INotifyPropertyChanged
{
    public string Raw          { get; set; } = "";
    public string Timestamp    { get; set; } = "";
    public string Severity     { get; set; } = "INFO";   // original syslog severity (for stats)
    public string SeverityText { get; set; } = "INFO";   // shown text (legacy)
    public bool   IsLinkDown   { get; set; }
    public bool   IsLinkUp     { get; set; }
    public string Host         { get; set; } = "";
    public string IpPart       { get; set; } = "";
    public string HostSuffix   { get; set; } = "";
    public string Message      { get; set; } = "";
    public string ChipLabel    { get; set; } = "";
    public Brush  ChipFg       { get; set; } = Brushes.Gray;
    public Brush  ChipBg       { get; set; } = Brushes.Transparent;
    public Brush  RowBg        { get; set; } = Brushes.Transparent;
    public Brush  RowBorder    { get; set; } = Brushes.Transparent;

    private bool _expanded;
    public bool IsExpanded
    {
        get => _expanded;
        set { _expanded = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
