using System.Runtime.InteropServices;
using System;
using Typedown.XamlUI;
using Windows.UI.Xaml.Hosting;
using System.Threading.Tasks;

namespace Typedown.Windows
{
    public class SignalWindow : Window
    {
        private readonly DesktopWindowXamlSource _xamlSource = new();

        public static SignalWindow Current { get; private set; }

        public SignalWindow()
        {
            Closed += OnClosed;
        }

        private void OnClosed(object sender, ClosedEventArgs e)
        {
            XamlApplication.Current.Exit();
        }

        protected override void InitializeWindow(WindowStyle? style = null, WindowExStyle? exStyle = null, nint hWndParent = 0)
        {
            base.InitializeWindow(0, 0, 0);
        }

        public static void Initialize()
        {
            if (Current != null)
                return;
            Current = new SignalWindow();
            Current.InitializeWindow();
        }
    }
}
