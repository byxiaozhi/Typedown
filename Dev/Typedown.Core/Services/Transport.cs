using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Services;

namespace Typedown.Services
{
    public class Transport
    {
        private readonly Dictionary<string, string> prevDic = new();

        public IServiceProvider ServiceProvider { get; }

        public RemoteInvoke RemoteInvoke { get; }

        public EventCenter EventCenter { get; }

        public Transport(RemoteInvoke remoteInvoke, EventCenter eventCenter)
        {
            RemoteInvoke = remoteInvoke;
            EventCenter = eventCenter;
        }

        public async void EmitWebViewMessage(IMarkdownEditor sender, string json)
        {
            var msg = JsonConvert.DeserializeObject<EditorMessage>(json, Core.Config.EditorJsonSerializerSettings);
            switch (msg.Type)
            {
                case "invoke":
                    try
                    {
                        var ret = await RemoteInvoke.Invoke(msg.Name, msg.Args);
                        sender.PostMessage(msg.Id, new { code = 0, data = ret });
                    }
                    catch (Exception ex)
                    {
                        sender.PostMessage(msg.Id, new { code = 1, msg = ex.Message });
                    }
                    break;
                case "message":
                    EventCenter.EmitEvent(msg.Name, new EditorEventArgs(msg.Name, msg.Args));
                    break;
                case "diffmsg":
                    if (msg.Diff)
                        prevDic[msg.Name] = prevDic[msg.Name].Substring(0, msg.Start) + msg.Args + prevDic[msg.Name].Substring(msg.End);
                    else
                        prevDic[msg.Name] = msg.Args.ToString();
                    EventCenter.EmitEvent(msg.Name, new EditorEventArgs(msg.Name, JToken.Parse(prevDic[msg.Name])));
                    break;
            }
        }

        public class EditorMessage
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public JToken Args { get; set; }
            public string Type { get; set; }
            public bool Diff { get; set; }
            public int Start { get; set; }
            public int End { get; set; }
        }
    }
}
