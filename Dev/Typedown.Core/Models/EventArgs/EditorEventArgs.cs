using Newtonsoft.Json.Linq;

namespace Typedown.Core.Models
{
    public class EditorEventArgs : global::System.EventArgs
    {
        public string Name { get; }

        public JToken Args { get; }

        public EditorEventArgs(string name, JToken args)
        {
            Name = name;
            Args = args;
        }
    }
}
