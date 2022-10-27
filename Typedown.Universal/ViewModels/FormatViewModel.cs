using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.ViewModels
{
    public class FormatViewModel : ObservableObject
    {
        public IServiceProvider ServiceProvider { get; }

        public AppViewModel ViewModel => ServiceProvider.GetService<AppViewModel>();

        public EditorViewModel EditorViewModel => ServiceProvider.GetService<EditorViewModel>();

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public JToken SelectionFormats { get; set; }

        public FormatState FormatState { get; set; } = new FormatState();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public Command<string> SetFormatCommand { get; } = new();

        public FormatViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            EventCenter.GetObservable<EditorEventArgs>("SelectionFormats").Subscribe(x => OnSelectionFormats(x.Args));
            SetFormatCommand.OnExecute.Subscribe(x => SetFormatFun(x));
        }

        public void OnSelectionFormats(JToken arg)
        {
            SelectionFormats = arg["formats"];
            List<JToken> array = SelectionFormats.ToList();
            var typeSet = new HashSet<string>();
            var tagSet = new HashSet<string>();
            array.Where(x => x["type"] != null).ToList().ForEach(x => typeSet.Add(x["type"].ToString()));
            array.Where(x => x["tag"] != null).ToList().ForEach(x => tagSet.Add(x["tag"].ToString()));
            FormatState = new(typeSet, tagSet);
            EditorViewModel.UpdateMuyaSelected();
        }

        private void SetFormatFun(string type)
        {
            MarkdownEditor?.PostMessage("Format", type);
        }
    }
}
