using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Typedown.Universal.Utilities;
using System.Diagnostics;

namespace Typedown.Utilities
{
    public class Dispatcher
    {
        private static readonly Dictionary<uint, Dispatcher> dispatchers = new();

        private readonly BlockingCollection<Action> queue = new();

        private readonly uint threadId;

        private Dispatcher()
        {
            threadId = PInvoke.GetCurrentThreadId();
            dispatchers.Add(threadId, this);
        }

        public static Dispatcher Current => dispatchers.TryGetValue(PInvoke.GetCurrentThreadId(), out var val) ? val : null;

        public static void Run(Action entry)
        {
            var dispatcher = new Dispatcher();
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
                if (dispatcher.queue.IsAddingCompleted || 
                    msg.message == (uint)PInvoke.WindowMessage.WM_QUIT)
                {
                    break;
                }
            }
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
            lock (dispatchers) dispatchers.Remove(threadId);
            queue.CompleteAdding();
            PInvoke.PostThreadMessage(threadId, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
