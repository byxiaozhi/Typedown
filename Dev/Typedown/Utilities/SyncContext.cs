using System.Threading;

namespace Typedown.Utilities
{
    public class SyncContext : SynchronizationContext
    {
        private readonly Dispatcher dispatcher;

        public SyncContext(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            dispatcher.InvokeAsync(() => d.Invoke(state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            dispatcher.InvokeAsync(() => d.Invoke(state)).Wait();
        }
    }
}
