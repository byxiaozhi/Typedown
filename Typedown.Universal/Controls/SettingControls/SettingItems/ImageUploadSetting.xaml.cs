﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Enums;
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
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls.SettingControls.SettingItems
{
    public sealed partial class ImageUploadSetting : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public ImageUploadSetting()
        {
            this.InitializeComponent();
        }

        public static FrameworkElement GetUploadSettingItem(ImageUploadMethod method)
        {
            return method switch
            {
                ImageUploadMethod.FTP => new UploadSettingItems.FTPSetting(),
                ImageUploadMethod.Git => new UploadSettingItems.GitSetting(),
                ImageUploadMethod.OSS => new UploadSettingItems.OSSSetting(),
                ImageUploadMethod.SCP => new UploadSettingItems.SCPSetting(),
                ImageUploadMethod.PowerShell => new UploadSettingItems.PowerShellSetting(),
                _ => null
            };
        }
    }
}