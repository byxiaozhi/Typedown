using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Windows.Foundation;

namespace Typedown.Universal.Models.ExportConfigModels
{
    public class PDFConfigModel : ConfigModel
    {
        public Size PageSize { get; set; } = new();

        public PageMargin Margins { get; set; } = new();

        public string Header { get; set; } = string.Empty;

        public string Footer { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public override async Task Export(IServiceProvider serviceProvider, string html, string filePath)
        {
            var converter = serviceProvider.GetService<IFileConverter>();
            var pdf = await converter.HtmlToPdf(html);
            await File.WriteAllBytesAsync(filePath, pdf.ToArray());
        }
    }

    public partial class PageSize : INotifyPropertyChanged
    {
        public double Width { get; set; }

        public double Height { get; set; }
    }

    public partial class PageMargin : INotifyPropertyChanged
    {
        public double Left { get; set; }

        public double Top { get; set; }

        public double Right { get; set; }

        public double Bottom { get; set; }
    }
}
