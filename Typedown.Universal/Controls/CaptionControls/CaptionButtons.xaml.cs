using System;
using System.Net;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public sealed partial class CaptionButtons : UserControl
    {
        private IWindowService WindowService => this.GetService<IWindowService>();

        private nint ParentHandle => WindowService.GetParent(WindowService.GetWindow(this));

        private const uint WM_SYSCOMMAND = 0x0112;
        private const uint SC_MINIMIZE = 0xF020;
        private const uint SC_MAXIMIZE = 0xF030;
        private const uint SC_RESTORE = 0xF120;
        private const uint SC_CLOSE = 0xF060;

        public CaptionButtons()
        {
            InitializeComponent();
        }

        private void Button_Minimize_Click(object sender, RoutedEventArgs e) => SendCommand(SC_MINIMIZE);

        private void Button_MaximizeOrRestore_Click(object sender, RoutedEventArgs e) => SendCommand(WindowService.IsZoomed(ParentHandle) ? SC_RESTORE : SC_MAXIMIZE);

        private void Button_Close_Click(object sender, RoutedEventArgs e) => SendCommand(SC_CLOSE);

        private void SendCommand(uint command) => WindowService.PostMessage(ParentHandle, WM_SYSCOMMAND, command, IntPtr.Zero);

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowService.WindowStateChanged += OnWindowStateChanged;
            UpdateMaximizeButtonIcon();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => WindowService.WindowStateChanged -= OnWindowStateChanged;

        private void OnWindowStateChanged(object sender, nint hWnd) => UpdateMaximizeButtonIcon();

        private void UpdateMaximizeButtonIcon() => Icon_MaximizeOrRestore.Glyph = WebUtility.HtmlDecode(WindowService.IsZoomed(ParentHandle) ? "&#xe923;" : "&#xe922;");
    }
}
