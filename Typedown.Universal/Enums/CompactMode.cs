using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Enums
{
    public enum AppCompactMode
    {
        [Localize("Off")]
        None,

        [Localize("AppCompactMode/MergeTitleBar")]
        MergeTitleBar,

        [Localize("AppCompactMode/MergeTitleBarAndMenuBar")]
        MergeTitleBarAndMenuBar,
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<AppCompactMode> AppCompactModes { get; } = Enum.GetValues(typeof(AppCompactMode)).Cast<AppCompactMode>().ToList();
    }
}
