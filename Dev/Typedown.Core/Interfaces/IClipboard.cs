using System.Collections.Specialized;

namespace Typedown.Core.Interfaces
{
    public enum TextDataFormat
    {
        Text,
        UnicodeText,
        Rtf,
        Html,
        CommaSeparatedValue,
        Xaml
    }

    public interface IClipboard
    {
        bool ContainsText(TextDataFormat format);

        string GetText(TextDataFormat format);

        void SetText(string text, TextDataFormat format);

        void SetText(string text);

        StringCollection GetFileDropList();

        void SetFileDropList(StringCollection fileDropList);

        IClipboardImage GetImage();
    }

    public interface IClipboardImage
    {
        void SaveAsPng(string path);

        byte[] GetBytes();
    }
}
