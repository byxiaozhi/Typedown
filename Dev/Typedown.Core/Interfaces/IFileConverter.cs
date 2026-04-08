using System.IO;
using System.Threading.Tasks;

namespace Typedown.Core.Interfaces
{
    public interface IFileConverter
    {
        Task<MemoryStream> HtmlToPdf(string html);
    }
}
