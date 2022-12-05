using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Core.Utilities;

namespace Typedown.Core.Enums
{
    public enum FileStartupAction
    {
        [Locale("General.StartupAction.FileStartupAction.NewFile")]
        None,

        [Locale("General.StartupAction.FileStartupAction.OpenLast")]
        OpenLast
    }

    public enum FolderStartupAction
    {
        [Locale("NoAction")]
        None,

        [Locale("General.StartupAction.FolderStartupAction.OpenLast")]
        OpenLast,

        [Locale("General.StartupAction.FolderStartupAction.OpenFolder")]
        OpenFolder
    }

    public static partial class Enumerable
    {
        public static IReadOnlyList<FileStartupAction> FileStartupActions { get; } = Enum.GetValues(typeof(FileStartupAction)).Cast<FileStartupAction>().ToList();

        public static IReadOnlyList<FolderStartupAction> FolderStartupActions { get; } = Enum.GetValues(typeof(FolderStartupAction)).Cast<FolderStartupAction>().ToList();
    }
}
