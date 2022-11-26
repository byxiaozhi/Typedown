using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Typedown.Universal.Models.CursorState;

namespace Typedown.Universal.Models
{
    public record CursorState(Pos Anchor, Pos Focus)
    {
        public record Pos(int Line, int Ch);
    }
}
