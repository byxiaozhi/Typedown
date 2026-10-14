using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Windows.UI.Xaml;

namespace Typedown.Core.Models
{
    public class HistoryModel
    {
        public string Text { get; set; } = null;
        public CursorState Cursor { get; set; } = null;
    }

    public class ContentHistory : INotifyPropertyChanged
    {
        const int deep = 100;
        // Snapshots are whole-document strings; also cap the total size so a multi-MB document cannot pin
        // hundreds of MB of undo history (UTF-16: 32M chars ~= 64 MB).
        const long maxTotalChars = 32L * 1024 * 1024;
        long totalChars = 0;
        readonly List<HistoryModel> histories = new();
        HistoryModel pending = new();
        int index = -1;
        // The caret last reported. Source mode reports it before the change it goes with: a change then had no caret, was
        // never a step (a step needs both), and an undo went past it - the letter just typed and the step before (KY01).
        CursorState lastCursor;
        private readonly DispatcherTimer commitTimer = new();

        public bool Undoable { get; set; }
        public bool Redoable { get; set; }
        public bool IsPending { get => pending.Text != null && pending.Cursor != null; }

        public ContentHistory()
        {
            commitTimer.Tick += (s, e) => CommitPending();
        }

        public HistoryModel Undo()
        {
            try
            {
                if (index > 0 || (index == 0 && IsPending))
                {
                    CommitPending();
                    index--;
                    Redoable = true;
                    Undoable = index > 0;
                    return histories[index];
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
            return null;
        }

        public HistoryModel Redo()
        {
            try
            {
                if (index < histories.Count - 1)
                {
                    commitTimer.Stop();
                    pending = new();
                    index++;
                    Redoable = index < histories.Count - 1;
                    Undoable = true;
                    return histories[index];
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
            return null;
        }

        public void ClearHistory()
        {
            try
            {
                lastCursor = null;
                histories.Clear();
                totalChars = 0;
                commitTimer.Stop();
                pending = new();
                index = -1;
                Redoable = false;
                Undoable = false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        public void CommitPending()
        {
            try
            {
                if (!IsPending) return;
                commitTimer.Stop();
                for (var i = index + 1; i < histories.Count; i++)
                    totalChars -= histories[i].Text?.Length ?? 0;
                histories.RemoveRange(index + 1, histories.Count - (index + 1));
                histories.Add(pending);
                totalChars += pending.Text?.Length ?? 0;
                index++;
                while (histories.Count > deep || (histories.Count > 1 && totalChars > maxTotalChars))
                {
                    totalChars -= histories[0].Text?.Length ?? 0;
                    histories.RemoveAt(0);
                    index--;
                }
                pending = new();
                Redoable = false;
                Undoable = index > 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private void ResetTimer()
        {
            commitTimer.Stop();
            commitTimer.Interval = TimeSpan.FromSeconds(3);
            commitTimer.Start();
        }

        public void CursorChange(CursorState cursor)
        {
            try
            {
                if (cursor != null) lastCursor = cursor;
                if (pending.Text == null && index > -1)
                {
                    histories[index].Cursor = cursor;
                    return;
                }
                if (cursor == null)
                {
                    return;
                }
                if (IsPending && pending.Cursor.Focus.Line != cursor.Focus.Line)
                {
                    pending.Cursor = cursor;
                    CommitPending();
                    return;
                }
                pending.Cursor = cursor;
                if (pending.Text != null && histories.Count == 0)
                {
                    CommitPending();
                    return;
                }
                StateChange();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        // Compare ignoring trailing newlines without allocating trimmed copies: this runs on every keystroke
        // with the whole document as input.
        private static int LengthWithoutTrailingNewlines(string s)
        {
            var n = s.Length;
            while (n > 0 && (s[n - 1] == '\n' || s[n - 1] == '\r')) n--;
            return n;
        }

        private static bool EqualsIgnoringTrailingNewlines(string a, string b)
        {
            if (a == null || b == null) return a == b;
            var la = LengthWithoutTrailingNewlines(a);
            var lb = LengthWithoutTrailingNewlines(b);
            return la == lb && string.CompareOrdinal(a, 0, b, 0, la) == 0;
        }

        public void ContentChange(string content)
        {
            try
            {
                if (content == null) return;
                if ((pending.Text != null && EqualsIgnoringTrailingNewlines(pending.Text, content)) ||
                    (pending.Text == null && index > -1 && EqualsIgnoringTrailingNewlines(histories[index].Text, content)))
                {
                    return;
                }
                pending.Text = content;
                pending.Cursor ??= lastCursor;
                if (pending.Cursor != null && histories.Count == 0)
                {
                    CommitPending();
                    return;
                }
                StateChange();
                ResetTimer();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private void StateChange()
        {
            Redoable = index < histories.Count - 1;
            Undoable = index > 0 || (index == 0 && pending.Text != null && pending.Cursor != null);
        }

        public void InitHistory(string content)
        {
            ClearHistory();
            CursorChange(new(Focus: new(Line: 0, Ch: 0), Anchor: new(Line: 0, Ch: 0)));
            ContentChange(content);
        }
#pragma warning disable CS0067
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067
    }

}
