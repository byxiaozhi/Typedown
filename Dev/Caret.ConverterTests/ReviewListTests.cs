using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The Review tab of the inspector pane: the review marks of a note as a list.
    public class ReviewListTests
    {
        [Fact]
        public void A_note_without_marks_gives_an_empty_list()
        {
            Assert.Empty(ReviewList.Build(""));
            Assert.Empty(ReviewList.Build(null));
            Assert.Empty(ReviewList.Build("Just text, and a {brace} that is no mark."));
        }

        [Fact]
        public void Every_kind_of_mark_is_listed_in_the_order_of_the_text()
        {
            var items = ReviewList.Build("a {++new++} b {--old--} c {~~was~>is~~} d {==seen==} e {>>note<<}");
            Assert.Equal(new[] { "add", "del", "sub", "mark", "comment" }, items.Select(i => i.Kind).ToArray());
            Assert.Equal(new[] { "new", "old", "was → is", "seen", "note" }, items.Select(i => i.Snippet).ToArray());
        }

        [Fact]
        public void The_stamp_behind_a_change_belongs_to_the_change_and_is_not_a_row_of_its_own()
        {
            var items = ReviewList.Build("The report {~~was late~>is on time~~}{>>@Ann 2026-10-07<<} today.");
            var item = Assert.Single(items);
            Assert.Equal("sub", item.Kind);
            Assert.Equal("Ann, 2026-10-07", item.Meta);
        }

        [Fact]
        public void A_comment_with_a_stamp_and_a_note_shows_the_note_and_who_wrote_it()
        {
            var items = ReviewList.Build("See {==this part==}{>>@Jo 2026-10-08: shorten it<<}.");
            var item = Assert.Single(items);
            Assert.Equal("mark", item.Kind);
            Assert.Equal("Jo, 2026-10-08", item.Meta);
            Assert.Equal("this part — shorten it", item.Snippet);
        }

        [Fact]
        public void A_stamp_alone_says_nothing_and_a_stamped_note_on_its_own_is_a_comment()
        {
            Assert.Empty(ReviewList.Build("Text {>>@Ann 2026-10-07<<} more."));
            var item = Assert.Single(ReviewList.Build("Text {>>@Ann 2026-10-07: check the figure<<} more."));
            Assert.Equal("comment", item.Kind);
            Assert.Equal("check the figure", item.Snippet);
            Assert.Equal("Ann, 2026-10-07", item.Meta);
        }

        [Fact]
        public void Equal_marks_are_told_apart_by_their_number()
        {
            var items = ReviewList.Build("{++x++} one {++x++} two {++y++} three {++x++}");
            Assert.Equal(new[] { 0, 1, 0, 2 }, items.Select(i => i.Nth).ToArray());
            Assert.All(items.Where(i => i.Snippet == "x"), i => Assert.Equal("{++x++}", i.Raw));
        }

        [Fact]
        public void Marks_in_code_are_not_listed()
        {
            Assert.Empty(ReviewList.Build("Use `{++x++}` in a line.\n\n```\n{--y--}\n```\n"));
        }

        [Fact]
        public void A_long_snippet_is_one_line_and_cut()
        {
            var item = Assert.Single(ReviewList.Build("{++" + new string('w', 200) + "++}"));
            Assert.True(item.Snippet.Length <= 90);
            Assert.EndsWith("…", item.Snippet);
            var broken = Assert.Single(ReviewList.Build("{++two\nlines   here++}"));
            Assert.Equal("two lines here", broken.Snippet);
        }

        [Fact]
        public void An_equal_text_in_code_before_a_mark_is_counted_so_that_the_editor_finds_the_mark()
        {
            // the editors search the text: the example in code stands first, the mark is the second equal text
            var items = ReviewList.Build("Write `{++x++}` for an addition, then {++x++}.");
            var item = Assert.Single(items);
            Assert.Equal("{++x++}", item.Raw);
            Assert.Equal(1, item.Nth);
        }

        [Fact]
        public void Equal_marks_are_numbered_in_the_order_of_the_text()
        {
            var items = ReviewList.Build("{++x++} and {++x++} and {++y++} and {++x++}");
            Assert.Equal(new[] { 0, 1, 0, 2 }, items.Select(i => i.Nth).ToArray());
        }
    }
}
