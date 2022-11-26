using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class MarkdownBlock
    {
        public string Key { get; set; }

        public string Text { get; set; }

        public string Type { get; set; }

        public bool Editable { get; set; }

        public string Parent { get; set; }

        public string PreSibling { get; set; }

        public string NextSibling { get; set; }

        public string HeadingStyle { get; set; }

        public string Marker { get; set; }

        public string Lang { get; set; }

        public string Style { get; set; }

        public string MathStyle { get; set; }

        public string FunctionType { get; set; }

        public int Row { get; set; }

        public int Column { get; set; }

        public string Align { get; set; }

        public bool IsLooseListItem { get; set; }

        public string BulletMarkerOrDelimiter { get; set; }

        public int? Start { get; set; }

        public string ListItemType { get; set; }

        public bool Checked { get; set; }

        public List<MarkdownBlock> Children { get; set; } = new();
    }
}
