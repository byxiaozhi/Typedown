using System;
using System.Net;
using System.Reactive.Disposables;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Typedown.Core.Controls
{
    public sealed partial class CaptionButtons : UserControl
    {
        private IWindowService WindowService => this.GetService<IWindowService>();

        private nint WindowHandle => WindowService.GetWindow(this);

        private const uint WM_SYSCOMMAND = 0x0112;
        private const uint SC_MINIMIZE = 0xF020;
        private const uint SC_MAXIMIZE = 0xF030;
        private const uint SC_RESTORE = 0xF120;
        private const uint SC_CLOSE = 0xF060;

        private readonly CompositeDisposable disposables = new();

        public CaptionButtons()
        {
            InitializeComponent();
        }

        private void Button_Minimize_Click(object sender, RoutedEventArgs e) => SendCommand(SC_MINIMIZE);

        private void Button_MaximizeOrRestore_Click(object sender, RoutedEventArgs e) => SendCommand(PInvoke.IsZoomed(WindowHandle) ? SC_RESTORE : SC_MAXIMIZE);

        private void Button_Close_Click(object sender, RoutedEventArgs e) => SendCommand(SC_CLOSE);

        private void SendCommand(uint command) => PInvoke.PostMessage(WindowHandle, WM_SYSCOMMAND, (nint)command, IntPtr.Zero);

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            disposables.Add(WindowService.WindowStateChanged.Subscribe(OnWindowStateChanged));
            disposables.Add(WindowService.WindowIsActivedChanged.Subscribe(OnWindowIsActivedChanged));
            UpdateMaximizeButtonIcon();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => disposables.Clear();

        private void OnWindowStateChanged(nint hWnd) => UpdateMaximizeButtonIcon();

        private void OnWindowIsActivedChanged(nint hWnd)
        {
            var isActived = PInvoke.GetForegroundWindow() == hWnd;
            var foreground = Resources[isActived ? "ActivedButtonForeground" : "DeactivedButtonForeground"] as Brush;
            Button_Minimize.Foreground = foreground;
            Button_MaximizeOrRestore.Foreground = foreground;
            Button_Close.Foreground = foreground;
        }

        private void UpdateMaximizeButtonIcon()
        {
            var isZoomed = PInvoke.IsZoomed(WindowHandle);
            Icon_MaximizeOrRestore.Glyph = WebUtility.HtmlDecode(isZoomed ? "&#xe923;" : "&#xe922;");
            ToolTip.SetTextResource(Button_MaximizeOrRestore, isZoomed ? "Restore/Text" : "Maximize/Text");
        }
    }
}
