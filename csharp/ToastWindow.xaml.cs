using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScalanceLogs;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer;

    public ToastWindow(string title, string message, bool isWarning)
    {
        InitializeComponent();

        TitleText.Text   = title;
        MessageText.Text = message;

        if (isWarning)
        {
            var warnBrush = Application.Current.Resources["YellowBrush"] as SolidColorBrush
                            ?? new SolidColorBrush(Color.FromRgb(0xb8, 0x91, 0x3a));
            AccentBar.Background = warnBrush;
            TitleText.Foreground = warnBrush;
        }

        // Position: bottom-right corner above taskbar
        PositionWindow();

        _timer          = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick    += (_, _) => CloseToast();
        _timer.Start();

        MouseEnter += (_, _) => _timer.Stop();
        MouseLeave += (_, _) => { _timer.Interval = TimeSpan.FromSeconds(2); _timer.Start(); };
    }

    private void PositionWindow()
    {
        // Place after layout pass so ActualWidth/Height are known
        Loaded += (_, _) =>
        {
            var area   = SystemParameters.WorkArea;
            Left = area.Right  - ActualWidth  - 12;
            Top  = area.Bottom - ActualHeight - 12;
        };
    }

    private void Close_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => CloseToast();

    public void CloseToast()
    {
        _timer.Stop();
        Close();
    }
}
