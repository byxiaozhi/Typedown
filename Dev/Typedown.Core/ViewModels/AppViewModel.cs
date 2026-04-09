using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;

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

        public Command<Unit> GoBackCommand { get; } = new(false);

        public Command<string> NavigateCommand { get; } = new();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public string[] CommandLineArgs { get; set; } = Environment.GetCommandLineArgs();

        public IReadOnlyList<Typedown.Core.Controls.GlobalFrame> FrameStack { get; set; } = new List<Typedown.Core.Controls.GlobalFrame>();

        public IntPtr MainWindow { get; set; }

        /// <summary>
        /// Gets the absolute path for an image, relative to the current file's directory.
        /// </summary>
        public string GetImageAbsolutePath(string relativePath)
        {
            var basePath = FileViewModel?.ImageBasePath;
            if (string.IsNullOrEmpty(basePath))
                return relativePath;
            if (System.IO.Path.IsPathRooted(relativePath))
                return relativePath;
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(basePath, relativePath));
        }

        private static readonly List<WeakReference<AppViewModel>> instances = new();

        private readonly CompositeDisposable disposables = new();

        public AppViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            lock (instances)
                instances.Add(new(this));
        }

        public void Dispose()
        {
            lock (instances)
                instances.RemoveAll(x => !x.TryGetTarget(out var target) || target == this);
            disposables.Dispose();
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
