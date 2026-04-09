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
    public enum AppTheme
    {
        [Locale("UseSystemSetting")]
        Default = 0,

        [Locale("View.AppTheme.Light")]
        Light = 1,

        [Locale("View.AppTheme.Dark")]
        Dark = 2,
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<AppTheme> AppThemes { get; } = Enum.GetValues(typeof(AppTheme)).Cast<AppTheme>().ToList();
    }
}
