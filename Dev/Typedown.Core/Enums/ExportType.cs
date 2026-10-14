using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.Core.Utilities;

namespace Typedown.Core.Enums
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

    /// <summary>What happens once an export is written (Settings > Export > After export).</summary>
    public enum ExportAfterAction
    {
        [Locale("NoAction")]
        None = 0,

        [Locale("Export.AfterExport.Notify")]
        Notify = 1,

        [Locale("Export.AfterExport.OpenFile")]
        OpenFile = 2,

        [Locale("Export.AfterExport.OpenFolder")]
        OpenFolder = 3,
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<ExportAfterAction> ExportAfterActions { get; } = Enum.GetValues(typeof(ExportAfterAction)).Cast<ExportAfterAction>().ToList();

        public static IReadOnlyList<ExportType> ExportTypes { get; } = Enum.GetValues(typeof(ExportType)).Cast<ExportType>().ToList();

        public static IReadOnlyList<ExportType> AvailableExportTypes { get; } = new List<ExportType>() { ExportType.PDF, ExportType.HTML };
    }
}
