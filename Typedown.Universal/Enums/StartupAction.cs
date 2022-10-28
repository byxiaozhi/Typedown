using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Enums
{
    public enum FileStartupAction
    {
        [Localize("NoAction")]
        None,

        [Localize("FileStartupAction/OpenLast")]
        OpenLast
    }

    public enum FolderStartupAction
    {
        [Localize("NoAction")]
        None,

        [Localize("FolderStartupAction/OpenLast")]
        OpenLast,

        [Localize("FolderStartupAction/OpenFolder")]
        OpenFolder
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<FileStartupAction> FileStartupActions { get; } = Enum.GetValues(typeof(FileStartupAction)).Cast<FileStartupAction>().ToList();

        public static IReadOnlyList<FolderStartupAction> FolderStartupActions { get; } = Enum.GetValues(typeof(FolderStartupAction)).Cast<FolderStartupAction>().ToList();
    }
}
