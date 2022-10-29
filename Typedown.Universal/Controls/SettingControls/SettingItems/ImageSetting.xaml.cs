using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Enums;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls.SettingControls.SettingItems
{
    public sealed partial class ImageSetting : UserControl
    {
        public AppViewModel AppViewModel => DataContext as AppViewModel;

        public SettingsViewModel SettingsViewModel => AppViewModel?.SettingsViewModel;

        public ImageSetting()
        {
            InitializeComponent();
        }

        public static bool IsCopyImageItemLoad(InsertImageAction action)
        {
            return action == InsertImageAction.CopyToPath;
        }
    }
}
