using Xunit;

namespace Typedown.Automation.Tests
{
    public class DocumentTextTests
    {
        [Fact]
        public void Content_hash_is_sha256_of_utf8_lowercase_hex()
        {
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", DocumentText.ContentHash(""));
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", DocumentText.ContentHash("abc"));
            Assert.NotEqual(DocumentText.ContentHash("a\n"), DocumentText.ContentHash("a\r\n"));
            Assert.Equal(DocumentText.ContentHash("中文😀"), DocumentText.ContentHash("中文😀"));
        }

        [Theory]
        [InlineData("a\r\nb", "carriageReturn")]
        [InlineData("a\rb", "carriageReturn")]
        public void Text_with_cr_or_broken_utf16_is_refused(string text, string reason)
        {
            var e = Assert.Throws<AutomationException>(() => DocumentText.Validate("text", text));
            Assert.Equal(AutomationErrorKind.invalid_params, e.Kind);
            Assert.Equal(reason, e.ErrorData["reason"]);
            Assert.Equal("text", e.ErrorData["field"]);
        }

        [Fact]
        public void Lone_surrogates_are_refused()
        {
            // Not as InlineData: the test runner's serialization would replace a lone surrogate with U+FFFD.
            foreach (var text in new[] { "x" + '\ud800' + "y", "x" + '\udc00', "" + '\ud83d' })
                Assert.Equal("loneSurrogate", Assert.Throws<AutomationException>(() => DocumentText.Validate("text", text)).ErrorData["reason"]);
        }

        [Fact]
        public void Valid_text_passes()
        {
            DocumentText.Validate("text", "# 标题\n\n😀 é\n");
            DocumentText.Validate("text", "");
        }

        [Fact]
        public void ReplaceText_counts_ordinal_non_overlapping_left_to_right()
        {
            Assert.Equal("ba", DocumentText.ReplaceText("aaa", "aa", "b", 1));
            Assert.Equal("x-x-", DocumentText.ReplaceText("a-a-", "a", "x", 2));
            // Ordinal: case and composed/decomposed forms differ.
            Assert.Equal("A é é !", DocumentText.ReplaceText("A é é a", "a", "!", 1));
            Assert.Equal("!!", DocumentText.ReplaceText("😀😀", "😀", "!", 2));
        }

        [Fact]
        public void ReplaceText_changes_nothing_when_the_count_differs()
        {
            var e = Assert.Throws<AutomationException>(() => DocumentText.ReplaceText("a a a", "a", "b", 2));
            Assert.Equal(AutomationErrorKind.match_count_mismatch, e.Kind);
            Assert.Equal(-32019, e.Code);
            Assert.Equal(3L, e.ErrorData["actualCount"]);
            Assert.Equal(2L, e.ErrorData["expectedCount"]);
            Assert.Equal("abc", DocumentText.ReplaceText("abc", "zz", "y", 0));
            Assert.Equal("find", Assert.Throws<AutomationException>(() => DocumentText.ReplaceText("abc", "", "y", 0)).ErrorData["field"]);
        }
    }
}
