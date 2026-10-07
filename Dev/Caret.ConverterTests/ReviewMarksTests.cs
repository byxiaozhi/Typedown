using System;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The text of a review comment: CriticMarkup with the author and the day inside (Services/ReviewMarks.cs).
    public class ReviewMarksTests
    {
        private static readonly DateTime Day = new(2026, 10, 7, 23, 59, 0);

        [Fact]
        public void StampHasAuthorAndDay()
        {
            Assert.Equal("{>>@Ferenc 2026-10-07<<}", ReviewMarks.Stamp("Ferenc", Day));
            Assert.Equal("{>>@Ferenc 2026-10-07: is this right?<<}", ReviewMarks.Stamp("Ferenc", Day, "is this right?"));
        }

        [Fact]
        public void TheDayIsWrittenTheSameInEveryCulture()
        {
            var before = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                Assert.Equal("2026-10-07", ReviewMarks.Day(Day));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = before;
            }
        }

        [Theory]
        [InlineData("Ferenc Szilagyi", "Ferenc Szilagyi")]
        [InlineData("  a   b  ", "a b")]
        [InlineData("Ann: <the {boss}>", "Ann the boss")]
        [InlineData("@me", "me")]
        [InlineData("", "?")]
        [InlineData(null, "?")]
        [InlineData("{}:", "?")]
        public void TheNameIsOneLineWithoutTheCharactersOfTheMarkup(string name, string expected) =>
            Assert.Equal(expected, ReviewMarks.Author(name));

        [Fact]
        public void TheNoteIsOneLineAndCannotCloseTheMark()
        {
            Assert.Equal("first second", ReviewMarks.Note("first\r\n\r\nsecond"));
            Assert.DoesNotContain("<<}", ReviewMarks.Note("oops <<} and <<<} more"));
            Assert.Equal("kept: {>> inside <<", ReviewMarks.Note("kept: {>> inside <<"));
        }

        [Fact]
        public void ASingleLineSelectionIsMarkedAndTheNoteFollowsIt()
        {
            var (markup, wraps) = ReviewMarks.Comment("the number", "check it", "Anna", Day);
            Assert.True(wraps);
            Assert.Equal("{==the number==}{>>@Anna 2026-10-07: check it<<}", markup);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("two\nlines")]
        [InlineData("two\r\nlines")]
        [InlineData("holds ==} the closing mark")]
        [InlineData("holds {== the opening mark")]
        public void WhatCannotBeMarkedGetsOnlyTheComment(string selected)
        {
            var (markup, wraps) = ReviewMarks.Comment(selected, "note", "Anna", Day);
            Assert.False(wraps);
            Assert.Equal("{>>@Anna 2026-10-07: note<<}", markup);
        }

        [Fact]
        public void TheSpacesAroundASelectionStayOutsideTheMark()
        {
            // a double-click in Windows selects the word with the space after it
            var (markup, wraps) = ReviewMarks.Comment("sentence ", "check", "A", Day);
            Assert.True(wraps);
            Assert.Equal("{==sentence==}{>>@A 2026-10-07: check<<} ", markup);
            Assert.Equal(" {==two words==}{>>@A 2026-10-07: n<<}  ", ReviewMarks.Comment(" two words  ", "n", "A", Day).Markup);
        }

        [Fact]
        public void MarkdownInTheSelectionIsKeptAsItIs() =>
            Assert.Equal("{==a **bold** [link](x)==}{>>@A 2026-10-07: n<<}", ReviewMarks.Comment("a **bold** [link](x)", "n", "A", Day).Markup);

        [Fact]
        public void APreviewIsOneShortLine()
        {
            Assert.Equal("a b", ReviewMarks.Preview("a\n  b"));
            var preview = ReviewMarks.Preview(new string('x', 500));
            Assert.Equal(ReviewMarks.PreviewLength + 1, preview.Length);
            Assert.EndsWith("…", preview);
        }
    }
}
