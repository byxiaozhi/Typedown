using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public class GlobalFrame : Frame
    {
        public AppViewModel AppViewModel => this.GetService<AppViewModel>();

        public GlobalFrame()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            Navigated += OnNavigated;
        }

        private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            AppViewModel.FrameStack = AppViewModel.FrameStack.Append(this).ToList();
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            AppViewModel.FrameStack = AppViewModel.FrameStack.Take(AppViewModel.FrameStack.Count - 1).ToList();
        }

        private void OnNavigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            AppViewModel.GoBackCommand.IsExecutable = AppViewModel.FrameStack.Any(x => x.CanGoBack);
        }
    }
}
