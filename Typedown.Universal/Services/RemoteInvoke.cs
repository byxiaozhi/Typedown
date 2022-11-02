using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace Typedown.Universal.Services
{
    public class RemoteInvoke : IDisposable
    {
        public delegate object FunctionHandler(JToken args);

        public delegate Task<object> FunctionHandlerAsync(JToken args);

        public delegate void ActionHandler(JToken args);

        public delegate Task ActionHandlerAsync(JToken args);

        private record Handler(object Source, Func<JToken, Task<object>> Func);

        private readonly Dictionary<string, Handler> handlerDic = new();

        public IDisposable Handle(string name, Action handler)
        {
            handlerDic[name] = new(handler, _ =>
            {
                handler();
                return Task.FromResult<object>(null);
            });
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public IDisposable Handle<T>(string name, Action<T> handler)
        {
            handlerDic[name] = new(handler, x =>
            {
                handler(x.ToObject<T>());
                return Task.FromResult<object>(null);
            });
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public IDisposable Handle<T, TResult>(string name, Func<T, TResult> handler)
        {
            handlerDic[name] = new(handler, x => Task.FromResult<object>(handler(x.ToObject<T>())));
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public IDisposable Handle<T, TResult>(string name, Func<T, Task<TResult>> handler)
        {
            handlerDic[name] = new(handler, async x => await handler(x.ToObject<T>()));
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public IDisposable Handle<TResult>(string name, Func<TResult> handler)
        {
            handlerDic[name] = new(handler, async x => await Task.FromResult(handler()));
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public IDisposable Handle<TResult>(string name, Func<Task<TResult>> handler)
        {
            handlerDic[name] = new(handler, async x => await handler());
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public void RemoveHandle(string name, object handler)
        {
            if (handlerDic.TryGetValue(name, out var val) && val.Source == handler)
                handlerDic.Remove(name);
        }

        public async Task<object> Invoke(string name, JToken args)
        {
            if (handlerDic.TryGetValue(name, out var handler))
                return await handler.Func(args);
            throw new Exception($"function [{name}] does not exist");
        }

        public void Dispose()
        {
            handlerDic.Clear();
        }
    }
}
