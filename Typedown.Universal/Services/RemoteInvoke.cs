using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Services
{
    public class RemoteInvoke : IDisposable
    {
        public delegate object FunctionHandler(JToken args);

        public delegate Task<object> FunctionHandlerAsync(JToken args);

        public delegate void ActionHandler(JToken args);

        public delegate Task ActionHandlerAsync(JToken args);

        private readonly Dictionary<string, object> handlerDic = new();

        public IDisposable Handle(string name, FunctionHandler handler)
        {
            handlerDic[name] = handler;
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public IDisposable Handle(string name, FunctionHandlerAsync handler)
        {
            handlerDic[name] = handler;
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public IDisposable Handle(string name, ActionHandler handler)
        {
            handlerDic[name] = handler;
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public IDisposable Handle(string name, ActionHandlerAsync handler)
        {
            handlerDic[name] = handler;
            return Disposable.Create(() => RemoveHandle(name, handler));
        }

        public void RemoveHandle(string name, object handler)
        {
            if (handlerDic.TryGetValue(name, out var val) && val == handler)
                handlerDic.Remove(name);
        }

        public async Task<object> Invoke(string name, JToken args)
        {
            if (handlerDic.TryGetValue(name, out var func))
            {
                if (func is FunctionHandler functionHandler)
                    return functionHandler(args);
                else if (func is FunctionHandlerAsync functionHandlerAsync)
                    return await functionHandlerAsync(args);
                else if (func is ActionHandler actionHandler)
                    actionHandler(args);
                else if (func is ActionHandlerAsync actionHandlerAsync)
                    await actionHandlerAsync(args);
            }
            throw new Exception($"function [{name}] does not exist");
        }

        public void Dispose()
        {
            handlerDic.Clear();
        }
    }
}
