using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Controls.FloatControls;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Windows.Foundation;

namespace Typedown.Universal.ViewModels
{
    public class FloatViewModel : ObservableObject
    {
        public IServiceProvider ServiceProvider { get; }

        public int SearchOpen { get; set; }

        public Command<int> SearchCommand { get; } = new();

        public AppViewModel ViewModel => ServiceProvider.GetService<AppViewModel>();

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public FloatViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            EventCenter.GetObservable<EditorEventArgs>("OpenFrontMenu").Subscribe(x => OnOpenFrontMenu(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenFormatPicker").Subscribe(x => OnOpenFormatPicker(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenImageSelector").Subscribe(x => OnOpenImageSelector(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenTableTools").Subscribe(x => OnOpenTableTools(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenImageToolbar").Subscribe(x => OnOpenImageToolbar(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenToolTip").Subscribe(x => OnOpenToolTip(x.Args));
            this.WhenPropertyChanged(nameof(SearchOpen)).Subscribe(_ => OnSearchOpenChange(SearchOpen));
            SearchCommand.OnExecute.Subscribe(Search);
        }

        public void Search(int open)
        {
            SearchOpen = open;
            var text = ViewModel.EditorViewModel.SelectionText;
            ViewModel.EditorViewModel.SearchValue = text;
            if (!string.IsNullOrEmpty(text))
                ViewModel.EditorViewModel.OnSearch();
        }

        public void OnSearchOpenChange(int open)
        {
            MarkdownEditor?.PostMessage("SearchOpenChange", new { open });
        }

        public void OnOpenImageToolbar(JToken arg)
        {
            //if (!ImageToolbarOpen)
            //{
            //    ImageToolbarArg = arg;
            //    ImageToolbarOpen = true;
            //}
        }

        public void OnOpenFrontMenu(JToken args)
        {
            var frontMenu = ServiceProvider.GetService<FrontMenu>();
            var rect = args["boundingClientRect"].ToObject<Rect>();
            frontMenu.Open(rect);
        }

        public void OnOpenFormatPicker(JToken arg)
        {
            //FormatPickerArg = arg;
            //FormatPickerOpen = true;
        }

        public void OnOpenImageSelector(JToken args)
        {
            var selector = ServiceProvider.GetService<ImageSelector>();
            var rect = args["boundingClientRect"].ToObject<Rect>();
            var info = args["imageInfo"];
            selector.Open(rect, info);
        }

        public void OnOpenTableTools(JToken args)
        {
            var tableTools = ServiceProvider.GetService<TableTools>();
            var rect = args["boundingClientRect"].ToObject<Rect>();
            var type = args["tableInfo"]["barType"].ToString();
            tableTools.Open(rect, type);
        }

        public void OnOpenToolTip(JToken arg)
        {
            //ToolTipArg = arg;
            //ToolTipOpen = arg["open"].ToObject<bool>();
        }
    }
}
