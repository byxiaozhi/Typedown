﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Core.Enums;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
    }
}