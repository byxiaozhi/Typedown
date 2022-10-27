using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class MenuState
    {
        public bool IsDisabled { get; set; }
        public bool IsMultiline { get; set; }
        public bool IsLooseListItem { get; set; }
        public bool IsTaskList { get; set; }
        public bool IsCodeFences { get; set; }
        public bool IsCodeContent { get; set; }
        public bool IsTable { get; set; }
        public JToken Affiliation { get; set; }
    }
}
