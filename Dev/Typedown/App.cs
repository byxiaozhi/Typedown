using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Typedown.Core.Utilities;
using Typedown.Windows;
using Typedown.XamlUI;
using Windows.UI.Xaml.Markup;

namespace Typedown
{
    public class App : XamlApplication
    {
        private static readonly Mutex mutex = new(true, "Typedown.App.Mutex");

        private App(IEnumerable<IXamlMetadataProvider> providers) : base(providers) { }

        public static void Launch()
        {
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                var providers = new List<IXamlMetadataProvider>() { new Core.Typedown_Core_XamlTypeInfo.XamlMetaDataProvider() };
                var xamlApp = new App(providers) { Resources = new Core.Resources() };
                xamlApp.Run();
            }
            else
            {
                OpenNewWindow();
            }
        }

        protected override async void OnLaunched()
        {
            base.OnLaunched();
            SignalWindow.Initialize();
            if (!await EnvCheck.EnsureWebView2Installed())
            {
                Exit();
                return;
            }
            var window = new MainWindow();
            var hasStartupPlacement = window.AppViewModel.SettingsViewModel.StartupPlacement.HasValue;
            window.Show(hasStartupPlacement ? ShowWindowCommand.SW_HIDE : ShowWindowCommand.SW_NORMAL);
            ListenPipe();
        }

        private static async void ListenPipe()
        {
            var dispatcher = Dispatcher.Current;
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream("Typedown.App.PiPe", PipeDirection.InOut);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server);
                    using var writer = new StreamWriter(server);
                    var args = (await reader.ReadLineAsync()).Split("\0");
                    var handle = await dispatcher.RunAsync(() => Utilities.Common.OpenNewWindow(args));
                    await writer.WriteLineAsync(handle.ToString());
                    await writer.FlushAsync();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                }
            }
        }

        internal static void OpenNewWindow()
        {
            using var client = new NamedPipeClientStream(".", "Typedown.App.PiPe", PipeDirection.InOut);
            client.Connect();
            using var reader = new StreamReader(client);
            using var writer = new StreamWriter(client);
            writer.WriteLine(string.Join("\0", Environment.GetCommandLineArgs()));
            writer.Flush();
            if (long.TryParse(reader.ReadLine(), out var handle))
                PInvoke.SetForegroundWindow((nint)handle);
        }
    }
}
