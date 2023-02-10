using static Typedown.Core.Models.CursorState;

namespace Typedown.Core.Models
{
    public record CursorState(Pos Anchor, Pos Focus)
    {
        public record Pos(int Line, int Ch);
    }
}
