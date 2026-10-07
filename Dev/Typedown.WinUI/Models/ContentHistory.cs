using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace Typedown.WinUI.Models
{
    // CodeMirror-style cursor, the shape the editor sends in CursorChange and accepts back in
    // SetMarkdown. Ported from Typedown.Core\Models\RuntimeModels\CursorState.cs.
    public record CursorState(CursorState.Pos Anchor, CursorState.Pos Focus)
    {
        public record Pos(int Line, int Ch);
    }

    public class HistoryModel
    {
        public string Text { get; set; }
        public CursorState Cursor { get; set; }
    }

    // Ported from Typedown.Core\Models\RuntimeModels\ContentHistory.cs, logic unchanged. The editor
    // (Muya) has no undo of its own: the original kept this snapshot history on the host and restored
    // a snapshot by sending SetMarkdown { text, cursor }. A pending snapshot is committed after 3s
    // without further edits or when the cursor moves to another line, so undo steps are "a burst of
    // typing on one line" rather than single keystrokes. Changes from the original: Microsoft.UI.Xaml
    // DispatcherTimer, and a Changed event instead of INotifyPropertyChanged (no x:Bind here).
    public class ContentHistory
    {
        const int deep = 100;
        readonly List<HistoryModel> histories = new();
        HistoryModel pending = new();
        int index = -1;
        private readonly DispatcherTimer commitTimer = new() { Interval = TimeSpan.FromSeconds(3) };

        public bool Undoable { get; private set; }
        public bool Redoable { get; private set; }
        public bool IsPending => pending.Text != null && pending.Cursor != null;

        public event Action Changed;

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
                    Changed?.Invoke();
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
                    Changed?.Invoke();
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
            histories.Clear();
            commitTimer.Stop();
            pending = new();
            index = -1;
            Redoable = false;
            Undoable = false;
            Changed?.Invoke();
        }

        public void CommitPending()
        {
            try
            {
                if (!IsPending) return;
                commitTimer.Stop();
                histories.RemoveRange(index + 1, histories.Count - (index + 1));
                histories.Add(pending);
                if (histories.Count > deep)
                    histories.RemoveAt(0);
                else
                    index++;
                pending = new();
                Redoable = false;
                Undoable = index > 0;
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        public void CursorChange(CursorState cursor)
        {
            try
            {
                if (cursor == null) return;
                if (pending.Text == null && index > -1)
                {
                    histories[index].Cursor = cursor;
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

        public void ContentChange(string content)
        {
            try
            {
                content = content.TrimEnd('\r', '\n');
                if ((pending.Text != null && pending.Text == content) ||
                    (pending.Text == null && index > -1 && histories[index].Text.Trim('\r', '\n') == content.Trim('\r', '\n')))
                {
                    return;
                }
                pending.Text = content;
                if (pending.Cursor != null && histories.Count == 0)
                {
                    CommitPending();
                    return;
                }
                StateChange();
                commitTimer.Stop();
                commitTimer.Start();
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
            Changed?.Invoke();
        }

        public void InitHistory(string content)
        {
            ClearHistory();
            CursorChange(new(new(0, 0), new(0, 0)));
            ContentChange(content);
        }
    }
}
