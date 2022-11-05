using System;
using System.Windows;
using Typedown.Windows;

namespace Typedown
{
    public class App : Application
    {
        public App()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new MainWindow().Show();
        }
    }
}
