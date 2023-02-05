using System;

namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}

namespace Typedown
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            App.Launch();
        }
    }
}
