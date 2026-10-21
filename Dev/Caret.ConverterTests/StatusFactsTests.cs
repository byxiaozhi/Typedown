using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The facts the live status bar shows about a note.
    public class StatusFactsTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(-5, 0)]
        [InlineData(1, 0)]
        [InlineData(99, 0)]
        [InlineData(100, 1)]
        [InlineData(299, 1)]
        [InlineData(300, 2)]
        [InlineData(1000, 5)]
        [InlineData(9000, 45)]
        public void The_reading_time_is_rounded_to_whole_minutes_and_zero_means_under_a_minute(int words, int minutes)
        {
            Assert.Equal(minutes, StatusFacts.ReadingMinutes(words));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("one line", null)]
        [InlineData("a\nb\n", null)]
        [InlineData("a\r\nb\r\n", "CRLF")]
        [InlineData("a\r\nb\nc", "CRLF")]
        [InlineData("a\rb\r", "CR")]
        public void The_line_ends_of_a_text_are_named_unless_they_are_the_LF_Caret_writes(string text, string expected)
        {
            Assert.Equal(expected, StatusFacts.LineEndingOf(text));
        }
    }
}
