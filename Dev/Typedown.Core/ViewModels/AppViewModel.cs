using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.ViewModels
{
    public sealed partial class AppViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public EditorViewModel EditorViewModel => ServiceProvider.GetService<EditorViewModel>();

        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();

        public FloatViewModel FloatViewModel => ServiceProvider.GetService<FloatViewModel>();

        public FormatViewModel FormatViewModel => ServiceProvider.GetService<FormatViewModel>();

        public ParagraphViewModel ParagraphViewModel => ServiceProvider.GetService<ParagraphViewModel>();

        public SettingsViewModel SettingsViewModel => ServiceProvider.GetService<SettingsViewModel>();

        public UIViewModel UIViewModel => ServiceProvider.GetService<UIViewModel>();

        public IReadOnlyList<Frame> FrameStack { get; set; } = new List<Frame>();

        public Command<Unit> GoBackCommand { get; } = new(false);

        public Command<string> NavigateCommand { get; } = new();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public string[] CommandLineArgs { get; set; } = Environment.GetCommandLineArgs();

        public IntPtr MainWindow { get; set; }

        public XamlRoot XamlRoot { get; set; }

        private static readonly List<WeakReference<AppViewModel>> instances = new();

        public AppViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            GoBackCommand.OnExecute.Subscribe(_ => GoBack());
            lock (instances) 
                instances.Add(new(this));
        }

        public void GoBack()
        {
            FrameStack.Where(x => x.CanGoBack).Last().GoBack();
        }

        public void Dispose()
        {
            lock (instances) 
                instances.RemoveAll(x => !x.TryGetTarget(out var target) || target == this);
        }

        ~AppViewModel()
        {
            Dispose();
        }

        public static List<AppViewModel> GetInstances()
        {
            lock (instances)
                return instances.Select(x => x.TryGetTarget(out var val) ? val : null).Where(x => x != null).ToList();
        }
    }
}
