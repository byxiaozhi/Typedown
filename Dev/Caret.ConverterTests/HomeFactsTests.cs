using System;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The facts the top of Home is made of: the line a note is about, the tip of the day, the comments that wait.
    public class HomeFactsTests
    {
        [Fact]
        public void The_excerpt_is_the_first_line_that_says_something_without_the_front_matter_or_the_heading()
        {
            var text = "---\ntitle: Plan\ntags: [a]\n---\n\n# Quarterly plan\n\nWe **start** with the [budget](https://x.example) and the _team_.\n\nSecond paragraph.";
            Assert.Equal("We start with the budget and the team.", HomeFacts.Excerpt(text));
        }

        [Fact]
        public void A_note_with_only_a_heading_is_about_that_heading()
        {
            Assert.Equal("Meeting notes", HomeFacts.Excerpt("# Meeting notes\n"));
            Assert.Equal("", HomeFacts.Excerpt("   \n\n"));
            Assert.Equal("", HomeFacts.Excerpt(null));
        }

        [Fact]
        public void Code_tables_rules_comments_and_marks_of_lists_and_quotes_are_passed_over_or_taken_off()
        {
            var text = "```\nint x = 1;\n```\n| a | b |\n|---|---|\n---\n<!-- hidden -->\n> - [ ] Call Ana {>>@Ben 2026-10-07 today?<<}\n";
            Assert.Equal("Call Ana", HomeFacts.Excerpt(text));
            Assert.Equal("The launch is in March.", HomeFacts.Excerpt("The launch is in March {>>@Ana 2026-10-19 sure?<<}.\n"));
        }

        [Fact]
        public void A_long_line_is_cut_at_a_word_and_ends_with_an_ellipsis()
        {
            var text = string.Join(" ", System.Linq.Enumerable.Repeat("alpha beta gamma", 20));
            var excerpt = HomeFacts.Excerpt(text, 40);
            Assert.EndsWith("…", excerpt);
            Assert.True(excerpt.Length <= 41, excerpt);
            Assert.DoesNotContain("  ", excerpt);
            Assert.StartsWith("alpha beta gamma", excerpt);
        }

        [Fact]
        public void The_tip_is_the_same_all_day_another_tomorrow_and_always_in_the_list()
        {
            var day = new DateTime(2026, 10, 21, 8, 0, 0);
            Assert.Equal(HomeFacts.TipIndex(day, 40), HomeFacts.TipIndex(day.AddHours(14), 40));
            Assert.NotEqual(HomeFacts.TipIndex(day, 40), HomeFacts.TipIndex(day.AddDays(1), 40));
            for (var d = -400; d < 800; d += 7)
            {
                var index = HomeFacts.TipIndex(day.AddDays(d), 37);
                Assert.InRange(index, 0, 36);
            }
            Assert.Equal(-1, HomeFacts.TipIndex(day, 0));
        }

        [Fact]
        public void The_tips_come_round_in_the_order_of_the_list()
        {
            var day = new DateTime(2026, 3, 3);
            var first = HomeFacts.TipIndex(day, 5);
            Assert.Equal((first + 1) % 5, HomeFacts.TipIndex(day.AddDays(1), 5));
            Assert.Equal(first, HomeFacts.TipIndex(day.AddDays(5), 5));
        }

        [Fact]
        public void Only_comments_are_counted_not_changes_or_the_stamps_that_belong_to_them()
        {
            Assert.Equal(0, HomeFacts.CountComments(""));
            Assert.Equal(0, HomeFacts.CountComments("plain text {not a mark}"));
            var text = "Check {>>@Ann 2026-10-07 is this right?<<} this.\n\n{++new words++}{>>@Ben 2026-10-08<<} and {--old--}.\n\nAnd {>>a note without a stamp<<}.";
            Assert.Equal(2, HomeFacts.CountComments(text));
        }
    }
}
