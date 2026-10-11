using System;

namespace Typedown.WinUI.Services
{
    // New since the fork: how wide the tab strip may be. The strip sits in the title bar and Windows lets a tab be
    // pressed and dragged, so the part of the bar it covers cannot move the window. With enough tabs the strip
    // filled the whole bar and the window could not be grabbed at all: a part of the bar is always left free.
    // Plain .NET (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal static class TitleBarLayout
    {
        // The free part, in device-independent pixels: wide enough to put the pointer on and drag.
        internal const double FreeDragWidth = 96;

        // The least a strip is given when the window is so narrow that even this much is not left: a scroll
        // button, a sliver of a tab and the add button, so the tabs never disappear altogether. Above it the
        // free part is kept and the strip gives way.
        internal const double LeastTabStripWidth = 48;

        // barWidth: the whole title bar. rightWidth: what the buttons at the right take (their margin included).
        // stripMargins: the strip's own margins at left and right. Returns the width the strip may not pass, or
        // null when the bar has not been laid out yet (nothing is limited then).
        internal static double? TabStripMaxWidth(double barWidth, double rightWidth, double stripMargins)
        {
            if (double.IsNaN(barWidth) || double.IsNaN(rightWidth) || barWidth <= 0) return null;
            var room = barWidth - rightWidth - stripMargins - FreeDragWidth;
            return Math.Max(LeastTabStripWidth, room);
        }
    }
}
