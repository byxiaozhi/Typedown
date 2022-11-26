using System.Collections.Specialized;
using System.IO;
using System.IO.Pipes;
using System.Windows.Media.Imaging;
using Typedown.Universal.Interfaces;

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

        public byte[] GetImage()
        {
            var image = System.Windows.Clipboard.GetImage();
            if (image == null) return null;
            using var memoryStream = new MemoryStream();
            var encoder = new PngBitmapEncoder()
            {
                Frames = { BitmapFrame.Create(image) }
            };
            encoder.Save(memoryStream);
            return memoryStream.GetBuffer();
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
