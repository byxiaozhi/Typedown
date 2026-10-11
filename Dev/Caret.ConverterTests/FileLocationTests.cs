using System;
using System.IO;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // Where a file is (Services/FileLocation.cs): the folder with the file selected, and the link to the file.
    public class FileLocationTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        [Fact]
        public void An_existing_file_is_selected_in_its_folder()
        {
            var path = Path.Combine(work, "My notes.md");
            File.WriteAllText(path, "x");
            var (outcome, arguments) = FileLocation.Reveal(path);
            Assert.Equal(FileLocation.Outcome.SelectedFile, outcome);
            Assert.Equal("/select,\"" + path + "\"", arguments);
        }

        [Fact]
        public void A_file_that_is_gone_opens_its_folder()
        {
            var path = Path.Combine(work, "moved.md");
            var (outcome, arguments) = FileLocation.Reveal(path);
            Assert.Equal(FileLocation.Outcome.OpenedFolder, outcome);
            Assert.Equal("\"" + work + "\"", arguments);
        }

        [Fact]
        public void A_file_and_a_folder_that_are_gone_are_not_found()
        {
            Assert.Equal(FileLocation.Outcome.NotFound, FileLocation.Reveal(Path.Combine(work, "no folder", "a.md")).Outcome);
            Assert.Equal(FileLocation.Outcome.NotFound, FileLocation.Reveal("").Outcome);
            Assert.Equal(FileLocation.Outcome.NotFound, FileLocation.Reveal(null).Outcome);
            Assert.Equal(FileLocation.Outcome.NotFound, FileLocation.Reveal("bad\0name").Outcome);
        }

        [Fact]
        public void The_link_is_a_file_address_that_keeps_spaces_and_special_characters()
        {
            Assert.Equal("file:///C:/My%20Notes/report%20%231.md", FileLocation.Link(@"C:\My Notes\report #1.md"));
            Assert.Equal("file:///C:/Users/ana/%C5%BC%C3%B3%C5%82w/%25done.md", FileLocation.Link(@"C:\Users\ana\" + "\u017c\u00f3\u0142w" + @"\%done.md"));
        }

        [Fact]
        public void A_network_path_is_a_link_with_the_server()
        {
            Assert.Equal("file://server/share/folder/a.md", FileLocation.Link(@"\\server\share\folder\a.md"));
        }

        [Fact]
        public void No_path_is_no_link()
        {
            Assert.Null(FileLocation.Link(null));
            Assert.Null(FileLocation.Link("  "));
            Assert.Null(FileLocation.Link("bad\0name"));
        }

        [Fact]
        public void The_link_opens_the_same_file()
        {
            var path = Path.Combine(work, "a b#c.md");
            File.WriteAllText(path, "x");
            Assert.Equal(path, new Uri(FileLocation.Link(path)).LocalPath);
        }

        [Fact]
        public void A_path_that_is_not_a_full_one_is_no_link_and_nothing_to_reveal()
        {
            // a relative name would be taken from the folder Caret was started in, even where a file of that name exists there
            var name = "relative-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".md";
            File.WriteAllText(name, "x");
            try
            {
                Assert.Null(FileLocation.Link(name));
                Assert.Null(FileLocation.Link(@"folder\" + name));
                Assert.Equal(FileLocation.Outcome.NotFound, FileLocation.Reveal(name).Outcome);
            }
            finally
            {
                File.Delete(name);
            }
        }
    }
}
