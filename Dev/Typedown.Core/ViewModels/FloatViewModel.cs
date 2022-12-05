using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Controls.FloatControls;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Windows.Foundation;

namespace Typedown.Core.ViewModels
{
    public sealed partial class FloatViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public enum FindReplaceDialogState { None, Search, Replace }

        public FindReplaceDialogState FindReplaceDialogOpen { get; set; }

        public Command<FindReplaceDialogState> SearchCommand { get; } = new();

        public AppViewModel ViewModel => ServiceProvider.GetService<AppViewModel>();

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        private readonly CompositeDisposable disposables = new();

        public FloatViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            EventCenter.GetObservable<EditorEventArgs>("OpenFrontMenu").Subscribe(x => OnOpenFrontMenu(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenFormatPicker").Subscribe(x => OnOpenFormatPicker(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenImageSelector").Subscribe(x => OnOpenImageSelector(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenTableTools").Subscribe(x => OnOpenTableTools(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenImageToolbar").Subscribe(x => OnOpenImageToolbar(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("OpenToolTip").Subscribe(x => OnOpenToolTip(x.Args));
            this.WhenPropertyChanged(nameof(FindReplaceDialogOpen)).Subscribe(_ => OnFindReplaceDialogOpenChange(FindReplaceDialogOpen));
            SearchCommand.OnExecute.Subscribe(Search);
        }

        public void Search(FindReplaceDialogState open)
        {
            FindReplaceDialogOpen = open;
            var text = ViewModel.EditorViewModel.SelectionText;
            ViewModel.EditorViewModel.SearchValue = text;
            if (!string.IsNullOrEmpty(text))
                ViewModel.EditorViewModel.OnSearch();
        }

        public void OnFindReplaceDialogOpenChange(FindReplaceDialogState open)
        {
            MarkdownEditor?.PostMessage("SearchOpenChange", new { open = (int)open });
        }

        public void OnOpenImageToolbar(JToken args)
        {
            var imageToolbar = ServiceProvider.GetService<ImageToolbar>();
            var rect = args["boundingClientRect"].ToObject<Rect>();
            var attrs = args["attrs"];
            imageToolbar.Open(rect, attrs);
        }

        public void OnOpenFrontMenu(JToken args)
        {
            var frontMenu = ServiceProvider.GetService<FrontMenu>();
            var rect = args["boundingClientRect"].ToObject<Rect>();
            frontMenu.Open(rect);
        }

        public void OnOpenFormatPicker(JToken args)
        {
            throw new NotImplementedException();
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

        private ToolTip openedToolTip;

        public void OnOpenToolTip(JToken args)
        {
            openedToolTip?.Hide();
            openedToolTip = null;
            if (args["open"].ToObject<bool>())
            {
                openedToolTip = ServiceProvider.GetService<ToolTip>();
                var name = args["tooltip"].ToString();
                var text = Locale.GetString(name) ?? name;
                openedToolTip.Open(text);
            }
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
