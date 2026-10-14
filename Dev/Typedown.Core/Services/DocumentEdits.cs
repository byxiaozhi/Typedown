using Typedown.Automation;
using Typedown.Core.Utilities;

namespace Typedown.Core.Services
{
    /// <summary>
    /// The one edit coordinator of the application: the automation methods and the application's own whole-document
    /// edits (File > Upload local images) take their turns on a document through it, so neither overwrites the other
    /// and each is one undo step.
    /// </summary>
    public static class DocumentEdits
    {
        private static readonly object gate = new();
        private static DocumentEditCoordinator coordinator;

        public static DocumentEditCoordinator Coordinator
        {
            get { lock (gate) return coordinator ??= Create(null); }
        }

        /// <summary>The automation host's coordinator, with the test host's barriers when it has them.</summary>
        public static DocumentEditCoordinator Start(IEditBarriers barriers)
        {
            lock (gate)
            {
                if (coordinator == null || barriers != null)
                    coordinator = Create(barriers);
                return coordinator;
            }
        }

        private static DocumentEditCoordinator Create(IEditBarriers barriers) =>
            new(WindowsAutomationHost.EditorClassifierVersion, barriers) { Diagnostics = message => Log.Debug("automation: " + message) };
    }
}
