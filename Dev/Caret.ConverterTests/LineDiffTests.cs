using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The lines that differ between two versions (the History tab of the inspector pane).
    public class LineDiffTests
    {
        [Fact]
        public void Equal_texts_have_no_changes()
        {
            Assert.Empty(LineDiff.Changes("a\nb\nc", "a\nb\nc", 50, out var more));
            Assert.Equal(0, more);
        }

        [Fact]
        public void An_added_a_removed_and_a_changed_line_are_found_in_the_order_of_the_text()
        {
            var changes = LineDiff.Changes("one\ntwo\nthree\nfour", "one\n2\nthree\nfour\nfive", 50, out _);
            Assert.Equal(new[] { (DiffKind.Removed, "two"), (DiffKind.Added, "2"), (DiffKind.Added, "five") },
                changes.Select(c => (c.Kind, c.Text)).ToArray());
        }

        [Fact]
        public void Lines_that_moved_apart_are_matched_not_shown_as_replaced_wholesale()
        {
            var older = "a\nb\nc\nd\ne\nf";
            var newer = "a\nb\nX\nd\ne\nf\ng";
            var changes = LineDiff.Changes(older, newer, 50, out _);
            Assert.Equal(new[] { "c", "X", "g" }, changes.Select(c => c.Text).ToArray());
        }

        [Fact]
        public void Line_endings_do_not_count_as_changes()
        {
            Assert.Empty(LineDiff.Changes("a\r\nb\r\n", "a\nb\n", 50, out _));
        }

        [Fact]
        public void Blank_lines_at_the_end_do_not_count_as_changes()
        {
            Assert.Empty(LineDiff.Changes("a\nb\n", "a\nb\n\n\n", 50, out _));
            Assert.Empty(LineDiff.Changes("a\nb", "a\nb  \n\n", 50, out _));
            var changes = LineDiff.Changes("a\nb\n", "a\nb\nc\n\n", 50, out _);
            Assert.Equal(new[] { "c" }, changes.Select(c => c.Text).ToArray());
        }

        [Fact]
        public void The_changes_are_limited_and_the_rest_is_counted()
        {
            var older = string.Join("\n", Enumerable.Range(0, 10).Select(i => "old " + i));
            var newer = string.Join("\n", Enumerable.Range(0, 10).Select(i => "new " + i));
            var changes = LineDiff.Changes(older, newer, 6, out var more);
            Assert.Equal(6, changes.Count);
            Assert.Equal(14, more);
        }

        [Fact]
        public void Compute_gives_every_line_so_that_the_old_text_can_be_read_back()
        {
            var older = "x\na\nb\ny";
            var newer = "x\nb\nc\ny";
            var all = LineDiff.Compute(older, newer);
            Assert.Equal(older, string.Join("\n", all.Where(l => l.Kind != DiffKind.Added).Select(l => l.Text)));
            Assert.Equal(newer, string.Join("\n", all.Where(l => l.Kind != DiffKind.Removed).Select(l => l.Text)));
        }

        [Fact]
        public void A_very_long_middle_is_all_removed_then_all_added()
        {
            var older = string.Join("\n", Enumerable.Range(0, 3000).Select(i => "old " + i));
            var newer = string.Join("\n", Enumerable.Range(0, 3000).Select(i => "new " + i));
            var all = LineDiff.Compute(older, newer);
            Assert.Equal(3000, all.Count(l => l.Kind == DiffKind.Removed));
            Assert.Equal(3000, all.Count(l => l.Kind == DiffKind.Added));
        }

        [Fact]
        public void The_first_changed_line_is_the_newer_one_cut_to_one_line()
        {
            Assert.Equal("Second line", LineDiff.FirstChangedLine("A\nB", "A\nSecond   line"));
            Assert.Equal("B", LineDiff.FirstChangedLine("A\nB", "A"));
            Assert.EndsWith("…", LineDiff.FirstChangedLine("A", new string('x', 200), 20));
            Assert.Equal("", LineDiff.FirstChangedLine("same", "same"));
            // an added blank line is skipped: the first line with words is shown
            Assert.Equal("Fresh line", LineDiff.FirstChangedLine("A\nB", "A\nB\n\nFresh line"));
        }
    }
}
