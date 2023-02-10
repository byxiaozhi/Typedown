using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Models;

namespace Typedown.Core.Interfaces
{
    public interface IFileConverter
    {
        Task<MemoryStream> HtmlToPdf(string html, PdfPrintSettings settings = null);
    }
}
