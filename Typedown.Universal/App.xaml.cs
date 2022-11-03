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
        public App()
        {
            Initialize();
            ((Window.Current as object) as IWindowPrivate).TransparentBackground = true;
        }
    }
}


