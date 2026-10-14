using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace Typedown.Core.Services
{
    public class EventCenter : IDisposable
    {
        private readonly Dictionary<string, List<Action<object>>> handlersDictionary = new();

        /// <summary>
        /// The editor page's messages of one name, as an observable. A handler that throws is logged and stays subscribed,
        /// and the other handlers of the message still get it: built on Observable.Create, a handler's exception made Rx
        /// detach that subscription for good (the outline, the selection or the word count stopped following the page
        /// until the window was opened again) and stopped the message there.
        /// </summary>
        public IObservable<TEventArgs> GetObservable<TEventArgs>(string name) => new Source<TEventArgs>(this, name);

        private sealed class Source<TEventArgs> : IObservable<TEventArgs>
        {
            private readonly EventCenter center;
            private readonly string name;

            public Source(EventCenter center, string name)
            {
                this.center = center;
                this.name = name;
            }

            public IDisposable Subscribe(IObserver<TEventArgs> observer)
            {
                void handler(object args)
                {
                    try
                    {
                        observer.OnNext((TEventArgs)args);
                    }
                    catch (Exception ex)
                    {
                        Utilities.Log.Debug($"editor message '{name}': a handler failed and stays subscribed: {ex}");
                    }
                }
                lock (center.handlersDictionary)
                {
                    if (!center.handlersDictionary.TryGetValue(name, out var list))
                        center.handlersDictionary.Add(name, list = new());
                    list.Add(handler);
                }
                return System.Reactive.Disposables.Disposable.Create(() =>
                {
                    lock (center.handlersDictionary)
                    {
                        if (center.handlersDictionary.TryGetValue(name, out var handlers))
                        {
                            handlers.Remove(handler);
                            if (handlers.Count == 0)
                                center.handlersDictionary.Remove(name);
                        }
                    }
                });
            }
        }

        public void EmitEvent(string name, object args)
        {
            // A copy: a handler may subscribe or unsubscribe while the message goes round.
            Action<object>[] handlers;
            lock (handlersDictionary)
            {
                if (!handlersDictionary.TryGetValue(name, out var list)) return;
                handlers = list.ToArray();
            }
            foreach (var handler in handlers)
                handler(args);
        }

        public void Dispose()
        {
            handlersDictionary.Clear();
        }
    }
}
