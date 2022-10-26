using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.ViewModels
{
    public class AppViewModel
    {
        public IServiceProvider ServiceProvider { get; }

        public EditorViewModel EditorViewModel => ServiceProvider.GetService<EditorViewModel>();

        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();

        public FloatViewModel FloatViewModel => ServiceProvider.GetService<FloatViewModel>();

        public FormatViewModel FormatViewModel => ServiceProvider.GetService<FormatViewModel>();

        public ParagraphViewModel ParagraphViewModel => ServiceProvider.GetService<ParagraphViewModel>();

        public SettingsViewModel SettingsViewModel => ServiceProvider.GetService<SettingsViewModel>();
        
        public Command<Unit> GoBackCommand { get; } = new(false);

        public Command<string> NavigateCommand { get; } = new();

        public AppViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
    }
}
