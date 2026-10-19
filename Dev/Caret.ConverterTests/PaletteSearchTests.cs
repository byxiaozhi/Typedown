using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The command palette's matching: what a person types and which command comes first.
    public class PaletteSearchTests
    {
        private static readonly (string Title, string Category)[] Commands =
        {
            ("Save", "File"), ("Save As...", "File"), ("Export", "File"), ("HTML...", "File › Export"), ("PDF...", "File › Export"),
            ("Bold", "Format"), ("Italic", "Format"), ("Find & Replace", "Edit"), ("Compare with another file...", "Review"),
            ("Code", "View"), ("Split (code + preview)", "View"), ("Command Palette...", "View"),
        };

        private static string[] Rank(string query) => Commands
            .Select(c => (c.Title, Score: PaletteSearch.Score(query, c.Title, c.Category)))
            .Where(x => x.Score >= 0).OrderByDescending(x => x.Score).Select(x => x.Title).ToArray();

        [Fact]
        public void An_empty_query_matches_everything_equally()
        {
            Assert.All(Commands, c => Assert.Equal(0, PaletteSearch.Score("", c.Title, c.Category)));
            Assert.Equal(0, PaletteSearch.Score("   ", "Save", "File"));
        }

        [Fact]
        public void A_word_that_is_not_in_the_command_rules_it_out()
        {
            Assert.Equal(-1, PaletteSearch.Score("zebra", "Save", "File"));
            Assert.Equal(-1, PaletteSearch.Score("save zebra", "Save", "File"));
        }

        [Fact]
        public void The_start_of_the_title_comes_before_the_middle_of_it()
        {
            Assert.Equal("Save", Rank("sav")[0]);
            Assert.Equal("Bold", Rank("bol")[0]);
            // "as" starts a word of "Save As..." and is inside "Compare ... another file"? no: only the first matches at a word start
            Assert.Equal("Save As...", Rank("as")[0]);
        }

        [Fact]
        public void Capitals_do_not_matter()
        {
            Assert.Equal(PaletteSearch.Score("BOLD", "Bold", "Format"), PaletteSearch.Score("bold", "Bold", "Format"));
        }

        [Fact]
        public void The_menu_name_counts_so_a_command_is_found_by_where_it_lives()
        {
            Assert.Contains("PDF...", Rank("export pdf"));
            Assert.Equal("PDF...", Rank("export pdf")[0]);
            Assert.Contains("Compare with another file...", Rank("review compare"));
        }

        [Fact]
        public void Every_word_has_to_be_there_but_not_in_one_piece()
        {
            Assert.Contains("Compare with another file...", Rank("file compare"));
            Assert.DoesNotContain("Italic", Rank("file compare"));
        }

        [Fact]
        public void Letters_in_order_find_a_command_when_nothing_else_does()
        {
            Assert.Contains("Italic", Rank("itlc"));
            Assert.DoesNotContain("Bold", Rank("itlc"));
        }

        [Fact]
        public void A_word_at_the_start_of_a_word_beats_the_same_letters_inside_one()
        {
            // "vi" starts "View" (the menu of Code) and is inside "preview" (in the title of Split): Code is the one meant
            Assert.Equal("Code", Rank("code vi")[0]);
            Assert.Equal("Code", Rank("view code")[0]);
            Assert.Contains("Split (code + preview)", Rank("code vi"));
        }

        [Fact]
        public void The_shorter_title_comes_first_among_equals()
        {
            Assert.True(PaletteSearch.Score("save", "Save", "File") > PaletteSearch.Score("save", "Save everything in all tabs", "File"));
        }
    }
}
