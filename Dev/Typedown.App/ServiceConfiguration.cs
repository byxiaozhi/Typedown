using Microsoft.Extensions.DependencyInjection;
using System;
using Typedown.App.Services;
using Typedown.Core.Interfaces;
using Typedown.Core.Services;
using Typedown.Core.ViewModels;

namespace Typedown.App;

/// <summary>
/// Central DI container. Registers all Core services and Avalonia platform implementations.
/// </summary>
public static class ServiceConfiguration
{
    public static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // ═══════════ Platform Services (Avalonia) ═══════════
        services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
        services.AddSingleton<IDialogService, AvaloniaDialogService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton<IWindowService, AvaloniaWindowService>();
        services.AddSingleton<IClipboard, AvaloniaClipboardService>();
        services.AddSingleton<IFileOperation, FileOperationService>();
        services.AddSingleton<IKeyboardAccelerator, AvaloniaKeyboardAccelerator>();
        services.AddSingleton<IFileExport, FileExportService>();
        services.AddSingleton<IFileConverter, FileConverterService>();
        services.AddSingleton<IPowerShellService, PowerShellService>();

        // MarkdownEditor — registered as both concrete + interface
        services.AddSingleton<AvaloniaMarkdownEditor>();
        services.AddSingleton<IMarkdownEditor>(sp => sp.GetRequiredService<AvaloniaMarkdownEditor>());

        // ═══════════ Core ViewModels ═══════════
        services.AddSingleton<AppViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<FileViewModel>();
        services.AddSingleton<FloatViewModel>();
        services.AddSingleton<FormatViewModel>();
        services.AddSingleton<ParagraphViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<UIViewModel>();

        // ═══════════ Core Services ═══════════
        services.AddSingleton<EventCenter>();
        services.AddSingleton<RemoteInvoke>();
        services.AddSingleton<AccessHistory>();
        services.AddSingleton<AutoBackup>();
        services.AddSingleton<ImageAction>();
        services.AddSingleton<ImageUpload>();

        return services.BuildServiceProvider();
    }
}
