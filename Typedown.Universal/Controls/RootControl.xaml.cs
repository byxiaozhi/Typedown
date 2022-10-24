using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Typedown.Universal.Pages;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Typedown.Universal.Controls
{
    public sealed partial class RootControl : UserControl
    {
        public Command<string> NavigateCommand { get; } = new();

        public Command<Unit> BackCommand { get; } = new(false);

        private readonly ObservableCollection<Type> history = new();

        public RootControl()
        {
            InitializeComponent();
            NavigateCommand.OnExecute.Select(GetPageType).Subscribe(history.Add);
            BackCommand.OnExecute.Select(_ => history.Count - 1).Subscribe(history.RemoveAt);
            var historyObservable = history.GetCollectionObservable();
            historyObservable.Subscribe(_ => BackCommand.IsExecutable = history.Count > 1);
            historyObservable.Select(x => x.EventArgs.Action).Select(GetTransition).Subscribe(t => Frame.Navigate(history.Last(), null, t));
            history.Add(typeof(MainPage));
        }

        public Type GetPageType(string pageName) => pageName switch
        {
            "Settings" => typeof(SettingsPage),
            _ => typeof(MainPage)
        };

        public SlideNavigationTransitionInfo GetTransition(NotifyCollectionChangedAction action) => new SlideNavigationTransitionInfo()
        {
            Effect = action switch
            {
                NotifyCollectionChangedAction.Add => SlideNavigationTransitionEffect.FromRight,
                _ => SlideNavigationTransitionEffect.FromLeft
            }
        };
    }
}
