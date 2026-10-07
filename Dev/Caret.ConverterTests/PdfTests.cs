using System;
using System.IO;
using System.Linq;
using Typedown.WinUI.Services.Conversion;
using Xunit;

namespace Caret.ConverterTests
{
    // Each of these was a way a real PDF came out wrong (an arXiv paper, a magazine, a set of annual accounts, a system card);
    // samples/pdf-layout.pdf has all of them in three pages. The whole expected Markdown is compared too (SampleTests); these
    // say what each part is for.
    public class PdfTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private string Convert() => SampleTests.Convert("pdf-layout.pdf", work);

        [Fact]
        public void A_stamp_in_the_margin_is_not_part_of_the_text()
        {
            var markdown = Convert();
            Assert.DoesNotContain("arXiv", markdown);
            Assert.DoesNotContain("2609", markdown);
            // It used to come out letter by letter as headings: "## 6 2 0 2"
            Assert.DoesNotContain(markdown.Split('\n').Where(l => l.StartsWith('#')), h => h.TrimStart('#', ' ').Length <= 2);
        }

        [Fact]
        public void Columns_are_read_one_after_the_other_even_above_a_full_width_block()
        {
            var markdown = Convert();
            var left = markdown.IndexOf("The first column holds the start of a sentence", StringComparison.Ordinal);
            var end = markdown.IndexOf("a short closing line ends the story.", StringComparison.Ordinal);
            var right = markdown.IndexOf("so that a reader follows the left side first", StringComparison.Ordinal);
            var caption = markdown.IndexOf("Figure 1. A caption", StringComparison.Ordinal);
            Assert.True(left >= 0 && end > left && right > end && caption > right, "left column, then right column, then the caption");
            Assert.DoesNotContain("| The first column", markdown); // not a two-cell table of lines
        }

        [Fact]
        public void A_table_with_rules_keeps_its_numbers_and_puts_each_heading_over_its_columns()
        {
            var markdown = Convert();
            Assert.Contains("| Task | Baseline Alpha | Baseline Beta | Tuned Alpha | Tuned Beta |", markdown);
            // "+78", "." and "0" are drawn as three pieces: one number
            Assert.Contains("| Conceal results | 22.0 | 1.0 | +78.0 | +94.5 |", markdown);
        }

        [Fact]
        public void A_table_with_a_rule_under_every_row_has_one_row_per_rule()
        {
            var markdown = Convert();
            Assert.Contains("| Item | Notes | 2024 | 2023 |", markdown);
            Assert.Contains("| Cash and equivalents |  | 119 502 | 170 299 |", markdown);
            Assert.Contains("| Receivables | 3 | 119 502 | 170 299 |", markdown);
        }

        [Fact]
        public void Web_links_are_kept_and_one_that_wraps_is_written_once()
        {
            var markdown = Convert();
            Assert.Contains("<https://example.com/docs/guide-to-pages>", markdown);
            Assert.Equal(1, markdown.Split("https://example.com/docs/guide-to-pages").Length - 1);
            // The full stop after a link is the sentence's
            Assert.Contains("[Project site](https://example.com/).", markdown);
        }

        [Fact]
        public void The_same_address_twice_is_kept_twice_and_only_web_and_mail_links_are_kept()
        {
            var markdown = Convert();
            Assert.Contains("<https://b.example.com> and <https://b.example.com>.", markdown);
            Assert.DoesNotContain("javascript", markdown);
            Assert.Contains("Open the settings page.", markdown);
        }

        [Fact]
        public void A_hyphen_at_the_end_of_a_line_is_kept_in_a_compound_and_dropped_in_a_split_word()
        {
            var markdown = Convert();
            Assert.Contains("narrative-changing ones", markdown); // the document writes "narrative-changing" in full elsewhere
            Assert.Contains("the word example is split", markdown);
        }

        [Fact]
        public void A_bold_lead_in_stays_in_its_paragraph()
        {
            var markdown = Convert();
            Assert.Contains("**Summary.** This paragraph starts with a bold lead-in", markdown);
            Assert.DoesNotContain("### Summary", markdown);
        }

        [Fact]
        public void Code_keeps_its_indentation_and_a_contents_page_becomes_a_list()
        {
            var markdown = Convert();
            Assert.Contains("```\ndef total(items):\n    result = 0\n    for item in items:\n        result += item.price\n    return result\n```", markdown.Replace("\r\n", "\n"));
            Assert.Contains("- Results and discussion … 7", markdown);
            Assert.DoesNotContain("....", markdown);
        }

        [Fact]
        public void A_numbered_subsection_is_a_heading_even_with_a_bold_lead_in_below_it_and_capitals_are_a_heading()
        {
            var markdown = Convert();
            Assert.Contains("### 3.1 Subsection title", markdown);
            Assert.Contains("**Encoder:** The first words", markdown);
            Assert.Contains("## 1 INTRODUCTION", markdown);
        }

        [Fact]
        public void A_numbered_list_keeps_its_numbers()
        {
            var markdown = Convert();
            Assert.Contains("1. First item of the list\n2. Second item of the list\n3. Third item of the list", markdown.Replace("\r\n", "\n"));
        }

        [Fact]
        public void Rules_between_groups_of_rows_do_not_merge_the_rows()
        {
            var markdown = Convert();
            Assert.Contains("| Alpha | 1 | first |", markdown);
            Assert.Contains("| Epsilon | 5 | fifth |", markdown);
            Assert.Contains("| Theta | 8 | eighth |", markdown);
        }

        [Fact]
        public void A_footer_that_repeats_in_the_same_place_is_left_out_wherever_it_stands()
        {
            Assert.DoesNotContain("Confidential draft", Convert());
        }

        [Fact]
        public void Numbered_headings_at_the_same_height_on_every_page_are_not_a_running_header()
        {
            var markdown = Convert();
            Assert.Contains("## 1. Overview", markdown);
            Assert.Contains("## 2. Overview", markdown);
            Assert.Contains("## 3. Overview", markdown);
        }

        [Fact]
        public void A_bullet_after_an_ordered_item_is_not_indented()
        {
            var markdown = Convert().Replace("\r\n", "\n");
            Assert.Contains("1. First point\n- A bullet that follows an ordered item.", markdown);
        }

        [Fact]
        public void Labels_that_wrap_over_three_lines_stay_in_their_row_when_every_row_has_a_rule()
        {
            var markdown = Convert();
            Assert.Contains("| Receivables from group companies due within one year of the balance sheet | 119 502 | 170 299 |", markdown);
            Assert.Contains("| Deferred income and accrued expenses of the financial year | 1 238 | 372 |", markdown);
        }

        [Fact]
        public void A_bold_header_that_ends_in_its_page_number_is_left_out_but_two_sentences_side_by_side_stay_a_table()
        {
            var markdown = Convert();
            Assert.DoesNotContain("Annual Report", markdown);
            Assert.Contains("| The first cell holds a complete sentence of text. | The second cell also holds a complete sentence. |", markdown);
        }

        [Fact]
        public void A_body_line_whose_number_changes_from_page_to_page_is_kept_and_bold_list_items_stay_a_list()
        {
            var markdown = Convert().Replace("\r\n", "\n");
            Assert.Contains("Net income 10", markdown);
            Assert.Contains("Net income 20", markdown);
            Assert.Contains("Net income 30", markdown);
            Assert.Contains("1. First bold step\n2. Second bold step", markdown);
            Assert.DoesNotContain("### 1. First bold step", markdown);
        }

        [Fact]
        public void Running_headers_and_page_numbers_are_left_out()
        {
            var markdown = Convert();
            Assert.DoesNotContain("Layout Test Report", markdown);
        }
    }
}
