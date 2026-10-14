using System.Collections.Specialized;
using System.Threading.Tasks;

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

        Task<string> GetTextAsync(TextDataFormat format);

        void SetText(string text, TextDataFormat format);

        void SetText(string text);

        /// <summary>One copy in two forms: the text, and the same content formatted (HTML) for the programs that take it.</summary>
        void SetTextAndHtml(string text, string html);

        Task<StringCollection> GetFileDropListAsync();

        Task SetFileDropListAsync(StringCollection fileDropList);

        Task<IClipboardImage> GetImageAsync();
    }

    public interface IClipboardImage
    {
        void SaveAsPng(string path);

        byte[] GetBytes();
    }
}
