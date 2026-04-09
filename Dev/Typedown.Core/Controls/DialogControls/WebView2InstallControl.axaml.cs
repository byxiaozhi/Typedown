using System;
using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Typedown.Core.Controls
{
    public sealed partial class WebView2InstallControl : UserControl
    {
        public EventHandler CloseButtonClick;

        public EventHandler InstallButtonClick;

        public WebView2InstallControl()
        {
            this.InitializeComponent();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            CloseButtonClick?.Invoke(this, EventArgs.Empty);
        }

        private void OnInstallButtonClick(object sender, RoutedEventArgs e)
        {
            InstallButtonClick?.Invoke(this, EventArgs.Empty);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
