using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Enums
{
    public enum AppTheme
    {
        [Localize("AppTheme/Default")]
        Default = 0,

        [Localize("AppTheme/Light")]
        Light = 1,

        [Localize("AppTheme/Dark")]
        Dark = 2,
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<AppTheme> AppThemes { get; } = Enum.GetValues(typeof(AppTheme)).Cast<AppTheme>().ToList();
    }
}
