using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Typedown.Universal.Controls;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.ViewModels
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

        private async void InsertTable()
        {
            var result = await InsertTableDialog.OpenInsertTableDialog(ViewModel.XamlRoot);
            if (result != null)
                MarkdownEditor?.PostMessage("InsertTable", new { rows = result.Rows, columns = result.Columns });
        }

        public async Task<object> ResizeTable()
        {
            var result = await InsertTableDialog.OpenResizeTableDialog(ViewModel.XamlRoot);
            return result != null ? new { rows = result.Rows, columns = result.Columns } : null;
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
