using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI.Enums
{
    // Ported verbatim from Typedown.Core\Enums\StartupAction.cs.
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
