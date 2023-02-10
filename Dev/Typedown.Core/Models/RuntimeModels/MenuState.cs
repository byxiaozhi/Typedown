using System.Collections.Generic;

namespace Typedown.Core.Models
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
        public bool IsFootnote { get; set; }
        public Dictionary<string, bool> Affiliation { get; set; }
    }
}
