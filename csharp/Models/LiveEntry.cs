using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScalanceLogs.Models;

public class LiveEntry : INotifyPropertyChanged
{
    public string TimeStr    { get; set; } = "";
    public string SwitchName { get; set; } = "";
    public string Message    { get; set; } = "";
    public string ChipLabel  { get; set; } = "";
    public Brush  ChipFg     { get; set; } = Brushes.Gray;
    public Brush  ChipBg     { get; set; } = Brushes.Transparent;
    public Brush  RowBorder  { get; set; } = Brushes.Transparent;
    public DateTime AddedAt  { get; set; } = DateTime.Now;

    private double _opacity = 1.0;
    public double Opacity
    {
        get => _opacity;
        set { _opacity = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
