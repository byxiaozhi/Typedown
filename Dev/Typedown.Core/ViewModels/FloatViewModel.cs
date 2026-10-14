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
            Subscribe("OpenFrontMenu", OnOpenFrontMenu);
            // A message from the page must never be able to end the process: each of these draws something, and
            // a failure to draw is worth a line in the log, not a crash.
            Subscribe("OpenFormatPicker", OnOpenFormatPicker);
            Subscribe("OpenImageSelector", OnOpenImageSelector);
            Subscribe("OpenTableTools", OnOpenTableTools);
            Subscribe("OpenImageToolbar", OnOpenImageToolbar);
            Subscribe("OpenToolTip", OnOpenToolTip);
            this.WhenPropertyChanged(nameof(FindReplaceDialogOpen)).Subscribe(_ => OnFindReplaceDialogOpenChange(FindReplaceDialogOpen));
            SearchCommand.OnExecute.Subscribe(Search);
        }

        private void Subscribe(string name, Action<JToken> handler)
        {
            EventCenter.GetObservable<EditorEventArgs>(name).Subscribe(x =>
            {
                try
                {
                    handler(x.Args);
                }
                catch (Exception ex)
                {
                    Log.Debug($"{name}: {ex.GetType().Name}: {ex.Message}");
                }
            });
        }

        /// <summary>Raised every time the search shortcut is pressed, so the bar can take focus even when it
        /// was already open — pressing it again after clicking back into the document used to do nothing.</summary>
        public event Action FocusSearchRequested;

        public void Search(FindReplaceDialogState open)
        {
            FindReplaceDialogOpen = open;
            var text = ViewModel.EditorViewModel.SelectionText;
            ViewModel.EditorViewModel.SearchValue = text;
            if (!string.IsNullOrEmpty(text))
                ViewModel.EditorViewModel.OnSearch();
            FocusSearchRequested?.Invoke();
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

        /// <summary>
        /// The editor asks for the little formatting toolbar when a selection is made — double-clicking a word
        /// is enough. This edition has never drawn one, and the stub that stood here threw, which took the
        /// whole process down. Until there is one, the request is noted and ignored.
        /// </summary>
        public void OnOpenFormatPicker(JToken args)
        {
            Log.Debug("OpenFormatPicker: no formatting toolbar in this edition; ignoring");
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
