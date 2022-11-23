using System.Reactive.Disposables;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public sealed partial class MenuBar : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;
        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public MenuBar()
        {
            InitializeComponent();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (TitleStackPanel != null)
            {
                if (ActualWidth / 2 > MenuBarControl.ActualWidth + TitleStackPanel.ActualWidth / 2 + 16)
                {
                    TitleStackPanel.Margin = new(0);
                    Grid.SetColumn(TitleStackPanel, 0);
                    Grid.SetColumnSpan(TitleStackPanel, 3);
                }
                else
                {
                    TitleStackPanel.Margin = new(0, 0, 46 * 3, 0);
                    Grid.SetColumn(TitleStackPanel, 1);
                    Grid.SetColumnSpan(TitleStackPanel, 2);
                }
            }
        }

        private bool CaptionButtonsLoad(bool isCompactMode)
        {
            return isCompactMode && !Config.IsMicaSupported;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Settings.AppCompactMode)
            {
                var uiViewModel = ViewModel.UIViewModel;
                var oldCaptionHeight = uiViewModel.CaptionHeight;
                uiViewModel.CaptionHeight = 41;
                disposables.Add(Disposable.Create(() => uiViewModel.CaptionHeight = oldCaptionHeight));
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Dispose();
        }
    }
}
