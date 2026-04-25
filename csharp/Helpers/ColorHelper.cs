using System.Text.RegularExpressions;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace ScalanceLogs.Helpers;

public static class ColorHelper
{
    private static readonly Regex RgbaRe =
        new(@"rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)", RegexOptions.Compiled);

    public static SolidColorBrush ParseBrush(string css)
    {
        if (string.IsNullOrWhiteSpace(css)) return Brushes.Transparent;

        if (css.StartsWith('#') && css.Length == 7)
        {
            try
            {
                var r = Convert.ToByte(css[1..3], 16);
                var g = Convert.ToByte(css[3..5], 16);
                var b = Convert.ToByte(css[5..7], 16);
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            }
            catch { return Brushes.Transparent; }
        }

        var m = RgbaRe.Match(css);
        if (m.Success)
        {
            var r = byte.Parse(m.Groups[1].Value);
            var g = byte.Parse(m.Groups[2].Value);
            var b = byte.Parse(m.Groups[3].Value);
            var a = m.Groups[4].Success
                ? (byte)Math.Round(double.Parse(m.Groups[4].Value,
                    System.Globalization.CultureInfo.InvariantCulture) * 255)
                : (byte)255;
            return new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
        }

        return Brushes.Transparent;
    }
}
