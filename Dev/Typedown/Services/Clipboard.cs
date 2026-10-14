using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Utilities;

namespace Typedown.Services
{
    public class Clipboard : IClipboard
    {
        public bool ContainsText(TextDataFormat format)
        {
            var view = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            return format switch
            {
                TextDataFormat.Text or TextDataFormat.UnicodeText => view.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text),
                TextDataFormat.Html => view.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Html),
                TextDataFormat.Rtf => view.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Rtf),
                _ => false,
            };
        }

        public async Task<string> GetTextAsync(TextDataFormat format)
        {
            if (!ContainsText(format))
            {
                return "";
            }
            var view = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            return format switch
            {
                TextDataFormat.Text or TextDataFormat.UnicodeText => await view.GetTextAsync(),
                TextDataFormat.Html => await view.GetHtmlFormatAsync(),
                TextDataFormat.Rtf => await view.GetRtfAsync(),
                _ => throw new NotImplementedException(),
            };
        }

        public async Task SetFileDropListAsync(StringCollection fileDropList)
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            var storageItems = new List<global::Windows.Storage.IStorageItem>();
            foreach (string filePath in fileDropList)
            {
                var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                storageItems.Add(file);
            }
            dataPackage.SetStorageItems(storageItems);
            SetContent(dataPackage);
        }

        public async Task<StringCollection> GetFileDropListAsync()
        {
            var view = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (view.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var storageItems = await view.GetStorageItemsAsync();
                var fileDropList = new StringCollection();
                foreach (var item in storageItems)
                {
                    fileDropList.Add(item.Path);
                }
                return fileDropList;
            }
            return new StringCollection();
        }

        public async Task<IClipboardImage> GetImageAsync()
        {
            var view = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (view.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
            {
                var bitmapStreamRef = await view.GetBitmapAsync();
                return new ClipboardImage(bitmapStreamRef);
            }
            return null;
        }

        /// <summary>
        /// The clipboard written, tried again a few times while another program holds it (CLIPBRD_E_CANT_OPEN: a clipboard
        /// manager, a remote desktop session reading it): Copy and Cut did nothing then. Throws if it stays held.
        /// </summary>
        private static void SetContent(global::Windows.ApplicationModel.DataTransfer.DataPackage dataPackage)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                    return;
                }
                catch (System.Runtime.InteropServices.COMException ex) when (attempt < 6)
                {
                    Core.Utilities.Log.Debug($"clipboard: held by another program ({ex.HResult:x8}), attempt {attempt}");
                    System.Threading.Thread.Sleep(40 * attempt);
                }
            }
        }

        public void SetTextAndHtml(string text, string html)
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            if (!string.IsNullOrEmpty(text)) dataPackage.SetText(text);
            // CF_HTML: the fragment with the header Word and the browsers read it by.
            if (!string.IsNullOrEmpty(html)) dataPackage.SetHtmlFormat(global::Windows.ApplicationModel.DataTransfer.HtmlFormatHelper.CreateHtmlFormat(html));
            SetContent(dataPackage);
        }

        public void SetText(string text, TextDataFormat format)
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            switch (format)
            {
                case TextDataFormat.Text:
                case TextDataFormat.UnicodeText:
                    dataPackage.SetText(text);
                    break;
                case TextDataFormat.Html:
                    dataPackage.SetHtmlFormat(text);
                    break;
                case TextDataFormat.Rtf:
                    dataPackage.SetRtf(text);
                    break;
                default:
                    dataPackage.SetText(text);
                    break;
            }
            SetContent(dataPackage);
        }

        public void SetText(string text)
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(text);
            SetContent(dataPackage);
        }
    }
}