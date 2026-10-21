using System.IO;
using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The folders of the Home page: the ones that are pinned and the ones that were opened last.
    public class FolderHistoryTests
    {
        private static (FolderHistory History, string File, string Work) Make()
        {
            var work = TestPaths.NewTempFolder();
            var file = Path.Combine(work, "Folders.json");
            return (new FolderHistory(file), file, work);
        }

        private static bool Always(string _) => true;

        [Fact]
        public void An_opened_folder_comes_first_and_is_there_once_whatever_the_case_and_the_trailing_slash()
        {
            var (history, _, work) = Make();
            try
            {
                history.Record(@"C:\Notes\Alpha");
                history.Record(@"C:\Notes\Beta");
                history.Record(@"c:\notes\ALPHA\");
                Assert.Equal(new[] { @"c:\notes\ALPHA", @"C:\Notes\Beta" }.Length, history.Recent.Count);
                Assert.Equal(@"C:\notes\ALPHA", history.Recent[0], ignoreCase: true);
                Assert.Equal(@"C:\Notes\Beta", history.Recent[1]);
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void Only_the_last_twelve_are_kept()
        {
            var (history, _, work) = Make();
            try
            {
                for (var i = 1; i <= 20; i++) history.Record(@"C:\Notes\F" + i);
                Assert.Equal(FolderHistory.MaxRecent, history.Recent.Count);
                Assert.Equal(@"C:\Notes\F20", history.Recent[0]);
                Assert.Equal(@"C:\Notes\F9", history.Recent[^1]);
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void The_pinned_folders_come_first_then_the_recent_ones_that_are_not_pinned()
        {
            var (history, _, work) = Make();
            try
            {
                history.Record(@"C:\Notes\A");
                history.Record(@"C:\Notes\B");
                history.Record(@"C:\Notes\C");
                Assert.True(history.TogglePinned(@"C:\Notes\A"));
                Assert.True(history.TogglePinned(@"C:\Work\Pinned"));
                var shelf = history.Shelf(Always);
                Assert.Equal(new[] { (@"C:\Work\Pinned", true), (@"C:\Notes\A", true), (@"C:\Notes\C", false), (@"C:\Notes\B", false) }, shelf.ToArray());
                Assert.False(history.TogglePinned(@"C:\Work\Pinned")); // unpinned: gone, it was never opened
                Assert.DoesNotContain(history.Shelf(Always), s => s.Path == @"C:\Work\Pinned");
                Assert.False(history.TogglePinned(@"C:\Notes\A"));
                Assert.Equal(new[] { @"C:\Notes\C", @"C:\Notes\B", @"C:\Notes\A" }, history.Shelf(Always).Select(s => s.Path).ToArray()); // A is a recent one again
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void A_folder_that_is_gone_is_not_listed_and_the_list_is_cut_to_the_size_asked()
        {
            var (history, _, work) = Make();
            try
            {
                foreach (var name in new[] { "A", "B", "C", "D" }) history.Record(@"C:\Notes\" + name);
                history.TogglePinned(@"C:\Notes\A");
                var shelf = history.Shelf(p => !p.EndsWith(@"\B"), max: 3);
                Assert.Equal(new[] { @"C:\Notes\A", @"C:\Notes\D", @"C:\Notes\C" }, shelf.Select(s => s.Path).ToArray());
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void The_lists_are_kept_in_the_file_and_a_second_instance_sees_them()
        {
            var (history, file, work) = Make();
            try
            {
                history.Record(@"C:\Notes\A");
                history.TogglePinned(@"C:\Notes\A");
                var other = new FolderHistory(file);
                Assert.True(other.IsPinned(@"c:\notes\a"));
                other.Record(@"C:\Notes\B");
                Assert.Equal(@"C:\Notes\B", history.Recent[0]); // read again: a window does not save over another's change
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void A_missing_or_damaged_file_is_an_empty_list()
        {
            var (history, file, work) = Make();
            try
            {
                Assert.Empty(history.Shelf(Always));
                File.WriteAllText(file, "{ not json");
                Assert.Empty(history.Shelf(Always));
                history.Record(@"C:\Notes\A"); // and the next change writes a good file
                Assert.Single(history.Recent);
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void A_deleted_folder_goes_off_both_lists_with_the_folders_under_it_and_only_those()
        {
            var (history, _, work) = Make();
            try
            {
                foreach (var path in new[] { @"C:\Notes\A", @"C:\Notes\A\Sub", @"C:\Notes\AB", @"C:\Other" }) history.Record(path);
                history.TogglePinned(@"C:\Notes\A\Sub");
                history.Remove(@"C:\Notes\A");
                Assert.Equal(new[] { @"C:\Other", @"C:\Notes\AB" }, history.Shelf(Always).Select(s => s.Path).ToArray());
                Assert.Empty(history.Pinned);
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void A_renamed_folder_keeps_its_place_and_its_pin_and_so_do_the_folders_under_it()
        {
            var (history, _, work) = Make();
            try
            {
                history.Record(@"C:\Notes\Old");
                history.Record(@"C:\Notes\Old\Sub");
                history.Record(@"C:\Notes\Other");
                history.TogglePinned(@"C:\Notes\Old");
                history.Rename(@"C:\Notes\Old", @"C:\Notes\New");
                Assert.True(history.IsPinned(@"C:\Notes\New"));
                Assert.False(history.IsPinned(@"C:\Notes\Old"));
                Assert.Equal(new[] { @"C:\Notes\New", @"C:\Notes\Other", @"C:\Notes\New\Sub" }, history.Shelf(Always).Select(s => s.Path).ToArray());
            }
            finally { Directory.Delete(work, true); }
        }
    }
}
