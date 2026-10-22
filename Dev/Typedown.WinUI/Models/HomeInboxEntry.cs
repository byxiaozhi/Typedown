namespace Typedown.WinUI.Models
{
    // A row of the comments waiting on Home: a note that has comments in it, with how many (the name, the folders and the day are those of a file row).
    public sealed class HomeInboxEntry : StartPageEntry
    {
        public HomeInboxEntry(string fullPath, int comments) : base(fullPath, false) => Comments = comments;

        public int Comments { get; }

        public string CommentCount => Comments.ToString();
    }
}
