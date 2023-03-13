using System;
using System.Reactive.Disposables;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Typedown.XamlUI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class MenuBar : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;
        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public MenuBar()
        {
            InitializeComponent();
        }

        private void OnSizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            if (TitleGrid != null)
            {
                if (ActualWidth / 2 > MenuBarControl.ActualWidth + TitleTextBlock.ActualWidth / 2 + 16)
                {
                    TitleGrid.Margin = new(0);
                    Grid.SetColumn(TitleGrid, 0);
                    Grid.SetColumnSpan(TitleGrid, 3);
                }
                else
                {
                    TitleGrid.Margin = new(0, 0, 46 * 3, 0);
                    Grid.SetColumn(TitleGrid, 1);
                    Grid.SetColumnSpan(TitleGrid, 2);
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Settings.AppCompactMode)
            {
                var uiViewModel = ViewModel.UIViewModel;
                var oldCaptionHeight = uiViewModel.CaptionHeight;
                uiViewModel.CaptionHeight = 40;
                disposables.Add(Disposable.Create(() => uiViewModel.CaptionHeight = oldCaptionHeight));
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Dispose();
             Bindings?.StopTracking();
        }

        private Visibility IsCollapsed(bool boolean) => boolean ? Visibility.Collapsed : Visibility.Visible;

        private DateTime prevLeftButtonPressedTime = DateTime.Now;

        private void OnMenuBarPointerEvent(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (Settings.AppCompactMode && e.OriginalSource is Grid && XamlWindow.GetWindow(this) is XamlWindow window)
            {
                _ = Dispatcher.RunIdleAsync(() =>
                {
                    PInvoke.GetCursorPos(out var point);
                    var packedPoint = (point.Y << 16) + point.X;
                    var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
                    if (kind == PointerUpdateKind.LeftButtonPressed)
                    {
                        if ((DateTime.Now - prevLeftButtonPressedTime).TotalMilliseconds < PInvoke.GetDoubleClickTime())
                            window.PostMessage((uint)PInvoke.WindowMessage.WM_NCLBUTTONDBLCLK, (uint)PInvoke.HitTestFlags.CAPTION, packedPoint);
                        else
                            window.PostMessage((uint)PInvoke.WindowMessage.WM_NCLBUTTONDOWN, (uint)PInvoke.HitTestFlags.CAPTION, packedPoint);
                        prevLeftButtonPressedTime = DateTime.Now;
                    }
                    if (kind == PointerUpdateKind.RightButtonReleased)
                    {
                        window.PostMessage((uint)PInvoke.WindowMessage.WM_NCRBUTTONUP, (uint)PInvoke.HitTestFlags.CAPTION, packedPoint);
                    }
                });
            }
        }
    }
}
