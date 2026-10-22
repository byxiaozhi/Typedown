using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    // Phase 3: the sentinels of WordMarks.cs become tracked changes, comments, highlights and gray notes. The text of a paragraph goes
    // through here piece by piece; what is open (an insertion, a deletion, a highlight, a comment range) is remembered until its end.
    internal sealed partial class WordBuilder
    {
        private sealed class MarkState
        {
            public int Insert = -1, Delete = -1, Highlight, Speech, ReplaceNumber = -1;
            public char Awaiting;
            public readonly StringBuilder Digits = new();
            public readonly Stack<int> Comments = new();
        }

        private readonly MarkTables marks = new();
        private MarkState state = new();
        private readonly HashSet<int> usedComments = new();

        private static bool HasMarks(string text)
        {
            foreach (var c in text) if (WordMarks.IsMark(c)) return true;
            return false;
        }

        // Text with its marks: the pieces between the sentinels become runs, in an insertion or a deletion when one is open.
        private void AddText(OpenXmlCompositeElement parent, string text, Fmt fmt)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (state.Awaiting == 0 && state.Insert < 0 && state.Delete < 0 && state.Highlight == 0 && state.Speech == 0 && !HasMarks(text))
            {
                parent.Append(TextRun(text, fmt));
                return;
            }
            var piece = new StringBuilder();
            void Emit()
            {
                if (piece.Length == 0) return;
                AddPiece(parent, piece.ToString(), fmt);
                piece.Clear();
            }
            foreach (var c in text)
            {
                if (state.Awaiting != 0)
                {
                    if (c >= '0' && c <= '9') { state.Digits.Append(c); continue; }
                    if (c == WordMarks.Terminator) { Emit(); FinishNumber(parent); continue; }
                    state.Awaiting = (char)0; // not a number after all: the sentinel is dropped and the text goes on
                }
                if (!WordMarks.IsMark(c)) { piece.Append(c); continue; }
                Emit();
                Handle(c, parent);
            }
            Emit();
        }

        private void Handle(char c, OpenXmlCompositeElement parent)
        {
            switch (c)
            {
                case WordMarks.InsertStart or WordMarks.DeleteStart or WordMarks.ReplaceStart or WordMarks.CommentStart or WordMarks.CommentPoint:
                    state.Awaiting = c;
                    state.Digits.Clear();
                    break;
                case WordMarks.InsertEnd or WordMarks.ReplaceEnd: state.Insert = -1; break;
                case WordMarks.DeleteEnd: state.Delete = -1; break;
                case WordMarks.ReplaceSplit: state.Delete = -1; state.Insert = state.ReplaceNumber; break;
                case WordMarks.HighlightStart: state.Highlight++; break;
                case WordMarks.HighlightEnd: state.Highlight = Math.Max(0, state.Highlight - 1); break;
                case WordMarks.SpeechStart: state.Speech++; break;
                case WordMarks.SpeechEnd: state.Speech = Math.Max(0, state.Speech - 1); break;
                case WordMarks.CommentEnd:
                    if (state.Comments.Count > 0) EndComment(parent, state.Comments.Pop());
                    break;
            }
        }

        // The number that follows an opening sentinel says which author and day, or which comment.
        private void FinishNumber(OpenXmlCompositeElement parent)
        {
            var kind = state.Awaiting;
            state.Awaiting = (char)0;
            if (!int.TryParse(state.Digits.ToString(), out var number)) return;
            switch (kind)
            {
                case WordMarks.InsertStart: state.Insert = number; break;
                case WordMarks.DeleteStart: state.Delete = number; break;
                case WordMarks.ReplaceStart: state.Delete = number; state.ReplaceNumber = number; break;
                case WordMarks.CommentStart:
                    parent.Append(new W.CommentRangeStart { Id = number.ToString() });
                    state.Comments.Push(number);
                    break;
                case WordMarks.CommentPoint:
                    parent.Append(new W.CommentRangeStart { Id = number.ToString() });
                    EndComment(parent, number);
                    break;
            }
        }

        private void EndComment(OpenXmlCompositeElement parent, int number)
        {
            parent.Append(new W.CommentRangeEnd { Id = number.ToString() });
            parent.Append(new W.Run(new W.RunProperties(new W.RunStyle { Val = WordStyles.CommentReference }), new W.CommentReference { Id = number.ToString() }));
            usedComments.Add(number);
        }

        // A piece of text in the state it is in: highlighted, gray, inserted or deleted.
        private void AddPiece(OpenXmlCompositeElement parent, string text, Fmt fmt)
        {
            if (state.Highlight > 0 || state.Speech > 0)
            {
                fmt = fmt.Clone();
                fmt.Highlight = state.Highlight > 0;
                fmt.Muted = state.Speech > 0;
            }
            var run = TextRun(text, fmt);
            if (state.Delete >= 0 && state.Delete < marks.Revisions.Count)
            {
                var plain = run.GetFirstChild<W.Text>();
                run.RemoveChild(plain);
                run.Append(new W.DeletedText(plain.Text) { Space = SpaceProcessingModeValues.Preserve });
                var (author, date) = marks.Revisions[state.Delete];
                parent.Append(new W.DeletedRun(run) { Id = (++bookmarkId).ToString(), Author = author, Date = Utc(date) });
            }
            else if (state.Insert >= 0 && state.Insert < marks.Revisions.Count)
            {
                var (author, date) = marks.Revisions[state.Insert];
                parent.Append(new W.InsertedRun(run) { Id = (++bookmarkId).ToString(), Author = author, Date = Utc(date) });
            }
            else parent.Append(run);
        }

        // The day or the moment as it is written in the file: no time zone shift.
        private static DateTime Utc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc);

        // The end of a paragraph closes what a mark left open: a comment range, a change, a highlight.
        private void CloseMarks(OpenXmlCompositeElement paragraph)
        {
            while (state.Comments.Count > 0) EndComment(paragraph, state.Comments.Pop());
            state.Insert = state.Delete = state.ReplaceNumber = -1;
            state.Highlight = state.Speech = 0;
            state.Awaiting = (char)0;
        }

        // The comments of the review, in a part of their own: the author, the day and the words of each. Only those that are in the text.
        private void AddComments()
        {
            if (usedComments.Count == 0) return;
            var part = main.AddNewPart<WordprocessingCommentsPart>();
            var comments = new W.Comments();
            foreach (var number in usedComments.OrderBy(n => n))
            {
                var (author, date, note) = marks.Comments[number];
                var initials = string.Concat(author.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
                comments.Append(new W.Comment(new W.Paragraph(
                    new W.ParagraphProperties(new W.ParagraphStyleId { Val = WordStyles.CommentText }),
                    new W.Run(new W.RunProperties(new W.RunStyle { Val = WordStyles.CommentReference }), new W.AnnotationReferenceMark()),
                    new W.Run(new W.Text(note) { Space = SpaceProcessingModeValues.Preserve })))
                { Id = number.ToString(), Author = author, Date = Utc(date), Initials = initials });
            }
            part.Comments = comments;
        }
    }
}
