using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Typedown.Automation
{
    /// <summary>Runs work on the thread that owns a window's state (its UI dispatcher).</summary>
    public interface IUiDispatcher
    {
        Task<T> InvokeAsync<T>(Func<T> work);
    }

    /// <summary>A registered window as the automation service sees it from any thread.</summary>
    public sealed class RegisteredWindow<TWindow> where TWindow : class
    {
        internal RegisteredWindow(string windowId, TWindow window, IUiDispatcher dispatcher, long order)
        {
            WindowId = windowId;
            Window = window;
            Dispatcher = dispatcher;
            Order = order;
        }

        /// <summary>Stable from the window's creation to its close; never reused within the process.</summary>
        public string WindowId { get; }

        /// <summary>Only to be touched on <see cref="Dispatcher"/>.</summary>
        public TWindow Window { get; }

        public IUiDispatcher Dispatcher { get; }

        /// <summary>Registration order, for a stable listing.</summary>
        public long Order { get; }
    }

    /// <summary>
    /// The windows of this process, safe to use from the automation service's threads. The registry itself only
    /// maps ids to windows; everything a window holds (its tabs and documents) is read on that window's own
    /// dispatcher, and a window that closes while work is queued for it answers <c>window_not_found</c> rather
    /// than running the work on a torn-down window.
    /// </summary>
    public sealed class WindowRegistry<TWindow> where TWindow : class
    {
        private readonly object gate = new();
        private readonly Dictionary<string, RegisteredWindow<TWindow>> byId = new(StringComparer.Ordinal);
        private readonly Dictionary<TWindow, RegisteredWindow<TWindow>> byWindow = new(ByReference.Instance);
        private long order;

        private sealed class ByReference : IEqualityComparer<TWindow>
        {
            public static readonly ByReference Instance = new();
            public bool Equals(TWindow? x, TWindow? y) => ReferenceEquals(x, y);
            public int GetHashCode(TWindow obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>Registers a window (again registering the same one returns its existing id).</summary>
        public string Register(TWindow window, IUiDispatcher dispatcher)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            lock (gate)
            {
                if (byWindow.TryGetValue(window, out var existing)) return existing.WindowId;
                var entry = new RegisteredWindow<TWindow>("w" + Guid.NewGuid().ToString("N"), window, dispatcher, Interlocked.Increment(ref order));
                byId.Add(entry.WindowId, entry);
                byWindow.Add(window, entry);
                return entry.WindowId;
            }
        }

        /// <summary>Forgets a window; work already queued for it then fails with <c>window_not_found</c>.</summary>
        public bool Unregister(TWindow window)
        {
            lock (gate)
            {
                if (window == null || !byWindow.TryGetValue(window, out var entry)) return false;
                byWindow.Remove(window);
                byId.Remove(entry.WindowId);
                return true;
            }
        }

        public string? IdOf(TWindow window)
        {
            lock (gate) return window != null && byWindow.TryGetValue(window, out var entry) ? entry.WindowId : null;
        }

        /// <summary>The registered windows in registration order.</summary>
        public IReadOnlyList<RegisteredWindow<TWindow>> Snapshot()
        {
            lock (gate) return byId.Values.OrderBy(w => w.Order).ToList();
        }

        private bool IsRegistered(RegisteredWindow<TWindow> entry)
        {
            lock (gate) return byId.TryGetValue(entry.WindowId, out var current) && ReferenceEquals(current, entry);
        }

        private static AutomationException WindowNotFound(string windowId) =>
            new(AutomationErrorKind.window_not_found, "The window is closed or the id is not valid.", new Dictionary<string, object?> { ["windowId"] = windowId });

        /// <summary>Runs <paramref name="work"/> on the window's dispatcher.</summary>
        /// <exception cref="AutomationException"><c>window_not_found</c> when the id is unknown or the window closed before the work ran.</exception>
        public Task<T> OnWindowAsync<T>(string windowId, Func<TWindow, T> work)
        {
            RegisteredWindow<TWindow>? entry;
            lock (gate) byId.TryGetValue(windowId ?? "", out entry);
            if (entry == null) return Task.FromException<T>(WindowNotFound(windowId ?? ""));
            return RunAsync(entry, work);
        }

        private async Task<T> RunAsync<T>(RegisteredWindow<TWindow> entry, Func<TWindow, T> work)
        {
            Task<T> task;
            try
            {
                task = entry.Dispatcher.InvokeAsync(() =>
                {
                    // Checked again on the window's own thread: it may have closed while this waited in its queue.
                    if (!IsRegistered(entry)) throw WindowNotFound(entry.WindowId);
                    return work(entry.Window);
                });
            }
            catch (Exception) when (!IsRegistered(entry))
            {
                // A dispatcher that has shut down may refuse the work outright.
                throw WindowNotFound(entry.WindowId);
            }
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Asks each window in turn, on its own dispatcher, and returns the first non-null answer with the window
        /// that gave it; null when none did. Windows that close meanwhile are skipped.
        /// </summary>
        public async Task<(RegisteredWindow<TWindow> Window, T Value)?> FindAsync<T>(Func<TWindow, T?> probe) where T : class
        {
            foreach (var entry in Snapshot())
            {
                T? value;
                try { value = await RunAsync(entry, probe).ConfigureAwait(false); }
                catch (AutomationException e) when (e.Kind == AutomationErrorKind.window_not_found) { continue; }
                if (value != null) return (entry, value);
            }
            return null;
        }
    }
}
