using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.ViewModels
{
    public sealed partial class AppViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public EditorViewModel EditorViewModel { get; }

        public FileViewModel FileViewModel { get; }

        public FloatViewModel FloatViewModel { get; }

        public FormatViewModel FormatViewModel { get; }

        public ParagraphViewModel ParagraphViewModel { get; }

        public SettingsViewModel SettingsViewModel { get; }

        public UIViewModel UIViewModel { get; }

        public IReadOnlyList<Frame> FrameStack { get; set; } = new List<Frame>();

        public Command<Unit> GoBackCommand { get; } = new(false);

        public Command<string> NavigateCommand { get; } = new();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public string[] CommandLineArgs { get; set; } = Environment.GetCommandLineArgs();

        public IntPtr MainWindow { get; set; }

        public XamlRoot XamlRoot { get; set; }

        private static readonly List<WeakReference<AppViewModel>> instances = new();

        public AppViewModel(
            IServiceProvider serviceProvider,
            EditorViewModel editorViewModel,
            FileViewModel fileViewModel,
            FloatViewModel floatViewModel,
            FormatViewModel formatViewModel,
            ParagraphViewModel paragraphViewModel,
            SettingsViewModel settingsViewModel,
            UIViewModel uiViewModel)
        {
            ServiceProvider = serviceProvider;
            EditorViewModel = editorViewModel;
            FileViewModel = fileViewModel;
            FloatViewModel = floatViewModel;
            FormatViewModel = formatViewModel;
            ParagraphViewModel = paragraphViewModel;
            SettingsViewModel = settingsViewModel;
            UIViewModel = uiViewModel;
            GoBackCommand.OnExecute.Subscribe(_ => GoBack());
            lock (instances) instances.Add(new(this));
        }

        public void GoBack()
        {
            FrameStack.Where(x => x.CanGoBack).Last().GoBack();
        }

        public void Dispose()
        {
            lock (instances) instances.RemoveAll(x => !x.TryGetTarget(out var target) || target == this);
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
