using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Universal.Enums;
using Typedown.Universal.Pages;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Typedown.Universal.Controls
{
    public sealed partial class RootControl : UserControl
    {
        public record PageData(Type Page, string Param);

        public AppViewModel AppViewModel => this.GetService<AppViewModel>();

        public SettingsViewModel SettingsViewModel => this.GetService<SettingsViewModel>();

        private readonly ObservableCollection<PageData> history = new();

        private readonly CompositeDisposable disposables = new();

        public RootControl()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            AppViewModel.XamlRoot = XamlRoot;
            disposables.Add(AppViewModel.NavigateCommand.OnExecute.Select(GetPageType).Subscribe(history.Add));
            disposables.Add(AppViewModel.GoBackCommand.OnExecute.Select(_ => history.Count - 1).Subscribe(history.RemoveAt));
            var historyObservable = history.GetCollectionObservable();
            disposables.Add(historyObservable.Subscribe(_ => AppViewModel.GoBackCommand.IsExecutable = history.Count > 1));
            disposables.Add(historyObservable.Select(x => x.EventArgs.Action).Select(GetTransition).Subscribe(t => Frame.Navigate(history.Last().Page, history.Last().Param, t)));
            history.Add(new(typeof(MainPage), null));
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            disposables.Clear();
            history.Clear();
        }

        public PageData GetPageType(string args)
        {
            var arr = args.Split("/");
            var page = arr[0] switch
            {
                "Settings" => typeof(SettingsPage),
                _ => typeof(MainPage)
            };
            return new(page, string.Join("/", arr.Skip(1)));
        }

        public NavigationTransitionInfo GetTransition(NotifyCollectionChangedAction action) => SettingsViewModel?.AnimationEnable ?? false ? new SlideNavigationTransitionInfo()
        {
            Effect = action switch
            {
                NotifyCollectionChangedAction.Add => SlideNavigationTransitionEffect.FromRight,
                _ => SlideNavigationTransitionEffect.FromLeft
            }
        } : new SuppressNavigationTransitionInfo();

        public static ElementTheme GetTheme(string theme) => theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => App.Current.RequestedTheme == ApplicationTheme.Light ? ElementTheme.Light : ElementTheme.Dark
        };
    }
}
