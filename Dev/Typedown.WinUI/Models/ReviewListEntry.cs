using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Typedown.WinUI.Models
{
    // One row of the Review tab of the inspector pane (bound by the list's row template).
    public class ReviewListEntry
    {
        public string Label { get; init; }
        public string Meta { get; init; }
        public string Snippet { get; init; }
        // The mark as written in the text, and which of the equal ones it is: the editor goes to it by these.
        public string Raw { get; init; }
        public int Nth { get; init; }
        // The colour of the kind's badge: green added, red deleted, amber replaced, yellow comment and highlight (see through, so it works in light and dark)
        public Brush Tint { get; init; }

        public static Brush TintOf(string kind) => new SolidColorBrush(kind switch
        {
            "add" => Color.FromArgb(0x55, 0x3F, 0xAE, 0x6B),
            "del" => Color.FromArgb(0x55, 0xE0, 0x60, 0x5A),
            "sub" => Color.FromArgb(0x55, 0xEF, 0x9F, 0x27),
            _ => Color.FromArgb(0x55, 0xD9, 0xB6, 0x4A),
        });
    }
}
