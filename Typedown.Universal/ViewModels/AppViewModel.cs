﻿using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml;

namespace Typedown.Universal.ViewModels
{
    public class AppViewModel
    {
        public IServiceProvider ServiceProvider { get; }

        public EditorViewModel EditorViewModel { get; }

        public FileViewModel FileViewModel { get; }

        public FloatViewModel FloatViewModel { get; }

        public FormatViewModel FormatViewModel { get; }

        public ParagraphViewModel ParagraphViewModel { get; }

        public SettingsViewModel SettingsViewModel { get; }

        public Command<Unit> GoBackCommand { get; } = new(false);

        public Command<string> NavigateCommand { get; } = new();

        public IMarkdownEditor Transport => ServiceProvider.GetService<IMarkdownEditor>();

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
        }
    }
}
