using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Typedown.Controls;
using Typedown.Universal.Models;
using Typedown.Universal.Services;

namespace Typedown.Services
{
    public class Transport
    {
        private readonly Dictionary<string, string> prevDic = new();

        public IServiceProvider ServiceProvider { get; }

        public RemoteInvoke RemoteInvoke => ServiceProvider.GetService<RemoteInvoke>();

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public Transport(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public async void EmitWebViewMessage(MarkdownEditor sender, string json)
        {
            var jObject = JObject.Parse(json);
            var name = jObject["name"].ToString();
            var args = jObject["args"];
            var type = jObject["type"].ToString();
            switch (type)
            {
                case "invoke":
                    var id = jObject["id"].ToString();
                    try
                    {
                        var ret = await RemoteInvoke.Invoke(name, args);
                        sender.PostMessage(id, new { code = 0, data = ret });
                    }
                    catch (Exception ex)
                    {
                        sender.PostMessage(id, new { code = 1, msg = ex.Message });
                    }
                    break;
                case "message":
                    EventCenter.EmitEvent(name, new EditorEventArgs(name, args));
                    break;
                case "diffmsg":
                    var diff = jObject["diff"].ToObject<bool>();
                    if (diff)
                    {
                        var start = jObject["start"].ToObject<int>();
                        var end = jObject["end"].ToObject<int>();
                        prevDic[name] = prevDic[name][..start] + args + prevDic[name][end..];
                    }
                    else
                    {
                        prevDic[name] = args.ToString();
                    }
                    EventCenter.EmitEvent(name, new EditorEventArgs(name, JToken.Parse(prevDic[name])));
                    break;
            }
        }
    }
}
