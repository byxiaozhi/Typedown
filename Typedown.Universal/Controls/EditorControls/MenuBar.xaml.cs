using Microsoft.Extensions.DependencyInjection;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public sealed partial class MenuBar : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;
        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public MenuBar()
        {
            InitializeComponent();
        }

        private void OnMenuBarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ViewModel != null)
                ViewModel.UIViewModel.MenuBarWidth = (sender as FrameworkElement).ActualWidth;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (TitleStackPanel != null)
            {
                if (ActualWidth / 2 > MenuBarControl.ActualWidth + TitleStackPanel.ActualWidth / 2 + 16)
                {
                    TitleStackPanel.Margin = new(0);
                    Grid.SetColumn(TitleStackPanel, 0);
                    Grid.SetColumnSpan(TitleStackPanel, 2);
                }
                else
                {
                    TitleStackPanel.Margin = new(0, 0, 46 * 3, 0);
                    Grid.SetColumn(TitleStackPanel, 1);
                    Grid.SetColumnSpan(TitleStackPanel, 1);
                }
            }
        }
    }
}
