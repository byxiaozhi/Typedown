using System;
using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // Live review (Services/LiveReview.cs): the baseline compared with the document, as a review text and a list of changes.
    public class LiveReviewTests
    {
        private static readonly DateTime Day = new(2026, 10, 9);

        private static LiveReview.Result Compare(string baseline, string current) => LiveReview.Compare(baseline, current, "Ann", Day, "code changed");

        private static string Unix(string text) => text.Replace("\r\n", "\n");

        [Fact]
        public void The_same_text_has_no_changes()
        {
            var result = Compare("# Title\n\nA line.\n", "# Title\n\nA line.\n");
            Assert.Equal(0, result.Changes);
            Assert.Empty(result.List);
        }

        [Fact]
        public void An_added_line_a_deleted_line_and_a_changed_word_are_listed_in_the_order_of_the_text()
        {
            var baseline = "# Report\n\nSales were low.\n\nThe old paragraph goes.\n";
            var current = "# Report\n\nSales were high.\n\nA brand new paragraph.\n";
            var result = Compare(baseline, current);
            Assert.True(result.Changes >= 2, result.Marked);
            Assert.Equal(result.Changes, result.List.Count);
            Assert.Contains(result.List, c => c.Kind == LiveReview.ChangeKind.Replaced && c.Text.Contains("low") && c.Text.Contains("high"));
            // accepting every mark gives the document, rejecting every mark gives the baseline
            Assert.Equal(current, Unix(ReviewMarks.Resolve(result.Marked, ReviewAction.Accept)));
            Assert.Equal(baseline, Unix(ReviewMarks.Resolve(result.Marked, ReviewAction.Reject)));
        }

        [Fact]
        public void A_mark_that_was_already_in_the_text_is_not_a_change_of_this_review()
        {
            var text = "A {++mark++}{>>@Bob 2026-01-01<<} that is there already.\n";
            var result = Compare(text, text + "\nNew line.\n");
            var change = Assert.Single(result.List);
            Assert.Equal(LiveReview.ChangeKind.Added, change.Kind);
        }

        [Fact]
        public void A_long_change_is_cut_for_the_list()
        {
            var result = Compare("Start.\n", "Start.\n\n" + new string('x', 300) + "\n");
            var change = Assert.Single(result.List);
            Assert.True(change.Text.Length <= 80, change.Text.Length.ToString());
            Assert.EndsWith("\u2026", change.Text);
        }

        // The three changes: a replaced word, a deleted paragraph and an added line.
        private const string Before = "# Report\n\nSales were low.\n\nThe old paragraph goes.\n\nThe end.\n";
        private const string After = "# Report\n\nSales were high.\n\nThe end.\n\nA new last line.\n";

        [Fact]
        public void Every_change_has_its_place_in_the_document()
        {
            var result = Compare(Before, After);
            Assert.True(result.Exact);
            var added = result.List.Single(c => c.Kind == LiveReview.ChangeKind.Added);
            Assert.Equal("A new last line.", After.Substring(added.Offset, added.Length).TrimEnd('\n'));
            var replaced = result.List.Single(c => c.Kind == LiveReview.ChangeKind.Replaced);
            Assert.Equal("high", After.Substring(replaced.Offset, replaced.Length));
        }

        [Fact]
        public void Accepting_one_change_leaves_the_others_as_changes()
        {
            var result = Compare(Before, After);
            Assert.True(result.List.Count >= 3, result.Marked);
            for (var k = 0; k < result.List.Count; k++)
            {
                var baseline = LiveReview.AcceptOne(result, k);
                var again = Compare(baseline, After);
                Assert.Equal(result.List.Count - 1, again.List.Count);
                Assert.True(again.Exact);
                // the baseline got that change and nothing else: the other changes are still the same ones
                Assert.Equal(result.List.Where(c => c.Index != k).Select(c => c.Text), again.List.Select(c => c.Text));
            }
        }

        [Fact]
        public void Rejecting_one_change_takes_it_back_in_the_document_and_leaves_the_others()
        {
            var result = Compare(Before, After);
            for (var k = 0; k < result.List.Count; k++)
            {
                var document = LiveReview.RejectOne(result, k);
                var again = Compare(Before, document);
                Assert.Equal(result.List.Count - 1, again.List.Count);
                Assert.True(again.Exact);
                Assert.Equal(result.List.Where(c => c.Index != k).Select(c => c.Text), again.List.Select(c => c.Text));
            }
        }

        [Fact]
        public void Accepting_every_change_one_by_one_ends_with_no_changes()
        {
            var result = Compare(Before, After);
            var baseline = Before;
            for (var n = result.List.Count; n > 0; n--)
            {
                var step = Compare(baseline, After);
                baseline = LiveReview.AcceptOne(step, 0);
            }
            Assert.Equal(After, baseline);
            Assert.Empty(Compare(baseline, After).List);
        }

        [Fact]
        public void Changed_code_cannot_be_marked_so_one_change_is_not_exact()
        {
            var result = Compare("Text.\n\n```\nold code\n```\n", "Text, changed.\n\n```\nnew code\n```\n");
            Assert.False(result.Exact);
        }

        [Fact]
        public void Windows_line_ends_are_understood()
        {
            var result = Compare(Before.Replace("\n", "\r\n"), After.Replace("\n", "\r\n"));
            Assert.True(result.Exact);
            Assert.Equal(3, result.List.Count);
        }
        [Fact]
        public void A_mark_that_was_there_before_is_not_swallowed_into_the_next_change()
        {
            var before = "An old {--mark--}{>>@Bob 2026-01-01<<} here.\n\nTail.\n";
            var after = "An old {--mark--}{>>@Bob 2026-01-01<<} here.\n\nTail. More.\n";
            var result = Compare(before, after);
            var change = Assert.Single(result.List);
            Assert.True(result.Exact);
            Assert.Equal(LiveReview.ChangeKind.Added, change.Kind);
            // accepting it gives the document, and the older mark is still in it
            var baseline = LiveReview.AcceptOne(result, 0);
            Assert.Equal(after, baseline);
            Assert.Contains("{--mark--}", baseline);
        }
        [Fact]
        public void Clearing_a_paragraph_and_leaving_blank_lines_is_still_exact()
        {
            var result = Compare("# T\n\nKeep.\n\nGo away.\n\nEnd.\n", "# T\n\nKeep.\n\n\n\nEnd.\n");
            Assert.True(result.Exact);
            Assert.Equal(LiveReview.ChangeKind.Deleted, Assert.Single(result.List).Kind);
        }
        [Fact]
        public void A_mark_with_the_same_author_and_day_is_not_a_change_of_this_review()
        {
            var before = "Old {++mark++}{>>@Ann 2026-10-09<<} here.\n\nTail.\n";
            var after = "Old {++mark++}{>>@Ann 2026-10-09<<} here.\n\nTail. More.\n";
            var result = Compare(before, after);
            var change = Assert.Single(result.List);
            Assert.Equal(LiveReview.ChangeKind.Added, change.Kind);
            Assert.True(result.Exact);
            Assert.Equal(after, LiveReview.AcceptOne(result, 0));
            Assert.Equal(before, LiveReview.RejectOne(result, 0));
            // what is shown has the stamp of the author, and the internal stamp is not in it
            Assert.Contains("{++ More.++}{>>@Ann 2026-10-09<<}", result.Marked);
            Assert.DoesNotContain("caret-live-review", result.Marked);
        }

        [Fact]
        public void A_comment_added_while_tracking_does_not_stop_the_changes_from_being_accepted_or_rejected()
        {
            var before = "### [Job title], [Company]\n\nWe are here today to the new year together\n";
            var after = "### {==[Job title]==}{>>@Ann 2026-10-09: why this<<}, [Company]\n\nWe are here  to the new year\n";
            var result = Compare(before, after);
            Assert.Equal(2, result.List.Count);
            Assert.True(result.Exact);
            // rejecting one keeps the comment and gives the old words back; accepting one keeps the rest as it was
            var rejected = LiveReview.RejectOne(result, 0);
            Assert.Contains("{==[Job title]==}", rejected);
            Assert.Contains("today", rejected);
            Assert.DoesNotContain("together", rejected);
        }


        [Fact]
        public void The_review_that_is_written_into_the_document_goes_back_to_both_versions_with_the_review_commands()
        {
            // L4 writes Marked into the text; from then on Accept all and Reject all (ReviewMarks.Resolve) must give the document and the
            // starting version again
            var result = Compare(Before, After);
            var accepted = ReviewMarks.Resolve(result.Marked, ReviewAction.Accept);
            var rejected = ReviewMarks.Resolve(result.Marked, ReviewAction.Reject);
            Assert.Equal(Blank(After), Blank(accepted));
            Assert.Equal(Blank(Before), Blank(rejected));
            // the stamps of the author are in what is written
            Assert.Contains("{>>@Ann 2026-10-09<<}", result.Marked);
        }

        // blank lines left where text went do not count
        private static string Blank(string text) => System.Text.RegularExpressions.Regex.Replace(text.Replace("\r\n", "\n"), "\n{3,}", "\n\n");

        public void A_card_finds_its_change_by_its_text_and_context_among_identical_changes()
        {
            var both = Compare("A red cat. A red dog.\n", "A cat. A dog.\n");
            Assert.Equal(2, both.Seen.Count);
            Assert.Equal(0, LiveReview.Reidentify(both.Seen, both.Seen[0].Old, both.Seen[0].New, both.Seen[0].Before, both.Seen[0].After, unique: false));
            Assert.Equal(1, LiveReview.Reidentify(both.Seen, both.Seen[1].Old, both.Seen[1].New, both.Seen[1].Before, both.Seen[1].After, unique: false));
        }

        [Fact]
        public void A_stale_card_does_not_act_on_the_identical_change_that_took_its_place()
        {
            var both = Compare("A red cat. A red dog.\n", "A cat. A dog.\n");
            var first = both.Seen[0];
            var second = both.Seen[1];
            // the first of the two went; what is left is the second, now number 0
            var left = Compare("A red cat. A red dog.\n", "A cat. A red dog.\n");
            Assert.Single(left.Seen);
            Assert.Equal(-1, LiveReview.Reidentify(left.Seen, first.Old, first.New, first.Before, first.After, unique: false));
            Assert.Equal(0, LiveReview.Reidentify(left.Seen, second.Old, second.New, left.Seen[0].Before, left.Seen[0].After, unique: false));
        }

        [Fact]
        public void A_change_that_was_alone_with_its_words_is_found_by_them_even_when_the_text_around_it_changed()
        {
            var result = Compare("One.\n\nTwo.\n", "One a.\n\nTwo.\n");
            var seen = result.Seen[0];
            Assert.Equal(0, LiveReview.Reidentify(result.Seen, seen.Old, seen.New, "something else", "other", unique: true));
            Assert.Equal(-1, LiveReview.Reidentify(result.Seen, seen.Old, seen.New, "something else", "other", unique: false));
            Assert.Equal(-1, LiveReview.Reidentify(result.Seen, "no such", "change", seen.Before, seen.After, unique: true));
        }
    }
}