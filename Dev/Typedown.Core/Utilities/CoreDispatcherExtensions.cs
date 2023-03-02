using System;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Typedown.Core.Utilities
{
    public static class CoreDispatcherExtensions
    {
        public static Task<T> RunAsync<T>(this CoreDispatcher dispatcher, Func<T> action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            var task = new TaskCompletionSource<T>();
            _ = dispatcher.RunAsync(priority, () =>
            {
                try
                {
                    task.SetResult(action());
                }
                catch (Exception ex)
                {
                    task.SetException(ex);
                }
            });
            return task.Task;
        }

        public static Task RunAsync(this CoreDispatcher dispatcher, Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            var task = new TaskCompletionSource<object>();
            _ = dispatcher.RunAsync(priority, () =>
            {
                try
                {
                    action();
                    task.SetResult(null);
                }
                catch (Exception ex)
                {
                    task.SetException(ex);
                }
            });
            return task.Task;
        }

        public static Task<T> RunIdleAsync<T>(this CoreDispatcher dispatcher, Func<T> action)
        {
            var task = new TaskCompletionSource<T>();
            _ = dispatcher.RunIdleAsync(_ =>
            {
                try
                {
                    task.SetResult(action());
                }
                catch (Exception ex)
                {
                    task.SetException(ex);
                }
            });
            return task.Task;
        }

        public static Task RunIdleAsync(this CoreDispatcher dispatcher, Action action)
        {
            var task = new TaskCompletionSource<object>();
            _ = dispatcher.RunIdleAsync(_ =>
            {
                try
                {
                    action();
                    task.SetResult(null);
                }
                catch (Exception ex)
                {
                    task.SetException(ex);
                }
            });
            return task.Task;
        }
    }
}
