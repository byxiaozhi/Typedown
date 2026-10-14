using System.Collections.Generic;

namespace Typedown.Core.Services
{
    /// <summary>
    /// Which settings belong to one window rather than the application. They are still saved (the next window opens
    /// the way the last one was left), but a change in one window is not applied to the others: switching one window
    /// to source mode, or moving its side pane, must not rearrange every other window. Everything else - theme, font,
    /// editor behaviour, language - is an application preference and reaches every open window at once.
    /// </summary>
    public static class SettingsScope
    {
        private static readonly HashSet<string> windowLocal = new()
        {
            "StartupPlacement",
            "SidePaneOpen",
            "SidePaneWidth",
            "SidePaneIndex",
            "StatusBarOpen",
            "FindReplaceDialogWidth",
            "SourceCode",
            "ReadOnly",
            "Typewriter",
            "FocusMode",
            "SearchIsCaseSensitive",
            "SearchIsRegexp",
            "SearchIsWholeWord",
            "Topmost",
        };

        public static bool IsWindowLocal(string name) => name != null && windowLocal.Contains(name);

        public static IReadOnlyCollection<string> WindowLocalNames => windowLocal;
    }
}
