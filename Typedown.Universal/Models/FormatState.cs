using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class FormatState
    {

        public FormatState()
        {
            Bold = false;
            Italic = false;
            Underline = false;
            InlineCode = false;
            InlineMath = false;
            Highlight = false;
            Strikethrough = false;
            Hyperlink = false;
            Image = false;
        }
        public FormatState(HashSet<string> typeSet, HashSet<string> tagSet)
        {
            Bold = typeSet.Contains("strong");
            Italic = typeSet.Contains("em");
            Underline = typeSet.Contains("html_tag") && tagSet.Contains("u");
            InlineCode = typeSet.Contains("inline_code");
            InlineMath = typeSet.Contains("inline_math");
            Highlight = typeSet.Contains("html_tag") && tagSet.Contains("mark");
            Strikethrough = typeSet.Contains("del");
            Hyperlink = typeSet.Contains("link");
            Image = typeSet.Contains("image");
        }
        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public bool Strikethrough { get; set; }

        public bool Highlight { get; set; }

        public bool InlineCode { get; set; }

        public bool InlineMath { get; set; }

        public bool Hyperlink { get; set; }

        public bool Image { get; set; } = false;
    }
}
