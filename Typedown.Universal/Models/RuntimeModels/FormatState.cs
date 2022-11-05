using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Models
{
    public class FormatState
    {
        public record SelectionFormat(string Type, string Tag);

        public FormatState(List<SelectionFormat> selectionFormats = null)
        {
            if (selectionFormats == null)
                return;
            var types = selectionFormats.Select(x => x.Type).ToHashSet();
            var tags = selectionFormats.Select(x => x.Tag).ToHashSet();
            Bold = types.Contains("strong");
            Italic = types.Contains("em");
            Underline = types.Contains("html_tag") && tags.Contains("u");
            InlineCode = types.Contains("inline_code");
            InlineMath = types.Contains("inline_math");
            Highlight = types.Contains("html_tag") && tags.Contains("mark");
            Strikethrough = types.Contains("del");
            Hyperlink = types.Contains("link");
            Image = types.Contains("image");
        }

        public bool Bold { get; }

        public bool Italic { get; }

        public bool Underline { get; }

        public bool Strikethrough { get; }

        public bool Highlight { get; }

        public bool InlineCode { get; }

        public bool InlineMath { get; }

        public bool Hyperlink { get; }

        public bool Image { get; }
    }
}
