using System;
using System.IO;
using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The local history of a note (the History tab of the inspector pane): when a snapshot is kept, how much is kept, and what the
    // list says about each version.
    public sealed class NoteHistoryTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "caret-history-tests-" + Guid.NewGuid().ToString("N"));
        private DateTime now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        private const string Note = @"C:\Notes\talk.md";

        private NoteHistory Make(int maxVersions = 50, int maxDays = 30, TimeSpan? minGap = null) =>
            new(root, () => now, maxVersions, maxDays, minGap ?? TimeSpan.FromMinutes(2));

        public void Dispose()
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }

        [Fact]
        public void The_first_text_is_kept_and_the_same_text_is_not_kept_again()
        {
            var history = Make();
            Assert.True(history.Record(Note, "one"));
            now = now.AddMinutes(10);
            Assert.False(history.Record(Note, "one"));
            Assert.Single(history.List(Note));
        }

        [Fact]
        public void An_automatic_save_within_the_gap_is_not_kept_but_a_checkpoint_is()
        {
            var history = Make();
            history.Record(Note, "one");
            now = now.AddSeconds(30);
            Assert.False(history.Record(Note, "two"));
            Assert.True(history.Record(Note, "two", checkpoint: true));
            now = now.AddMinutes(3);
            Assert.True(history.Record(Note, "three"));
            Assert.Equal(3, history.List(Note).Count);
        }

        [Fact]
        public void A_blank_text_and_a_text_that_is_too_long_are_not_kept()
        {
            var history = Make();
            Assert.False(history.Record(Note, ""));
            Assert.False(history.Record(Note, "  \n "));
            Assert.False(history.Record(Note, new string('x', NoteHistory.MaxLength + 1)));
            Assert.False(history.Record("", "text"));
            Assert.Empty(history.List(Note));
        }

        [Fact]
        public void The_list_is_newest_first_with_the_change_in_length_and_the_first_changed_line()
        {
            var history = Make();
            history.Record(Note, "# Title\n\nFirst paragraph.\n");
            now = now.AddMinutes(5);
            history.Record(Note, "# Title\n\nFirst paragraph, now longer.\n\nSecond.\n");
            now = now.AddMinutes(5);
            history.Record(Note, "# Title\n\nFirst paragraph, now longer.\n");

            var list = history.List(Note);
            Assert.Equal(3, list.Count);
            Assert.True(list[0].Time > list[1].Time && list[1].Time > list[2].Time);
            Assert.True(list[2].IsFirst);
            Assert.False(list[0].IsFirst);
            Assert.True(list[1].Delta > 0);
            Assert.True(list[0].Delta < 0);
            Assert.Equal("First paragraph, now longer.", list[1].FirstChange);
            Assert.Equal("", list[2].FirstChange);
        }

        [Fact]
        public void A_version_can_be_read_back_and_an_unknown_one_is_null()
        {
            var history = Make();
            history.Record(Note, "kept text");
            var id = history.List(Note).Single().Id;
            Assert.Equal("kept text", history.Read(Note, id));
            Assert.Null(history.Read(Note, "20200101T000000000Z"));
        }

        [Fact]
        public void Only_the_newest_versions_are_kept()
        {
            var history = Make(maxVersions: 3);
            for (var i = 1; i <= 6; i++)
            {
                history.Record(Note, "text " + i, checkpoint: true);
                now = now.AddMinutes(1);
            }
            var list = history.List(Note);
            Assert.Equal(3, list.Count);
            Assert.Equal("text 6", history.Read(Note, list[0].Id));
            Assert.Equal("text 4", history.Read(Note, list[2].Id));
        }

        [Fact]
        public void Versions_older_than_the_limit_go_but_the_newest_is_always_kept()
        {
            var history = Make(maxDays: 30);
            history.Record(Note, "old one", checkpoint: true);
            now = now.AddDays(10);
            history.Record(Note, "old two", checkpoint: true);
            now = now.AddDays(25);                    // the first is 35 days old, the second 25
            history.Record(Note, "fresh", checkpoint: true);
            Assert.Equal(new[] { "fresh", "old two" }, history.List(Note).Select(e => history.Read(Note, e.Id)).ToArray());

            now = now.AddDays(100);                   // everything else is old: the newest stays
            history.Record(Note, "newest", checkpoint: true);
            Assert.Equal("newest", history.Read(Note, history.List(Note).Single().Id));
        }

        [Fact]
        public void PruneAll_removes_the_expired_versions_of_every_note_but_keeps_the_newest_of_each()
        {
            var history = Make(maxDays: 30);
            history.Record(@"C:\One\a.md", "a old", checkpoint: true);
            history.Record(@"C:\One\a.md", "a older still", checkpoint: true);
            history.Record(@"C:\Two\b.md", "b old", checkpoint: true);
            now = now.AddDays(40);
            history.Record(@"C:\Three\c.md", "c fresh", checkpoint: true);   // another note: nothing prunes the first two

            Assert.Equal(2, history.List(@"C:\One\a.md").Count);
            history.PruneAll();
            Assert.Single(history.List(@"C:\One\a.md"));    // the newest one stays although it is 40 days old
            Assert.Single(history.List(@"C:\Two\b.md"));
            Assert.Single(history.List(@"C:\Three\c.md"));
        }

        [Fact]
        public void Two_snapshots_in_the_same_millisecond_are_both_kept()
        {
            var history = Make();
            history.Record(Note, "a", checkpoint: true);
            history.Record(Note, "b", checkpoint: true);
            Assert.Equal(2, history.List(Note).Count);
        }

        [Fact]
        public void Notes_have_histories_of_their_own_and_a_clear_removes_one()
        {
            var history = Make();
            history.Record(@"C:\One\notes.md", "in one");
            history.Record(@"C:\Two\notes.md", "in two");
            Assert.Single(history.List(@"C:\One\notes.md"));
            Assert.Single(history.List(@"C:\Two\notes.md"));

            history.Clear(@"C:\One\notes.md");
            Assert.Empty(history.List(@"C:\One\notes.md"));
            Assert.False(history.Any(@"C:\One\notes.md"));
            Assert.True(history.Any(@"C:\Two\notes.md"));
        }

        [Fact]
        public void A_renamed_note_takes_its_history_along()
        {
            var history = Make();
            history.Record(@"C:\Notes\old.md", "text");
            history.Move(@"C:\Notes\old.md", @"C:\Notes\new.md");
            Assert.Empty(history.List(@"C:\Notes\old.md"));
            Assert.Single(history.List(@"C:\Notes\new.md"));
        }

        [Fact]
        public void The_path_is_not_case_sensitive()
        {
            var history = Make();
            history.Record(@"C:\Notes\Talk.md", "text");
            Assert.Single(history.List(@"c:\notes\TALK.md"));
        }
    }
}
