using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Services;
using Typedown.Core.Utilities;

namespace Typedown.Core.ViewModels
{
    public sealed partial class ParagraphViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public AppViewModel ViewModel => ServiceProvider.GetService<AppViewModel>();

        public RemoteInvoke RemoteInvoke => ServiceProvider.GetService<RemoteInvoke>();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public Command<string> UpdateParagraphCommand { get; } = new();

        public Command<string> InsertParagraphCommand { get; } = new();

        public Command<Unit> DeleteParagraphCommand { get; } = new();

        public Command<Unit> DuplicateCommand { get; } = new();

        public Command<Unit> InsertTableCommand { get; } = new();

        private readonly CompositeDisposable disposables = new();

        public ParagraphViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            RemoteInvoke.Handle("ResizeTable", ResizeTable);
            UpdateParagraphCommand.OnExecute.Subscribe(x => UpdateParagraph(x));
            InsertParagraphCommand.OnExecute.Subscribe(x => InsertParagraph(x));
            DeleteParagraphCommand.OnExecute.Subscribe(_ => DeleteParagraph());
            DuplicateCommand.OnExecute.Subscribe(_ => Duplicate());
            InsertTableCommand.OnExecute.Subscribe(_ => InsertTable());
        }

        private void UpdateParagraph(string type) => MarkdownEditor?.PostMessage("UpdateParagraph", type);

        private void InsertParagraph(string type) => MarkdownEditor?.PostMessage("InsertParagraph", type);

        private void DeleteParagraph() => MarkdownEditor?.PostMessage("DeleteParagraph", null);

        private void Duplicate() => MarkdownEditor?.PostMessage("Duplicate", null);

        private void InsertTable()
        {
            // TODO: Phase 3 — Show Avalonia InsertTableDialog
            MarkdownEditor?.PostMessage("InsertTable", new { rows = 3, columns = 3 });
        }

        public Task<object> ResizeTable()
        {
            // TODO: Phase 3 — Show Avalonia ResizeTableDialog
            return Task.FromResult<object>(null);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
