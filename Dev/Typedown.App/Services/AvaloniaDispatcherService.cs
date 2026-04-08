using System;
using System.Threading;
using Avalonia.Threading;
using Typedown.Core.Interfaces;

namespace Typedown.App.Services;

/// <summary>
/// Avalonia implementation of IDispatcherService — posts work to the UI thread via Dispatcher.UIThread.
/// </summary>
public class AvaloniaDispatcherService : IDispatcherService
{
    public void Post(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    public SynchronizationContext SynchronizationContext =>
        SynchronizationContext.Current ?? new SynchronizationContext();
}
