using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
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

        string GetText(TextDataFormat format);

        void SetText(string text, TextDataFormat format);

        void SetText(string text);

        StringCollection GetFileDropList();

        void SetFileDropList(StringCollection fileDropList);

        byte[] GetImage();
    }
}
