using Newtonsoft.Json.Linq;

namespace Typedown.WinUI.Models
{
    // Ported verbatim from Typedown.Core\Models\EventArgs\EditorEventArgs.cs — no UWP dependencies, no changes needed.
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
