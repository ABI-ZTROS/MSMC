using System;
using System.Windows.Media;

namespace io.NET.ZTR_OS.Features.Settings.Colors;

public static class ColorHelper
    {
        public static string ToRgbHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public static string ToArgbHex(Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public static Color FromRgbHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return global::System.Windows.Media.Colors.Blue;

            var h = hex.Trim().TrimStart('#');

            if (h.Length == 6)
            {
                var r = Convert.ToByte(h.Substring(0, 2), 16);
                var g = Convert.ToByte(h.Substring(2, 2), 16);
                var b = Convert.ToByte(h.Substring(4, 2), 16);
                return Color.FromRgb(r, g, b);
            }

            if (h.Length == 8)
            {
                var a = Convert.ToByte(h.Substring(0, 2), 16);
                var r = Convert.ToByte(h.Substring(2, 2), 16);
                var g = Convert.ToByte(h.Substring(4, 2), 16);
                var b = Convert.ToByte(h.Substring(6, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }

            if (h.Length == 3)
            {
                var r = Convert.ToByte(new string(h[0], 2), 16);
                var g = Convert.ToByte(new string(h[1], 2), 16);
                var b = Convert.ToByte(new string(h[2], 2), 16);
                return Color.FromRgb(r, g, b);
            }

            return global::System.Windows.Media.Colors.Blue;
        }

        public static string NormalizeHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return "#3B82F6";
            var color = FromRgbHex(hex);
            return ToRgbHex(color);
        }

        public static bool IsValidHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return false;
            var h = hex.Trim().TrimStart('#');
            return h.Length == 6 || h.Length == 3 || h.Length == 8;
        }
    }
}
