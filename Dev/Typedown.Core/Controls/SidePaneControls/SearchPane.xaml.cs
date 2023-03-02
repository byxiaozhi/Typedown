using System;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class SearchPane : UserControl
    {
        public event EventHandler Close;

        public SearchPane()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = Dispatcher.RunIdleAsync(() => SearchTextBox.Focus(FocusState.Programmatic));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void OnSearchTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchTextBox.Text))
                Close?.Invoke(this, EventArgs.Empty);
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close?.Invoke(this, EventArgs.Empty);
        }
    }
}
