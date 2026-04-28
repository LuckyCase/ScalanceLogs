using System.Text.RegularExpressions;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace SyslogViewer.Helpers;

public static class ColorHelper
{
    private static readonly Regex RgbaRe =
        new(@"rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)", RegexOptions.Compiled);

    public static SolidColorBrush ParseBrush(string css)
    {
        if (string.IsNullOrWhiteSpace(css)) return Brushes.Transparent;

        if (css.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            return Brushes.Transparent;

        if (css.StartsWith('#'))
        {
            try
            {
                var hex = css[1..];
                byte r, g, b, a = 255;
                switch (hex.Length)
                {
                    case 3:                                          // #RGB
                        r = (byte)(Convert.ToByte(hex[..1], 16) * 17);
                        g = (byte)(Convert.ToByte(hex[1..2], 16) * 17);
                        b = (byte)(Convert.ToByte(hex[2..3], 16) * 17);
                        break;
                    case 6:                                          // #RRGGBB
                        r = Convert.ToByte(hex[..2], 16);
                        g = Convert.ToByte(hex[2..4], 16);
                        b = Convert.ToByte(hex[4..6], 16);
                        break;
                    case 8:                                          // #RRGGBBAA
                        r = Convert.ToByte(hex[..2], 16);
                        g = Convert.ToByte(hex[2..4], 16);
                        b = Convert.ToByte(hex[4..6], 16);
                        a = Convert.ToByte(hex[6..8], 16);
                        break;
                    default:
                        return Brushes.Transparent;
                }
                return new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
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
