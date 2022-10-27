using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class InvokeEventArgs : System.EventArgs
    {
        public string Name { get; }

        public JToken Args { get; }

        public object Result { get; set; }

        public InvokeEventArgs(string name, JToken args)
        {
            Name = name;
            Args = args;
        }
    }
}
