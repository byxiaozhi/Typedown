using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Pages.SettingPages;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public AppViewModel AppViewModel => this.GetService<AppViewModel>();

        public SettingsViewModel SettingsViewModel => this.GetService<SettingsViewModel>();

        public SettingsPage()
        {
            InitializeComponent();
        }

        private void OnNavigationViewSelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            var item = (sender.SelectedItem as Microsoft.UI.Xaml.Controls.NavigationViewItem);
            var first = ContentFrame.SourcePageType == null;
            var enable = (SettingsViewModel?.AnimationEnable ?? false) && !first;
            var transition = enable ? args.RecommendedNavigationTransitionInfo : new SuppressNavigationTransitionInfo();
            ContentFrame.Navigate(GetPageType(item?.Tag as string), null, transition);
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
            {
                VisualStateManager.GoToState(this, "Large", false);
            }
            else if (ActualWidth >= 641)
            {
                VisualStateManager.GoToState(this, "Medium", false);
            }
            else
            {
                VisualStateManager.GoToState(this, "Small", false);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            object navigateItem = NavigationView.MenuItems.First();
            if (e.Parameter is string str)
            {
                var item = NavigationView.MenuItems.OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>().Where(x => x.Tag is string tag && tag == str);
                if (item.Any())
                    navigateItem = item.First();
            }
            NavigationView.SelectedItem = navigateItem;
            base.OnNavigatedTo(e);
        }
    }
}
