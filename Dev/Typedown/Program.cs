using System;
using Typedown.Utilities;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using Typedown.Core.Utilities;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Windows;
using Microsoft.Toolkit.Win32.UI.XamlHost;
using Windows.UI.Xaml.Markup;
using System.Collections.Generic;
using Typedown.Core.Interfaces;
using Windows.UI.Xaml;

namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}

namespace Typedown
{
    public class Program
    {
        private static readonly PInvoke.HookProc hookProc = new(HookProc);

        private static IntPtr hHook;

        private static readonly Mutex mutex = new(true, "Typedown.Program.Mutex");

        public static Dispatcher Dispatcher { get; private set; }

        [STAThread]
        public static void Main()
        {
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                Dispatcher.Run(() =>
                {
                    Dispatcher = Dispatcher.Current;
                    try
                    {
                        Startup();
                    }
                    catch
                    {
                        Dispatcher.Shutdown();
                    }
                });
            }
            else
            {
                OpenNewWindow();
            }
        }

        private static void Startup()
        {
            PInvoke.SetProcessDPIAware();
            PInvoke.EnableMouseInPointer(true);
            hHook = PInvoke.SetWindowsHookEx(PInvoke.HookType.WH_CBT, hookProc, IntPtr.Zero, PInvoke.GetCurrentThreadId());
            var providers = new List<IXamlMetadataProvider>() { new Core.Typedown_Core_XamlTypeInfo.XamlMetaDataProvider() };
            var xamlApp = new XamlApplication(providers) { Resources = new Core.Resources() };
            ((Window.Current as object) as IWindowPrivate).TransparentBackground = true;
            new MainWindow().Show();
            ExitSigWindow.Start();
            Task.Run(ListenPipe);
        }

        private static IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code == 3) // HCBT_CREATEWND 
            {
                if (CoreWindowHelper.IsCoreWindow(wParam))
                    CoreWindowHelper.SetCoreWindow(wParam);
            }
            return PInvoke.CallNextHookEx(hHook, code, wParam, lParam);
        }

        private static void OpenNewWindow()
        {
            using var client = new NamedPipeClientStream(".", "Typedown.Program.PiPe", PipeDirection.InOut);
            client.Connect();
            using var reader = new StreamReader(client);
            using var writer = new StreamWriter(client);
            writer.WriteLine(string.Join("\0", Environment.GetCommandLineArgs()));
            writer.Flush();
            var handle = new IntPtr(long.Parse(reader.ReadLine()));
            PInvoke.SetForegroundWindow(handle);
        }

        private static async void ListenPipe()
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream("Typedown.Program.PiPe", PipeDirection.InOut);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server);
                    using var writer = new StreamWriter(server);
                    var args = (await reader.ReadLineAsync()).Split("\0");
                    var handle = await Dispatcher.InvokeAsync(() => Utilities.Common.OpenNewWindow(args));
                    await writer.WriteLineAsync(handle.ToString());
                    await writer.FlushAsync();
                    await Dispatcher.InvokeAsync(() => { });
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                }
            }
        }
    }
}
