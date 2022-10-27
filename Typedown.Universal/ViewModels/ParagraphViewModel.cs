using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Controls;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.ViewModels
{
    public class ParagraphViewModel : ObservableObject
    {
        public IServiceProvider ServiceProvider { get; }

        readonly ResourceLoader dialogMessages = ResourceLoader.GetForViewIndependentUse("DialogMessages");

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public AppViewModel ViewModel => ServiceProvider.GetService<AppViewModel>();

        public RemoteInvoke RemoteInvoke => ServiceProvider.GetService<RemoteInvoke>();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public Command<string> UpdateParagraphCommand { get; } = new();

        public Command<string> InsertParagraphCommand { get; } = new();

        public Command<Unit> DeleteParagraphCommand { get; } = new();

        public Command<Unit> DuplicateCommand { get; } = new();

        public Command<Unit> InsertTableCommand { get; } = new();

        public ParagraphViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            RemoteInvoke.Handle("ResizeTable", OnResizeTable);
            UpdateParagraphCommand.OnExecute.Subscribe(x => UpdateParagraph(x));
            InsertParagraphCommand.OnExecute.Subscribe(x => InsertParagraph(x));
            DeleteParagraphCommand.OnExecute.Subscribe(_ => DeleteParagraph());
            DuplicateCommand.OnExecute.Subscribe(_ => Duplicate());
            InsertTableCommand.OnExecute.Subscribe(_ => InsertTable());
        }

        private void UpdateParagraph(string type)
        {
            MarkdownEditor?.PostMessage("UpdateParagraph", type);
        }

        private void InsertParagraph(string type)
        {
            MarkdownEditor?.PostMessage("InsertParagraph", type);
        }

        private void DeleteParagraph()
        {
            MarkdownEditor?.PostMessage("DeleteParagraph", null);
        }

        private void Duplicate()
        {
            MarkdownEditor?.PostMessage("Duplicate", null);
        }

        private async void InsertTable()
        {
            throw new NotImplementedException();
            //var content = new InsertTableDialog();
            //var result = await AppContentDialog.Create(
            //    dialogMessages.GetString("InsertTableTitle"),
            //    content,
            //    dialogMessages.GetString("Cancel"),
            //    dialogMessages.GetString("Confirm")).ShowAsync(ViewModel.XamlRoot);
            //if (result == ContentDialogResult.Primary)
            //{
            //    Transport?.PostMessage("InsertTable", new { rows = content.Rows, columns = content.Columns });
            //}
        }

        public async Task<object> OnResizeTable(JToken arg)
        {
            throw new NotImplementedException();
            //var content = new InsertTableDialog();
            //content.Rows = arg["rows"].ToObject<int>();
            //content.Columns = arg["columns"].ToObject<int>();
            //var result = await AppContentDialog.Create(
            //    dialogMessages.GetString("ResizeTableTitle"),
            //    content,
            //    dialogMessages.GetString("Cancel"),
            //    dialogMessages.GetString("Confirm")).ShowAsync(ViewModel.XamlRoot);
            //if (result == ContentDialogResult.Primary)
            //{
            //    return new { rows = content.Rows, columns = content.Columns };
            //}
            //return null;
        }
    }
}
