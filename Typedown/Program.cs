using System;
using Typedown.Utilities;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using Typedown.Universal.Utilities;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Windows;

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
                    Startup();
                });
            }
            else
            {
                OpenNewWindow();
            }
        }

        private static void Startup()
        {
            hHook = PInvoke.SetWindowsHookEx(PInvoke.HookType.WH_CBT, hookProc, IntPtr.Zero, PInvoke.GetCurrentThreadId());
            using (new Universal.App())
            {
                new MainWindow().Show();
                ExitSigWindow.Start();
                Task.Run(ListenPipe);
            }
        }

        private static IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code == 3) // HCBT_CREATEWND 
            {
                var className = PInvoke.GetClassName(wParam);
                if (className == typeof(global::Windows.UI.Core.CoreWindow).FullName)
                    CoreWindow.SetCoreWindow(wParam);
            }
            return PInvoke.CallNextHookEx(hHook, code, wParam, lParam);
        }

        private static void OpenNewWindow()
        {
            using var client = new NamedPipeClientStream(".", "Typedown.Program.PiPe", PipeDirection.InOut);
            client.Connect(1000);
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
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                }
            }
        }
    }
}
