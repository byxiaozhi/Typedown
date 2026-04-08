using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;

namespace Typedown.App.Services;

/// <summary>
/// Stub IFileConverter for Avalonia. PDF conversion will be integrated later.
/// </summary>
public class FileConverterService : IFileConverter
{
    public Task<MemoryStream> HtmlToPdf(string html)
        => Task.FromResult(new MemoryStream());
}
