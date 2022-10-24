using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace Typedown.Universal.Services
{
    public class EventCenter
    {
        private readonly Dictionary<string, List<Action<object>>> handlersDictionary = new();

        public IObservable<TEventArgs> GetObservable<TEventArgs>(string name)
        {
            return Observable.Create<TEventArgs>(subscribe =>
            {
                if (!handlersDictionary.ContainsKey(name))
                    handlersDictionary.Add(name, new());
                void handler(object args) => subscribe.OnNext((TEventArgs)args);
                handlersDictionary[name].Add(handler);
                return () =>
                {
                    handlersDictionary[name].Remove(handler);
                    if (handlersDictionary[name].Count == 0)
                        handlersDictionary.Remove(name);
                };
            });
        }

        public void EmitEvent(string name, object args)
        {
            if (handlersDictionary.TryGetValue(name, out var handlers))
                foreach (var handler in handlers)
                    handler(args);
        }
    }
}
