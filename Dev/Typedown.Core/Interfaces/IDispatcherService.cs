using System;
using System.Threading;

namespace Typedown.Core.Interfaces
{
    /// <summary>
    /// Cross-platform dispatcher service for posting work to the UI thread.
    /// </summary>
    public interface IDispatcherService
    {
        /// <summary>Post an action to run on the UI thread.</summary>
        void Post(Action action);

        /// <summary>Get the SynchronizationContext for the UI thread.</summary>
        SynchronizationContext SynchronizationContext { get; }
    }
}
