using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Typedown.Core.Utilities;

namespace Typedown.Utilities
{
    public class Dispatcher
    {
        public static Dispatcher Current => dispatchers[PInvoke.GetCurrentThreadId()];

        private static readonly ConcurrentDictionary<uint, Dispatcher> dispatchers = new();

        private readonly BlockingCollection<Action> queue = new();

        private readonly uint threadId = PInvoke.GetCurrentThreadId();

        public static void Run(Action entry)
        {
            var dispatcher = new Dispatcher();
            dispatchers[dispatcher.threadId] = dispatcher;
            dispatcher.InvokeAsync(entry);
            SynchronizationContext.SetSynchronizationContext(new SyncContext(dispatcher));
            while (PInvoke.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                if (msg.message != 0)
                {
                    PInvoke.TranslateMessage(ref msg);
                    PInvoke.DispatchMessage(ref msg);
                }
                while (dispatcher.queue.TryTake(out var action))
                {
                    action();
                }
            }
            dispatchers.Remove(dispatcher.threadId, out _);
        }

        public Task<TResult> InvokeAsync<TResult>(Func<TResult> action)
        {
            var source = new TaskCompletionSource<TResult>();
            queue.Add(() =>
            {
                try
                {
                    source.SetResult(action());
                }
                catch (Exception ex)
                {
                    source.SetException(ex);
                }
            });
            PInvoke.PostThreadMessage(threadId, 0, IntPtr.Zero, IntPtr.Zero);
            return source.Task;
        }

        public Task InvokeAsync(Action action)
        {
            return InvokeAsync(() =>
            {
                action();
                return true;
            });
        }

        public void Shutdown()
        {
            PInvoke.PostThreadMessage(threadId, (uint)PInvoke.WindowMessage.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
