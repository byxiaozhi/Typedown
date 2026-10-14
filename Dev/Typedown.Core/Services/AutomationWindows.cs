using System;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Automation;
using Typedown.Core.Models;
using Typedown.Core.ViewModels;
using Windows.UI.Core;

namespace Typedown.Core.Services
{
    /// <summary>
    /// The windows the automation API can address (docs/automation-api-spec.md). Every window registers once it
    /// has loaded and unregisters when it closes; the service threads reach a window's tabs and documents only
    /// through its dispatcher. Registration is cheap and happens whether or not the service is enabled.
    /// </summary>
    public static class AutomationWindows
    {
        public static WindowRegistry<AppViewModel> Registry { get; } = new();

        public static string Register(AppViewModel app, CoreDispatcher dispatcher) => Registry.Register(app, new UiDispatcher(dispatcher));

        public static void Unregister(AppViewModel app) => Registry.Unregister(app);

        /// <summary>The window holding a document, and its tab; null when no open window has it.</summary>
        public static async Task<(RegisteredWindow<AppViewModel> Window, DocumentTab Tab)?> FindDocumentAsync(string documentId)
        {
            var found = await Registry.FindAsync(app => app.TabsViewModel?.Tabs.FirstOrDefault(t => t.DocumentId == documentId));
            return found == null ? null : (found.Value.Window, found.Value.Value);
        }

        private sealed class UiDispatcher : IUiDispatcher
        {
            private readonly CoreDispatcher dispatcher;

            public UiDispatcher(CoreDispatcher dispatcher) => this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            // A dispatcher shut down together with its window may never run the work: callers bound every call with
            // the request's deadline rather than rely on this task completing.
            public Task<T> InvokeAsync<T>(Func<T> work)
            {
                var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try { done.SetResult(work()); }
                    catch (Exception e) { done.SetException(e); }
                });
                return done.Task;
            }
        }
    }
}
