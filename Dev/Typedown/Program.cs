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
#if AUTOMATION_TEST_HOST
            // The test host: its data root comes from the command line (or a fixed manual one), before anything else.
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, Core.Config.AutomationTestRootArgument);
            Core.Config.UseAutomationTestHost(at >= 0 && at + 1 < args.Length ? args[at + 1]
                : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Typedown-AutomationTests", "manual"));
#endif
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            // A crash that kills the process without reaching the handlers above (an exception escaping into the
            // native XAML dispatcher, a stack overflow) leaves no report at all. First-chance logging records
            // every exception as it is thrown, so the last lines of debug.log still say what happened.
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
            App.Launch();
        }

        private static int firstChanceCount;

        private static void OnFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            // handled exceptions are normal in places (settings file in use, clipboard formats); keep the flood bounded
            if (System.Threading.Interlocked.Increment(ref firstChanceCount) > 200) return;
            Log.Debug($"exception: {e.Exception.GetType().Name}: {e.Exception.Message}\n{e.Exception.StackTrace}");
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.WriteLocal(e.IsTerminating ? "UnhandledException" : "UnhandledException-NonFatal", e.ExceptionObject?.ToString());
            if (e.IsTerminating)
            {
                try { Log.Report("UnhandledException", e.ExceptionObject.ToString()).Wait(3000); } catch { }
            }
        }
    }
}
