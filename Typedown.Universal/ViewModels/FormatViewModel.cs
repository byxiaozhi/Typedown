using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.ViewModels
{
    public sealed partial class FormatViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public AppViewModel ViewModel => ServiceProvider.GetService<AppViewModel>();

        public EditorViewModel EditorViewModel => ServiceProvider.GetService<EditorViewModel>();

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public FormatState FormatState { get; private set; } = new();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public Command<string> SetFormatCommand { get; } = new();

        private readonly CompositeDisposable disposables = new();

        public FormatViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            EventCenter.GetObservable<EditorEventArgs>("SelectionFormats").Subscribe(x => OnSelectionFormats(x.Args));
            SetFormatCommand.OnExecute.Subscribe(x => SetFormatFun(x));
        }

        public void OnSelectionFormats(JToken arg)
        {
            FormatState = new(arg["formats"]?.ToObject<List<FormatState.SelectionFormat>>());
            EditorViewModel.UpdateMuyaSelected();
        }

        private void SetFormatFun(string type)
        {
            MarkdownEditor?.PostMessage("Format", type);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
