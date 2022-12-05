using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
