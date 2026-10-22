using System;
using System.IO;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The file of rehearsals beside a speech (Services/SpeechRehearsal.cs): where it is, how a run is added, how the last one is found.
    public class SpeechRehearsalTests
    {
        private const string RunA = "## Rehearsal 2026-10-08 09:05\n\nPlanned 1:11, read in 1:06.\n";
        private const string RunB = "## Rehearsal 2026-10-09 10:00\r\n\r\nPlanned 1:11, read in 1:20.\r\n";

        [Fact]
        public void The_file_is_beside_the_speech_with_its_name()
        {
            Assert.Equal(Path.Combine(@"C:\talks", "keynote.rehearsal.md"), SpeechRehearsal.PathFor(@"C:\talks\keynote.md"));
            Assert.Equal(Path.Combine(@"C:\talks", "notes.rehearsal.md"), SpeechRehearsal.PathFor(@"C:\talks\notes.markdown"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void A_document_with_no_file_has_no_rehearsal_file(string path) => Assert.Null(SpeechRehearsal.PathFor(path));

        [Fact]
        public void A_new_file_starts_with_a_title_and_a_run_is_added_at_the_end()
        {
            var first = SpeechRehearsal.Append("", RunA, "Rehearsals of keynote");
            Assert.StartsWith("# Rehearsals of keynote\n\n## Rehearsal 2026-10-08", first);
            var second = SpeechRehearsal.Append(first, RunB, "ignored");
            Assert.DoesNotContain("ignored", second);
            Assert.EndsWith("read in 1:20.\n", second);
            Assert.DoesNotContain("\r", second);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(second, "^## Rehearsal ", System.Text.RegularExpressions.RegexOptions.Multiline).Count);
        }

        [Fact]
        public void The_last_run_is_found_whatever_the_line_endings()
        {
            var file = SpeechRehearsal.Append(SpeechRehearsal.Append("", RunA, "T"), RunB, "T");
            Assert.StartsWith("## Rehearsal 2026-10-09 10:00", SpeechRehearsal.LastRun(file));
            Assert.StartsWith("## Rehearsal 2026-10-09 10:00", SpeechRehearsal.LastRun(file.Replace("\n", "\r\n")));
            Assert.StartsWith("## Rehearsal 2026-10-08", SpeechRehearsal.LastRun(RunA));
            Assert.Equal("", SpeechRehearsal.LastRun("# Rehearsals\n\nnothing yet"));
            Assert.Equal("", SpeechRehearsal.LastRun(null));
        }

        [Fact]
        public void Saving_adds_to_the_file_beside_the_document_and_reading_finds_the_last_run()
        {
            var folder = Path.Combine(Path.GetTempPath(), "caret-rehearsal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var doc = Path.Combine(folder, "keynote.md");
                Assert.Equal("", SpeechRehearsal.LastRunFor(doc));
                var path = SpeechRehearsal.Save(doc, RunA, "Rehearsals of keynote");
                Assert.Equal(Path.Combine(folder, "keynote.rehearsal.md"), path);
                SpeechRehearsal.Save(doc, RunB, "Rehearsals of keynote");
                var text = File.ReadAllText(path);
                Assert.Contains("2026-10-08", text);
                Assert.Contains("2026-10-09", text);
                Assert.StartsWith("## Rehearsal 2026-10-09", SpeechRehearsal.LastRunFor(doc));
                Assert.False(File.Exists(path + ".tmp"));
                Assert.False(File.Exists(doc), "the speech itself is never created or changed");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void Saving_for_a_document_with_no_file_says_so()
        {
            Assert.Throws<InvalidOperationException>(() => SpeechRehearsal.Save("", RunA, "T"));
        }
    }
}
