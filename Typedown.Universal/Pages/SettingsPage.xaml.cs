using System;
using System.Linq;
using System.Reactive.Disposables;
using Typedown.Universal.Pages.SettingPages;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Universal.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public SettingsPage()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            disposables.Add(ViewModel.WhenPropertyChanged(nameof(ViewModel.NavPagePath)).Subscribe(_ => Navigate()));
            Navigate();
        }

        private void OnUnLoaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }

        private void Navigate()
        {
            if (ViewModel.NavPagePath.Count > 1)
            {
                var pageName = ViewModel.NavPagePath[1];
                var type = GetPageType(pageName);
                if (type != ContentFrame.SourcePageType)
                {
                    ContentFrame.Navigate(type);
                    NavigationView.SelectedItem = NavigationView.MenuItems.Cast<muxc.NavigationViewItem>().Where(x => x.Tag as string == pageName).FirstOrDefault();
                }
            }
            else
            {
                ViewModel.GoBack();
            }
        }

        private void OnNavigationViewSelectionChanged(muxc.NavigationView sender, muxc.NavigationViewSelectionChangedEventArgs args)
        {
            var pageName = (sender.SelectedItem as muxc.NavigationViewItem).Tag as string;
            ViewModel.Navigate($"/Settings/{pageName}");
        }

        public Type GetPageType(string pageName) => pageName switch
        {
            "View" => typeof(ViewPage),
            "Editor" => typeof(EditorPage),
            "Image" => typeof(ImagePage),
            "Export" => typeof(ExportPage),
            "About" => typeof(AboutPage),
            _ => typeof(GeneralPage),
        };

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ActualWidth >= 1008)
                VisualStateManager.GoToState(this, "Large", false);
            else if (ActualWidth >= 641)
                VisualStateManager.GoToState(this, "Medium", false);
            else
                VisualStateManager.GoToState(this, "Small", false);
        }
    }
}
