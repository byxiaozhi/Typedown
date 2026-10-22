using System;
using System.IO;
using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // What live review keeps in Caret's own data folder (ReviewStore) and the first-seen day of each change (ReviewDates).
    public class ReviewStoreTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "caret-review-" + Guid.NewGuid().ToString("N"));

        private ReviewStore Store => new(folder);

        public void Dispose()
        {
            try { Directory.Delete(folder, true); } catch { }
        }

        private static ReviewStore.Saved Sample(string path) => new(
            path, "# T\n\nOl\u00e1 \u00e9 \u4e16\u754c.\n", "Ann", "2026-10-09", ReviewStore.Hash("x"),
            new[] { "first", "second" },
            new[] { new ReviewDates.Seen("old", "new", "before", "after", 2, "2026-10-08") });

        [Fact]
        public void What_is_saved_is_read_back_whole()
        {
            var path = Path.Combine(folder, "doc.md");
            Store.Save(Sample(path));
            var back = Store.Load(path);
            Assert.NotNull(back);
            Assert.Equal("# T\n\nOl\u00e1 \u00e9 \u4e16\u754c.\n", back.Baseline);
            Assert.Equal("Ann", back.Author);
            Assert.Equal("2026-10-09", back.Started);
            Assert.Equal(ReviewStore.Hash("x"), back.SavedHash);
            Assert.Equal(new[] { "first", "second" }, back.Previous);
            Assert.Equal(new ReviewDates.Seen("old", "new", "before", "after", 2, "2026-10-08"), Assert.Single(back.Seen));
        }

        [Fact]
        public void Nothing_kept_gives_null_and_a_damaged_copy_gives_null()
        {
            var path = Path.Combine(folder, "doc.md");
            Assert.Null(Store.Load(path));
            Store.Save(Sample(path));
            File.WriteAllText(Path.Combine(folder, ReviewStore.KeyOf(path) + ".json"), "{ not json");
            Assert.Null(Store.Load(path));
        }

        [Fact]
        public void A_baseline_without_its_data_is_not_a_tracked_document()
        {
            var path = Path.Combine(folder, "doc.md");
            Store.Save(Sample(path));
            File.Delete(Path.Combine(folder, ReviewStore.KeyOf(path) + ".json"));
            Assert.Null(Store.Load(path));
        }

        [Fact]
        public void Remove_takes_both_files_and_is_harmless_when_there_are_none()
        {
            var path = Path.Combine(folder, "doc.md");
            Store.Remove(path);
            Store.Save(Sample(path));
            Store.Remove(path);
            Assert.Empty(Directory.GetFiles(folder));
            Assert.Null(Store.Load(path));
        }

        [Fact]
        public void The_key_is_the_same_whatever_the_case_and_differs_between_documents()
        {
            Assert.Equal(ReviewStore.KeyOf(@"C:\Docs\A.md"), ReviewStore.KeyOf(@"c:\docs\a.MD"));
            Assert.NotEqual(ReviewStore.KeyOf(@"C:\Docs\A.md"), ReviewStore.KeyOf(@"C:\Docs\B.md"));
            Assert.Equal(32, ReviewStore.KeyOf(@"C:\Docs\A.md").Length);
        }

        [Fact]
        public void The_hash_ignores_line_endings_and_the_newlines_at_the_end()
        {
            Assert.Equal(ReviewStore.Hash("a\nb\n"), ReviewStore.Hash("a\r\nb\r\n\r\n"));
            Assert.NotEqual(ReviewStore.Hash("a\nb"), ReviewStore.Hash("a\nc"));
        }

        [Fact]
        public void Only_the_newest_earlier_baselines_stay_when_they_are_very_long()
        {
            var path = Path.Combine(folder, "doc.md");
            var big = new string('x', 1_000_000);
            Store.Save(Sample(path) with { Previous = new[] { big + "1", big + "2", big + "3", big + "4", big + "5" } });
            var back = Store.Load(path);
            Assert.True(back.Previous.Count < 5);
            Assert.EndsWith("5", back.Previous.Last());
        }

        [Fact]
        public void Cleanup_removes_what_was_not_opened_for_ninety_days_and_stray_files()
        {
            var old = Path.Combine(folder, "old.md");
            var recent = Path.Combine(folder, "recent.md");
            Store.Save(Sample(old));
            Store.Save(Sample(recent));
            File.SetLastWriteTimeUtc(Path.Combine(folder, ReviewStore.KeyOf(old) + ".json"), DateTime.UtcNow.AddDays(-91));
            File.WriteAllText(Path.Combine(folder, "stray.md"), "x");
            File.WriteAllText(Path.Combine(folder, "half.json.tmp"), "x");
            Assert.Equal(1, Store.Cleanup(DateTime.Now));
            Assert.Null(Store.Load(old));
            Assert.NotNull(Store.Load(recent));
            Assert.False(File.Exists(Path.Combine(folder, "stray.md")));
            Assert.False(File.Exists(Path.Combine(folder, "half.json.tmp")));
        }

        [Fact]
        public void Opening_starts_the_ninety_days_again()
        {
            var path = Path.Combine(folder, "doc.md");
            Store.Save(Sample(path));
            var data = Path.Combine(folder, ReviewStore.KeyOf(path) + ".json");
            File.SetLastWriteTimeUtc(data, DateTime.UtcNow.AddDays(-80));
            Assert.NotNull(Store.Load(path));
            Assert.True(File.GetLastWriteTimeUtc(data) > DateTime.UtcNow.AddDays(-1));
        }

        [Fact]
        public void RemoveUnder_removes_the_copies_of_every_document_under_a_folder_and_no_others()
        {
            var inside = Path.Combine(folder, "docs", "a.md");
            var deeper = Path.Combine(folder, "docs", "sub", "b.md");
            var other = Path.Combine(folder, "docs2", "c.md");
            Store.Save(Sample(inside));
            Store.Save(Sample(deeper));
            Store.Save(Sample(other));
            Assert.Equal(2, Store.RemoveUnder(Path.Combine(folder, "docs")));
            Assert.Null(Store.Load(inside));
            Assert.Null(Store.Load(deeper));
            Assert.NotNull(Store.Load(other));
        }
    }

    public class ReviewDatesTests
    {
        private static readonly DateTime Day1 = new(2026, 10, 1);
        private static readonly DateTime Day2 = new(2026, 10, 9);

        private static LiveReview.Result Compare(string baseline, string current, DateTime when, LiveReview.Result before = null) =>
            LiveReview.Compare(baseline, current, "Ann", when, "code changed", before?.Seen);

        [Fact]
        public void A_change_keeps_the_day_it_was_first_seen()
        {
            var baseline = "One.\n\nTwo.\n";
            var first = Compare(baseline, "One.\n\nTwo changed.\n", Day1);
            Assert.Equal(new[] { "2026-10-01" }, first.Days);
            var later = Compare(baseline, "One.\n\nTwo changed.\n", Day2, first);
            Assert.Equal(new[] { "2026-10-01" }, later.Days);
            Assert.Contains("@Ann 2026-10-01<<}", later.Marked);
            Assert.DoesNotContain("2026-10-09", later.Marked);
        }

        [Fact]
        public void A_new_change_gets_the_day_it_is_first_seen_and_the_old_one_keeps_its_own()
        {
            var baseline = "One.\n\nTwo.\n\nThree.\n";
            var first = Compare(baseline, "One a.\n\nTwo.\n\nThree.\n", Day1);
            var later = Compare(baseline, "One a.\n\nTwo.\n\nThree b.\n", Day2, first);
            Assert.Equal(new[] { "2026-10-01", "2026-10-09" }, later.Days);
            Assert.Contains("{>>@Ann 2026-10-01<<}", later.Marked);
            Assert.Contains("{>>@Ann 2026-10-09<<}", later.Marked);
        }

        [Fact]
        public void Two_identical_changes_in_different_places_keep_their_own_days()
        {
            var baseline = "red apple. red pear.\n";
            var first = Compare(baseline, "apple. red pear.\n", Day1);
            var later = Compare(baseline, "apple. pear.\n", Day2, first);
            Assert.Equal(2, later.List.Count);
            Assert.Equal(new[] { "2026-10-01", "2026-10-09" }, later.Days);
        }

        [Fact]
        public void A_change_is_still_known_when_the_text_on_one_side_was_edited()
        {
            var baseline = "Start here. The middle word stays. Then the end follows.\n";
            var first = Compare(baseline, "Start here. The middle changed stays. Then the end follows.\n", Day1);
            var later = Compare(baseline, "Start here, now. The middle changed stays. Then the end follows.\n", Day2, first);
            Assert.Equal("2026-10-01", later.Days[later.List.ToList().FindIndex(c => c.Text.Contains("changed"))]);
        }

        [Fact]
        public void When_one_of_two_identical_saved_changes_is_gone_the_survivor_takes_neither_day()
        {
            var saved = new[]
            {
                new ReviewDates.Seen("x", "", "aaa", "bbb", 1, "2026-10-01"),
                new ReviewDates.Seen("x", "", "ccc", "ddd", 2, "2026-10-02"),
            };
            var found = new[] { new ReviewDates.Probe("x", "", "zzz", "yyy", 2) };
            Assert.Equal(new[] { "2026-10-09" }, ReviewDates.Assign(saved, found, "2026-10-09"));
        }

        [Fact]
        public void With_the_same_number_of_saved_and_found_the_occurrence_number_pairs_them()
        {
            var saved = new[]
            {
                new ReviewDates.Seen("x", "", "aaa", "bbb", 1, "2026-10-01"),
                new ReviewDates.Seen("x", "", "ccc", "ddd", 2, "2026-10-02"),
            };
            var found = new[] { new ReviewDates.Probe("x", "", "zzz", "yyy", 2), new ReviewDates.Probe("x", "", "qqq", "ppp", 1) };
            Assert.Equal(new[] { "2026-10-02", "2026-10-01" }, ReviewDates.Assign(saved, found, "2026-10-09"));
        }

        [Fact]
        public void A_saved_fingerprint_is_used_once_and_changes_that_are_gone_are_dropped()
        {
            var baseline = "One.\n\nTwo.\n";
            var first = Compare(baseline, "One a.\n\nTwo b.\n", Day1);
            Assert.Equal(2, first.Seen.Count);
            var later = Compare(baseline, "One a.\n\nTwo.\n", Day2, first);
            Assert.Single(later.Seen);
            Assert.Equal("2026-10-01", Assert.Single(later.Days));
        }

        [Fact]
        public void Without_anything_saved_every_change_is_of_the_day_of_the_comparison()
        {
            var result = Compare("One.\n\nTwo.\n", "One a.\n\nTwo b.\n", Day2);
            Assert.All(result.Days, d => Assert.Equal("2026-10-09", d));
        }

        [Fact]
        public void The_code_note_is_of_the_day_of_the_comparison()
        {
            var first = Compare("Text.\n\n```\nold\n```\n", "Text a.\n\n```\nnew\n```\n", Day1);
            var later = Compare("Text.\n\n```\nold\n```\n", "Text a.\n\n```\nnew\n```\n", Day2, first);
            Assert.Contains("{>>@Ann 2026-10-01<<}", later.Marked);
            Assert.Contains("{>>@Ann 2026-10-09: code changed<<}", later.Marked);
        }

        [Fact]
        public void Text_that_looks_like_the_stamp_of_the_comparison_is_left_as_it_is_and_takes_no_date()
        {
            var literal = "{>>@caret-live-review 0001-01-01<<}";
            var code = "```\n" + literal + "\n```\n\n";
            var before = code + "One.\n\nTwo.\n";
            var first = LiveReview.Compare(before, code + "One a.\n\nTwo.\n", "Ann", Day1, "code changed");
            var later = LiveReview.Compare(before, code + "One a.\n\nTwo b.\n", "Ann", Day2, "code changed", first.Seen);
            Assert.Equal(new[] { "2026-10-01", "2026-10-09" }, later.Days);
            // the text of the document is untouched, and each change has its own day
            Assert.Contains(literal, later.Marked);
            var firstStamp = later.Marked.IndexOf("{>>@Ann 2026-10-01<<}", StringComparison.Ordinal);
            var secondStamp = later.Marked.IndexOf("{>>@Ann 2026-10-09<<}", StringComparison.Ordinal);
            Assert.True(firstStamp >= 0 && secondStamp > firstStamp);
        }

        [Fact]
        public void A_note_line_in_a_block_of_code_that_looks_like_the_comparisons_is_left_as_it_is()
        {
            var literal = "{>>@caret-live-review 0001-01-01: code changed<<}";
            var code = "```\n" + literal + "\n```\n\n";
            var result = LiveReview.Compare(code + "One.\n", code + "One a.\n", "Ann", Day2, "code changed");
            // no block of code changed, so the comparison wrote no note: the line is the document's
            Assert.Contains(literal, result.Marked);
            Assert.DoesNotContain("{>>@Ann 2026-10-09: code changed<<}", result.Marked);
        }
    }
}