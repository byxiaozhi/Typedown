using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Models;

namespace Typedown.Universal.Interfaces
{
    public interface IFileConverter
    {
        Task<MemoryStream> HtmlToPdf(string html, PdfPrintSettings settings = null);
    }
}
