using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

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
        readonly List<HistoryModel> histories = new();
        HistoryModel pending = new();
        int index = -1;
        private System.Threading.Timer commitTimer;

        public bool Undoable { get; set; }
        public bool Redoable { get; set; }
        public bool IsPending => pending.Text != null && pending.Cursor != null;

        public ContentHistory()
        {
            commitTimer = new System.Threading.Timer(_ => CommitPending());
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
                    StopTimer();
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
                histories.Clear();
                StopTimer();
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
                StopTimer();
                histories.RemoveRange(index + 1, histories.Count - (index + 1));
                histories.Add(pending);
                if (histories.Count > deep)
                {
                    histories.RemoveAt(0);
                }
                else
                {
                    index++;
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

        private void StopTimer()
        {
            commitTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        }

        private void ResetTimer()
        {
            StopTimer();
            commitTimer?.Change(TimeSpan.FromSeconds(3), System.Threading.Timeout.InfiniteTimeSpan);
        }

        public void CursorChange(CursorState cursor)
        {
            try
            {
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

        public void ContentChange(string content)
        {
            try
            {
                content = content.TrimEnd('\r', '\n');
                if ((pending.Text != null && pending.Text == content) ||
                    (pending.Text == null && index > -1 && histories[index].Text.Trim('\r','\n') == content.Trim('\r', '\n')))
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
