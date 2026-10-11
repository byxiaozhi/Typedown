using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The tab strip never takes the whole title bar (Services/TitleBarLayout.cs): a part is left to grab the window by.
    public class TitleBarLayoutTests
    {
        [Fact]
        public void A_wide_bar_leaves_the_free_part_after_the_tabs()
        {
            // 1200 wide, 300 of buttons at the right, 34 + 8 of margins: the strip may use what is left less the free part.
            var max = TitleBarLayout.TabStripMaxWidth(1200, 300, 42);
            Assert.Equal(1200 - 300 - 42 - TitleBarLayout.FreeDragWidth, max);
        }

        [Fact]
        public void The_free_part_is_wide_enough_to_press_on()
        {
            Assert.True(TitleBarLayout.FreeDragWidth >= 64);
        }

        [Fact]
        public void A_narrow_window_still_gives_the_strip_its_least_width()
        {
            Assert.Equal(TitleBarLayout.LeastTabStripWidth, TitleBarLayout.TabStripMaxWidth(500, 300, 42));
        }

        [Fact]
        public void A_bar_not_laid_out_yet_limits_nothing()
        {
            Assert.Null(TitleBarLayout.TabStripMaxWidth(0, 0, 42));
            Assert.Null(TitleBarLayout.TabStripMaxWidth(double.NaN, 300, 42));
        }
    }
}
