using System;
using System.Threading.Tasks;
using Typedown.Core.Controls;
using Typedown.Core.Utilities;

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
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            App.Launch();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.IsTerminating)
            {
                Task.Run(() => Common.Post("https://typedown.ownbox.cn/report", new
                {
                    version = AboutApp.GetAppVersion(),
                    system = Environment.OSVersion.VersionString,
                    type = "UnhandledException",
                    content = e.ExceptionObject.ToString(),
                })).Wait();
            }
        }
    }
}
