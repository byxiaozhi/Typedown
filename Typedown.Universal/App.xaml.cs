using Microsoft.Toolkit.Win32.UI.XamlHost;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace Typedown.Universal
{
    public sealed partial class App : XamlApplication
    {
        public App()
        {
            Initialize();
        }
    }
}


