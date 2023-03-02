using System.Collections.Specialized;
using Typedown.Core.Interfaces;
using Typedown.Utilities;

namespace Typedown.Services
{
    public class Clipboard : IClipboard
    {
        public bool ContainsText(TextDataFormat format)
        {
            return System.Windows.Clipboard.ContainsText((System.Windows.TextDataFormat)format);
        }

        public string GetText(TextDataFormat format)
        {
            return System.Windows.Clipboard.GetText((System.Windows.TextDataFormat)format);
        }

        public void SetFileDropList(StringCollection fileDropList)
        {
            System.Windows.Clipboard.SetFileDropList(fileDropList);
        }

        public StringCollection GetFileDropList()
        {
            return System.Windows.Clipboard.GetFileDropList();
        }

        public IClipboardImage GetImage()
        {
            var image = System.Windows.Forms.Clipboard.GetImage();
            return new ClipboardImage(image);
        }

        public void SetText(string text, TextDataFormat format)
        {
            System.Windows.Clipboard.SetText(text, (System.Windows.TextDataFormat)format);
        }

        public void SetText(string text)
        {
            System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText);
        }
    }
}
