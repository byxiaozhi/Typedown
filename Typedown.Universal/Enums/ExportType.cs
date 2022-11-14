using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Enums
{
    public enum ExportType
    {
        [Localize("None")]
        None = 0,

        [Localize("ExportType/PDF")]
        PDF = 1,

        [Localize("ExportType/HTML")]
        HTML = 2,

        [Localize("ExportType/Image")]
        Image = 3,
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<ExportType> ExportTypes { get; } = Enum.GetValues(typeof(ExportType)).Cast<ExportType>().ToList();
    }
}
