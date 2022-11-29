using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Enums
{
    public enum PrintOrientation
    {
        [Locale("Export.PDFConfig.Orientations.Portrait")]
        Portrait = 0,

        [Locale("Export.PDFConfig.Orientations.Landscape")]
        Landscape = 1
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<PrintOrientation> PrintOrientations { get; } = Enum.GetValues(typeof(PrintOrientation)).Cast<PrintOrientation>().ToList();
    }
}
