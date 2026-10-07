using System;
using System.Globalization;
using System.Linq;

namespace Typedown.WinUI.Utilities
{
    // The size of an image in a note is the `zoom` in its style attribute (`<img src="a.png" style="zoom:50%;">`),
    // set from the image toolbar's Size menu. Plain .NET (no WinUI), so the tests compile it as it is.
    internal static class ImageStyle
    {
        // The zoom as a percentage ("zoom:50%;" is 50, "zoom: 0.5" is 50), or null when the style has none.
        public static double? Zoom(string style)
        {
            if (string.IsNullOrWhiteSpace(style)) return null;
            foreach (var part in style.Split(';'))
            {
                var declaration = part.Trim();
                if (!declaration.StartsWith("zoom", StringComparison.OrdinalIgnoreCase)) continue;
                var rest = declaration.Substring(4).TrimStart();
                if (!rest.StartsWith(':')) continue;
                var value = rest.Substring(1).Trim();
                if (value.EndsWith('%'))
                {
                    if (double.TryParse(value.Substring(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)) return percent;
                }
                else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor)) return factor * 100;
            }
            return null;
        }

        // The style with its zoom replaced by `zoom` ("50%"), everything else in it kept.
        public static string WithZoom(string style, string zoom)
        {
            var kept = (style ?? "").Split(';')
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Trim().StartsWith("zoom:", StringComparison.OrdinalIgnoreCase)).ToList();
            kept.Add($"zoom:{zoom}");
            return string.Join(';', kept) + ";";
        }

        // Whether the image is shown at this size from the Size menu ("50%"); an image with no zoom is at 100%.
        public static bool IsZoom(string style, string zoom)
        {
            var current = Zoom(style) ?? 100;
            return double.TryParse(zoom.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var wanted) && Math.Abs(current - wanted) < 0.5;
        }
    }
}
