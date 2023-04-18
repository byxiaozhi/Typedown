using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Core.Utilities;
using Typedown.Windows;
using Typedown.XamlUI;
using Windows.UI.Core;
using Windows.UI.Xaml.Markup;

namespace Typedown
{
    public class App : XamlApplication
    {
        private static readonly Mutex mutex = new(true, "Typedown.App.Mutex");

        private App(IEnumerable<IXamlMetadataProvider> providers) : base(providers) { }

        public static void Launch()
        {
            try
            {
                if (mutex.WaitOne(TimeSpan.Zero, true))
                {
                    LaunchNewApplication();
                }
                else
                {
                    OpenNewWindow();
                }
            }
            catch (AbandonedMutexException)
            {
                mutex.ReleaseMutex();
                Launch();
            }
        }

        public static void LaunchNewApplication()
        {
            var providers = new List<IXamlMetadataProvider>() { new Core.Typedown_Core_XamlTypeInfo.XamlMetaDataProvider() };
            var xamlApp = new App(providers) { Resources = new Core.Resources() };
            xamlApp.Run();
        }

        protected override async void OnLaunched()
        {
            base.OnLaunched();
            if (!await EnvCheck.EnsureWebView2Installed())
            {
                Exit();
                return;
            }
            var window = new MainWindow();
            window.Show(ShowWindowCommand.SW_HIDE);
            ListenPipe(window.Dispatcher);
        }

        private static async void ListenPipe(CoreDispatcher dispatcher)
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream("Typedown.App.PiPe", PipeDirection.InOut);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server);
                    using var writer = new StreamWriter(server);
                    var args = (await reader.ReadLineAsync()).Split("\0");
                    var handle = await dispatcher.RunIdleAsync(() => Utilities.Common.OpenNewWindow(args));
                    await writer.WriteLineAsync(handle.ToString());
                    await writer.FlushAsync();
                }
                catch (Exception)
                {
                    await Task.Delay(1000);
                }
            }
        }

        internal static void OpenNewWindow()
        {
            try
            {
                using var client = new NamedPipeClientStream(".", "Typedown.App.PiPe", PipeDirection.InOut);
                client.Connect();
                using var reader = new StreamReader(client);
                using var writer = new StreamWriter(client);
                writer.WriteLine(string.Join("\0", Environment.GetCommandLineArgs()));
                writer.Flush();
                try
                {
                    if (long.TryParse(reader.ReadLine(), out var handle))
                        PInvoke.SetForegroundWindow((nint)handle);
                }
                catch
                {
                    // Ignore
                }
            }
            catch
            {
                LaunchNewApplication();
            }
        }
    }
}
