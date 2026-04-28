using SyslogViewer.Helpers;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SyslogViewer.Converters;

[ValueConversion(typeof(string), typeof(Brush))]
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s ? ColorHelper.ParseBrush(s) : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
