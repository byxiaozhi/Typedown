using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml;

namespace Typedown.Universal.ViewModels
{
    public class AppViewModel : ObservableObject
    {
        public IServiceProvider ServiceProvider { get; }

        public EditorViewModel EditorViewModel { get; }

        public FileViewModel FileViewModel { get; }

        public FloatViewModel FloatViewModel { get; }

        public FormatViewModel FormatViewModel { get; }

        public ParagraphViewModel ParagraphViewModel { get; }

        public SettingsViewModel SettingsViewModel { get; }

        public IReadOnlyList<string> NavPagePath { get; set; } = new List<string>();

        public Command<Unit> GoBackCommand { get; } = new(false);

        public Command<string> NavigateCommand { get; } = new();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public IntPtr MainWindow { get; set; }

        public XamlRoot XamlRoot { get; set; }

        public AppViewModel(
            IServiceProvider serviceProvider,
            EditorViewModel editorViewModel,
            FileViewModel fileViewModel,
            FloatViewModel floatViewModel,
            FormatViewModel formatViewModel,
            ParagraphViewModel paragraphViewModel,
            SettingsViewModel settingsViewModel)
        {
            ServiceProvider = serviceProvider;
            EditorViewModel = editorViewModel;
            FileViewModel = fileViewModel;
            FloatViewModel = floatViewModel;
            FormatViewModel = formatViewModel;
            ParagraphViewModel = paragraphViewModel;
            SettingsViewModel = settingsViewModel;

            NavigateCommand.OnExecute.Subscribe(Navigate);
            GoBackCommand.OnExecute.Subscribe(_ => GoBack());
            this.WhenPropertyChanged(nameof(NavPagePath)).Subscribe(_ => UpdateCanGoBack());
        }

        public void Navigate(string path)
        {
            NavPagePath = path.Trim('/').Split("/").ToList();
        }

        public void GoBack()
        {
            NavPagePath = NavPagePath.Take(Math.Max(0, NavPagePath.Count - 1)).ToList();
        }

        public void UpdateCanGoBack()
        {
            GoBackCommand.IsExecutable = NavPagePath.Any();
        }
    }
}
