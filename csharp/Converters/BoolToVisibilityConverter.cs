using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyslogViewer.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        value is Visibility.Visible;
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        value is not Visibility.Visible;
}

[ValueConversion(typeof(bool), typeof(string))]
public class BoolToStringConverter : IValueConverter
{
    public string TrueValue  { get; set; } = "";
    public string FalseValue { get; set; } = "";

    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? TrueValue : FalseValue;

    public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
        value?.ToString() == TrueValue;
}
