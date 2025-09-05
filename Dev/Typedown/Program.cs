using System;
using Typedown.Core.Utilities;

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
                Log.Report("UnhandledException", e.ExceptionObject.ToString()).Wait();
            }
        }
    }
}
