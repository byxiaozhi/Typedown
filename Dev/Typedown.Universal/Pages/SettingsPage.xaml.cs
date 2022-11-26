using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using Typedown.Universal.Pages.SettingPages;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Universal.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public ObservableCollection<SettingsBreadcrumbBarItem> BreadcrumbBarItems { get; } = new();

        private readonly CompositeDisposable disposables = new();

        public SettingsPage()
        {
            InitializeComponent();
            ContentFrame.Navigated += OnNavigated;
        }

        private void OnNavigationViewSelectionChanged(muxc.NavigationView sender, muxc.NavigationViewSelectionChangedEventArgs args)
        {
            var pageName = (sender.SelectedItem as muxc.NavigationViewItem).Tag as string;
            var pageType = Route.GetSettingsPageType(pageName);
            if (pageType != ContentFrame.SourcePageType)
            {
                var transition = Settings.AnimationEnable ? args.RecommendedNavigationTransitionInfo : new SuppressNavigationTransitionInfo();
                BreadcrumbBarItems.Clear();
                ContentFrame.Navigate(Route.GetSettingsPageType(pageName), null, transition);
                ContentFrame.BackStack.Clear();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var pageType = Route.GetSettingsPageType(e.Parameter as string) ?? typeof(GeneralPage);
            ContentFrame.Navigate(pageType);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            VisualStateManager.GoToState(this, ActualWidth.GetBreakPointValue("Large", "Medium", "Small"), false);
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            var item = NavigationView.MenuItems.OfType<muxc.NavigationViewItem>().Where(x => Route.GetSettingsPageType(x.Tag as string) == ContentFrame.SourcePageType).FirstOrDefault();
            if (item != null) NavigationView.SelectedItem = item;
            switch (e.NavigationMode)
            {
                case NavigationMode.Forward:
                case NavigationMode.New:
                    BreadcrumbBarItems.Add(new(e.Content as Page));
                    break;
                case NavigationMode.Back:
                    BreadcrumbBarItems.RemoveAt(BreadcrumbBarItems.Count - 1);
                    break;
            }
        }

        private void Navigate(string args)
        {
            var path = args?.Split('?')[0].TrimStart('/').Split('/');
            if (path != null && path.Length > 1)
            {
                var type = Route.GetSettingsPageType(path[1]);
                var query = args.Contains("?") ? args.Substring(args.IndexOf("?") + 1) : "";
                if (type != Frame.SourcePageType)
                    ContentFrame.Navigate(type, query, GetTransition());
            }
        }

        public NavigationTransitionInfo GetTransition() => Settings?.AnimationEnable ?? false ? new SlideNavigationTransitionInfo()
        {
            Effect = SlideNavigationTransitionEffect.FromRight
        } : new SuppressNavigationTransitionInfo();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            disposables.Add(ViewModel.NavigateCommand.OnExecute.Subscribe(args => Navigate(args)));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }

        private void OnBreadcrumbBarItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var count = BreadcrumbBarItems.Count - args.Index - 1;
            for (int i = 0; i < count; i++)
                ContentFrame.GoBack();
        }

        public void SetPageTitle(Page page, string title)
        {
            var item = BreadcrumbBarItems.Where(x => x.Page == page).FirstOrDefault();
            if (item != null)
                item.Title = title;
        }
    }

    public partial class SettingsBreadcrumbBarItem : INotifyPropertyChanged
    {
        public Page Page { get; }

        public Type PageType { get; }

        public string Title { get; set; }

        public SettingsBreadcrumbBarItem(Page page, string title = null)
        {
            Page = page;
            PageType = Page.GetType();
            Title = title ?? Locale.GetTypeString(PageType);
        }
    }
}
