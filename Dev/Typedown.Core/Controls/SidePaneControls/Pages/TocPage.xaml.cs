using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SidePanelControls.Pages
{
    public sealed partial class TocPage : Page
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public EditorViewModel Editor => ViewModel.EditorViewModel;

        public TocPage()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }
    }
}
