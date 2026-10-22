namespace Typedown.WinUI.Models
{
    // A row of the list of tracked changes in the Review panel (MainWindow.LiveReview.cs): what it is, its text, its number, and the
    // words for the two buttons. `CanAct` is false when one change cannot be accepted or rejected on its own.
    public sealed class TrackItem
    {
        public int Index { get; init; }

        public string Glyph { get; init; }

        public string Text { get; init; }

        public bool CanAct { get; init; }

        public string AcceptTip { get; init; }

        public string RejectTip { get; init; }
    }
}
