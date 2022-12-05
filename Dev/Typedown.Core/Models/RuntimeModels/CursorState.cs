using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Typedown.Core.Models.CursorState;

namespace Typedown.Core.Models
{
    public record CursorState(Pos Anchor, Pos Focus)
    {
        public record Pos(int Line, int Ch);
    }
}
