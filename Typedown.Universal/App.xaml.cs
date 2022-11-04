using Microsoft.Toolkit.Win32.UI.XamlHost;
using Typedown.Universal.Interfaces;
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

        public App()
        {
            Current = this;
            Initialize();
            ((Window.Current as object) as IWindowPrivate).TransparentBackground = true;
        }

        public static void InitializeXAMLIsland()
        {
            if (Current == null)
                new App();
        }
    }
}


