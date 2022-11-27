using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Enums
{
    public enum ExportType
    {
        [Locale("None")]
        None = 0,

        [Locale("Export.Types.PDF")]
        PDF = 1,

        [Locale("Export.Types.HTML")]
        HTML = 2,

        [Locale("Export.Types.Image")]
        Image = 3,
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<ExportType> ExportTypes { get; } = Enum.GetValues(typeof(ExportType)).Cast<ExportType>().ToList();

        public static IReadOnlyList<ExportType> AvailableExportTypes { get; } = new List<ExportType>() { ExportType.PDF, ExportType.HTML };
    }
}
