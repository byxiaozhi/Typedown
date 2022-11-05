using Microsoft.Toolkit.Win32.UI.XamlHost;
using Typedown.Universal.Interfaces;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}

namespace Typedown.Universal
{
    public sealed partial class App : XamlApplication
    {
        public static new App Current { get; private set; }

        public static CoreDispatcher Dispatcher { get; private set; }

        public App()
        {
            Initialize();
            ((Window.Current as object) as IWindowPrivate).TransparentBackground = true;
            Current = this;
            Dispatcher = Window.Current.Dispatcher;
        }

        public static void InitializeXAMLIsland()
        {
            if (Current == null)
                new App();
        }
    }
}


