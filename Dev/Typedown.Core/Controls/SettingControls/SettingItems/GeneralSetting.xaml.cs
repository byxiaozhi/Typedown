using System;
using Typedown.Core.Enums;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems
{
    public sealed partial class GeneralSetting : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public GeneralSetting()
        {
            InitializeComponent();
        }

        public static bool IsStartupOpenFolderItemLoad(FolderStartupAction action)
        {
            return action == FolderStartupAction.OpenFolder;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
