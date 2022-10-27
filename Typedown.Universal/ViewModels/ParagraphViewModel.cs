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

        public IMarkdownEditor Transport => ServiceProvider.GetService<IMarkdownEditor>();

        public ParagraphViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public Command<string> UpdateParagraph { get; }

        private void UpdateParagraphFun(string type)
        {
            Transport?.PostMessage("UpdateParagraph", type);
        }

        public Command<string> InsertParagraph { get; }

        private void InsertParagraphFun(string type)
        {
            Transport?.PostMessage("InsertParagraph", type);
        }

        public Command<Unit> DeleteParagraph { get; }

        private void DeleteParagraphFun()
        {
            Transport?.PostMessage("DeleteParagraph", null);
        }

        public Command<Unit> Duplicate { get; }

        private void DuplicateFun()
        {
            Transport?.PostMessage("Duplicate", null);
        }

        public Command<Unit> InsertTable { get; }

        private async void InsertTableFun()
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
