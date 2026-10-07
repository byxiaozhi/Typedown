using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // Accepting, rejecting and deleting in a document that carries a review (Services/ReviewMarks.Resolve.cs).
    public class ReviewResolveTests
    {
        private static string Accept(string text) => ReviewMarks.Resolve(text, ReviewAction.Accept);
        private static string Reject(string text) => ReviewMarks.Resolve(text, ReviewAction.Reject);
        private static string Comments(string text) => ReviewMarks.Resolve(text, ReviewAction.DeleteComments);

        [Theory]
        [InlineData("a {++new++} b", "a new b", "a b")]
        [InlineData("a {--old--} b", "a b", "a old b")]
        [InlineData("a {~~old~>new~~} b", "a new b", "a old b")]
        public void AChangeIsKeptOrUndone(string text, string accepted, string rejected)
        {
            Assert.Equal(accepted, Accept(text));
            // what goes leaves one space between its neighbours, not two
            Assert.Equal(rejected, Reject(text));
        }

        [Fact]
        public void TheStampsOfAChangeGoWithIt()
        {
            const string text = "the {~~was late~>is on time~~}{>>@Ferenc 2026-10-07<<} report";
            Assert.Equal("the is on time report", Accept(text));
            Assert.Equal("the was late report", Reject(text));
        }

        [Fact]
        public void ANoteOnAChangeStaysWhenTheChangeIsResolved()
        {
            const string text = "x {++new++}{>>@A 2026-10-07<<}{>>@B 2026-10-08: why?<<} y";
            Assert.Equal("x new{>>@B 2026-10-08: why?<<} y", Accept(text));
            Assert.Equal("x {>>@B 2026-10-08: why?<<} y", Reject(text));
        }

        [Fact]
        public void CommentsAndHighlightsAreLeftWhenChangesAreResolved()
        {
            const string text = "a {==this==}{>>@A 2026-10-07: check<<} and {++that++}{>>@A 2026-10-07<<}";
            Assert.Equal("a {==this==}{>>@A 2026-10-07: check<<} and that", Accept(text));
            Assert.Equal("a {==this==}{>>@A 2026-10-07: check<<} and ", Reject(text));
        }

        [Fact]
        public void DeletingCommentsUnwrapsTheHighlightAndKeepsTheChangesAndWhoMadeThem()
        {
            const string text = "a {==this==}{>>@A 2026-10-07: check<<} and {++that++}{>>@A 2026-10-07<<} and {>>@B 2026-10-08: lone<<}.";
            Assert.Equal("a this and {++that++}{>>@A 2026-10-07<<} and .", Comments(text));
        }

        [Fact]
        public void ANoteAfterAChangeIsDeletedButItsStampIsKept() =>
            Assert.Equal("x {++new++}{>>@A 2026-10-07<<} y", Comments("x {++new++}{>>@A 2026-10-07<<}{>>@B 2026-10-08: why?<<} y"));

        [Fact]
        public void AParagraphThatWasDeletedLeavesNoEmptyParagraph()
        {
            const string text = "First\n\n{--Second--}{>>@A 2026-10-07<<}\n\nThird\n";
            Assert.Equal("First\n\nThird\n", Accept(text));
            Assert.Equal("First\n\nSecond\n\nThird\n", Reject(text));
        }

        [Fact]
        public void AListItemThatWasDeletedLeavesNoEmptyItem()
        {
            const string text = "- one\n- {--two--}\n- three\n";
            Assert.Equal("- one\n- three\n", Accept(text));
            Assert.Equal("- one\n- two\n- three\n", Reject(text));
        }

        [Fact]
        public void AHeadingOrAQuoteThatWasDeletedGoes()
        {
            Assert.Equal("Intro\n\nBody\n", Accept("Intro\n\n# {--Title--}\n\nBody\n"));
            Assert.Equal("Intro\n\nBody\n", Accept("Intro\n\n> {--Quoted--}\n\nBody\n"));
        }

        [Fact]
        public void AParagraphThatWasAddedBecomesPlainOnAcceptAndGoesOnReject()
        {
            const string text = "First\n\n{++Second++}{>>@A 2026-10-07<<}\n\nThird\n";
            Assert.Equal("First\n\nSecond\n\nThird\n", Accept(text));
            Assert.Equal("First\n\nThird\n", Reject(text));
        }

        [Fact]
        public void TextOnTheSameLineKeepsTheLine() =>
            Assert.Equal("Keep this.\n", Accept("Keep {--that--} this.\n"));

        [Fact]
        public void CodeIsNeverAMark()
        {
            const string text = "Use `{++x++}` here\n\n```\n{--not a change--}\n```\n\nand {--this--}.\n";
            Assert.Equal("Use `{++x++}` here\n\n```\n{--not a change--}\n```\n\nand .\n", Accept(text));
            Assert.Equal("Use `{++x++}` here\n\n```\n{--not a change--}\n```\n\nand this.\n", Reject(text));
        }

        [Fact]
        public void AnEscapedBraceIsNotAMark() =>
            Assert.Equal("\\{++x++} and y", Accept("\\{++x++} and {++y++}"));

        [Fact]
        public void AMarkDoesNotRunAcrossParagraphs()
        {
            const string text = "{++open\n\nnever closed++}";
            Assert.Equal(text, Accept(text));
        }

        [Fact]
        public void MarkdownInsideAMarkIsKept() =>
            Assert.Equal("a **bold** word", Accept("a {++**bold**++} word"));

        [Fact]
        public void ATextWithoutMarksIsReturnedAsItIs()
        {
            const string text = "Nothing {here} and `code` and {a} brace.\r\nSecond line.";
            Assert.Equal(text, Accept(text));
            Assert.Equal(text, Reject(text));
            Assert.Equal(text, Comments(text));
        }

        [Fact]
        public void WindowsLineEndingsAreKept() =>
            Assert.Equal("First\r\n\r\nThird\r\n", Accept("First\r\n\r\n{--Second--}\r\n\r\nThird\r\n"));

        [Fact]
        public void AllThreeKindsInOneText()
        {
            const string text = "{~~a~>b~~} and {++c++} and {--d--}";
            Assert.Equal("b and c and ", Accept(text));
            Assert.Equal("a and and d", Reject(text));
        }

        [Theory]
        [InlineData("{++a++}", "Change")]
        [InlineData("{--a--}{>>@A 2026-10-07<<}", "Change")]
        [InlineData("{~~a~>b~~}", "Change")]
        [InlineData("{==a==}{>>@A 2026-10-07: n<<}", "Comment")]
        [InlineData("{>>@A 2026-10-07: n<<}", "Comment")]
        [InlineData("plain", "None")]
        [InlineData("", "None")]
        public void TheKindOfAChain(string raw, string kind) => Assert.Equal(kind, ReviewMarks.KindOf(raw).ToString());

        [Theory]
        [InlineData("{>>@A 2026-10-07: a note<<}", true)]
        [InlineData("{>>@A 2026-10-07<<}", false)]
        [InlineData("{==a==}", false)]
        [InlineData("{++a++}", false)]
        [InlineData("", false)]
        public void OnlyANoteIsANote(string raw, bool note) => Assert.Equal(note, ReviewMarks.IsNote(raw));

        [Fact]
        public void ANoteOnItsOwnIsDeletedWithoutTouchingWhatSurroundsIt() =>
            Assert.Equal("", Comments("{>>@B 2026-10-08: reply<<}"));

        [Fact]
        public void CountsLeaveOutTheStamps()
        {
            var (changes, comments) = ReviewMarks.Count("{++a++}{>>@A 2026-10-07<<} {==b==}{>>@A 2026-10-07: n<<} {>>@B 2026-10-08: lone<<} `{--x--}`");
            Assert.Equal(1, changes);
            Assert.Equal(3, comments); // the highlight, its note and the lone note
        }
    }
}
