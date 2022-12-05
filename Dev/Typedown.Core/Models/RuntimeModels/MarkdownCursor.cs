using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Core.Models
{
    public record MarkdownCursor
    {
        public record Pos(string Key, int Offset);

        public Pos Anchor;

        public Pos Focus;
    }
}
