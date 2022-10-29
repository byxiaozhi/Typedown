using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

namespace Typedown.Universal.Controls
{
    public sealed partial class StatusBar : UserControl
    {
        public AppViewModel AppViewModel => this.GetService<AppViewModel>();

        public SettingsViewModel SettingsViewModel => this.GetService<SettingsViewModel>();

        public StatusBar()
        {
            this.InitializeComponent();
        }
    }
}
