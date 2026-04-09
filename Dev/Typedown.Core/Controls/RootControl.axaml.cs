using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Typedown.Core.Controls
{
    public sealed partial class RootControl : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public RootControl()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // ViewModel.XamlRoot = XamlRoot; // Not applicable in standard Avalonia
            disposables.Add(ViewModel.NavigateCommand.OnExecute.Subscribe(args => Navigate(args)));
            // GlobalFrame.Navigate(typeof(MainPage), null);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
            // Bindings?.StopTracking(); // Not needed in Avalonia
        }

        private void Navigate(string args)
        {
            // var path = args?.TrimStart('/').Split('/');
            // if (path != null && path.Any())
            // {
            //     var type = Route.GetRootPageType(path.First());
            //     if (type != Frame.SourcePageType)
            //     {
            //         Frame.Navigate(type, string.Join('/', path.Skip(1)));
            //     }
            // }
        }

        public static bool GetCaptionIsLoad(bool compactMode, Type currentPage)
        {
            return !compactMode; // || currentPage != typeof(MainPage);
        }
    }
}

