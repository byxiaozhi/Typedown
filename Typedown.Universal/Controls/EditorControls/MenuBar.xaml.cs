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
    public sealed partial class MenuBar : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;
        public EditorViewModel Editor => ViewModel?.EditorViewModel;
        public FileViewModel File => ViewModel?.FileViewModel;
        public FormatViewModel Format => ViewModel?.FormatViewModel;
        public ParagraphViewModel Paragraph => ViewModel?.ParagraphViewModel;
        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public MenuBar()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
