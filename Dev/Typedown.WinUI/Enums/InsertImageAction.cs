using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI.Enums
{
    // Ported verbatim from Typedown.Core\Enums\InsertImageAction.cs.
    public enum InsertImageAction
    {
        [Locale("NoAction")]
        None,

        [Locale("Image.InsertActions.CopyToPath")]
        CopyToPath,

        [Locale("Image.InsertActions.Upload")]
        Upload,
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<InsertImageAction> InsertImageActions { get; } = Enum.GetValues(typeof(InsertImageAction)).Cast<InsertImageAction>().ToList();
    }
}
