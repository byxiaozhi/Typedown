using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;
using Typedown.Core.Utilities;

namespace Typedown.Core.Enums
{
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
