using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Typedown.Universal.Enums;
using Typedown.Universal.Models;

namespace Typedown.Universal.Interfaces
{
    public interface IFileExport
    {
        ObservableCollection<ExportConfig> ExportConfigs { get; }

        Task<ExportConfig> AddExportConfig(string name = null, ExportType type = 0);

        Task RemoveExportConfig(int id);

        Task<bool> SaveExportConfig(ExportConfig config);

        Task<ExportConfig> GetExportConfig(int id);

        Task UpdateExportConfigs();

        Task HtmlToPdf(string basePath, string htmlString, string savePath);

        Task Print(string basePath, string htmlString, string documentName = null);
    }
}
