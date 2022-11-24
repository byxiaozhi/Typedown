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
        [Locale("FileStartupAction/NewFile")]
        None,

        [Locale("FileStartupAction/OpenLast")]
        OpenLast
    }

    public enum FolderStartupAction
    {
        [Locale("NoAction")]
        None,

        [Locale("FolderStartupAction/OpenLast")]
        OpenLast,

        [Locale("FolderStartupAction/OpenFolder")]
        OpenFolder
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<FileStartupAction> FileStartupActions { get; } = Enum.GetValues(typeof(FileStartupAction)).Cast<FileStartupAction>().ToList();

        public static IReadOnlyList<FolderStartupAction> FolderStartupActions { get; } = Enum.GetValues(typeof(FolderStartupAction)).Cast<FolderStartupAction>().ToList();
    }
}
