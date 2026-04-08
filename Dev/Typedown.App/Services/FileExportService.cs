using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;

namespace Typedown.App.Services;

/// <summary>
/// Stub IFileExport for Avalonia. Export/print functionality will be implemented later.
/// </summary>
public class FileExportService : IFileExport
{
    public ObservableCollection<ExportConfig> ExportConfigs { get; } = new();

    public Task<ExportConfig> AddExportConfig(string name = null, ExportType type = 0)
        => Task.FromResult(new ExportConfig());

    public Task RemoveExportConfig(int id)
        => Task.CompletedTask;

    public Task<bool> SaveExportConfig(ExportConfig config)
        => Task.FromResult(true);

    public Task<ExportConfig> GetExportConfig(int id)
        => Task.FromResult<ExportConfig>(null);

    public Task UpdateExportConfigs()
        => Task.CompletedTask;

    public Task Print(string basePath, string html, string documentName = null)
        => Task.CompletedTask;
}
