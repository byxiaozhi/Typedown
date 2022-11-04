using Microsoft.Extensions.DependencyInjection;
using Typedown.Services;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Services;
using Typedown.Universal.ViewModels;
using Typedown.Controls;
using Typedown.Universal.Controls.FloatControls;

namespace Typedown
{
    public static class Injection
    {
        public static ServiceProvider ServiceProvider { get; }

        static Injection()
        {
            if (ServiceProvider == null)
            {
                var builder = new ServiceCollection();
                RegisterViewModel(builder);
                RegisterService(builder);
                RegisterComponent(builder);
                ServiceProvider = builder.BuildServiceProvider();
            }
        }

        private static void RegisterViewModel(ServiceCollection builder)
        {
            builder.AddScoped<AppViewModel>();
            builder.AddScoped<EditorViewModel>();
            builder.AddScoped<FileViewModel>();
            builder.AddScoped<FloatViewModel>();
            builder.AddScoped<FormatViewModel>();
            builder.AddScoped<ParagraphViewModel>();
            builder.AddScoped<SettingsViewModel>();
        }

        private static void RegisterService(ServiceCollection builder)
        {
            builder.AddScoped<IClipboard, Clipboard>();
            builder.AddScoped<IFileExport, FileExport>();
            builder.AddScoped<IFileOperation, FileOperation>();
            builder.AddScoped<IWindowService, WindowService>();
            builder.AddScoped<IKeyboardAccelerator, KeyboardAccelerator>();
            builder.AddScoped<Transport>();
            builder.AddScoped<RemoteInvoke>();
            builder.AddScoped<EventCenter>();
            builder.AddScoped<AutoBackup>();
        }

        private static void RegisterComponent(ServiceCollection builder)
        {
            builder.AddScoped<IMarkdownEditor, MarkdownEditor>();
            builder.AddTransient<FrontMenu>();
            builder.AddTransient<TableTools>();
            builder.AddTransient<ImageSelector>();
            builder.AddTransient<ToolTip>();
        }
    }
}
