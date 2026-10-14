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
        // The last full text of each diffed message, per editor page: a page sends a message as the change from its own
        // previous one. One table for the window was shared by two pages when the main page was built again while the old
        // one still sent (a language change): one page's change was applied to the other's text, and the result no
        // longer parsed. Held weakly, so a page that goes away takes its table with it.
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IMarkdownEditor, Dictionary<string, string>> previous = new();
        private readonly Dictionary<string, string> previousOfNone = new();

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
            // async void: an exception here would end the process. A message that cannot be read is dropped and logged.
            try
            {
                await Emit(sender, json);
            }
            catch (Exception ex)
            {
                Typedown.Core.Utilities.Log.Debug($"editor message dropped: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task Emit(IMarkdownEditor sender, string json)
        {
            var msg = JsonConvert.DeserializeObject<EditorMessage>(json, Core.Config.EditorJsonSerializerSettings);
            var prevDic = sender == null ? previousOfNone : previous.GetValue(sender, _ => new Dictionary<string, string>());
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
                    {
                        // A change needs the text it was made from: without it (or out of its range) the message is dropped
                        // and logged, not applied to another text.
                        if (!prevDic.TryGetValue(msg.Name, out var baseText) || msg.Start < 0 || msg.End < msg.Start || msg.End > baseText.Length)
                            throw new InvalidOperationException($"'{msg.Name}' changed from a text this page did not send");
                        prevDic[msg.Name] = baseText.Substring(0, msg.Start) + msg.Args + baseText.Substring(msg.End);
                    }
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
