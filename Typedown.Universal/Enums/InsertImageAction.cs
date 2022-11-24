using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Enums
{
    public enum InsertImageAction
    {
        [Locale("NoAction")]
        None,

        [Locale("InsertImageAction/CopyToPath")]
        CopyToPath,

        [Locale("InsertImageAction/Upload")]
        Upload,
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<InsertImageAction> InsertImageActions { get; } = Enum.GetValues(typeof(InsertImageAction)).Cast<InsertImageAction>().ToList();
    }
}
